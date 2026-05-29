using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Audio.Config;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Utilities.Extensions;
using R3;
using Serilog;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace MToolKit.Runtime.Audio
{
  public class AudioService : IAudioService, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioService>().ForFeature("Audio.Service"));

    // Static lock to prevent concurrent loads of the same AssetReference across multiple instances
    private static readonly object assetLoadLock = new();

    // Track which AssetReferences are currently being loaded to prevent duplicate loads
    private static readonly HashSet<string> activeLoads = new();
    private static ILogger log => logLazy.Value ?? Logger.None;


    private readonly IRuntimeAssetService assetService;
    private readonly Transform audioRoot;
    private readonly AudioConfig config;
    private readonly Dictionary<AudioClip, float> lastPlayTimes = new();
    private readonly ISettingsSystem settingsSystem;
    private readonly CompositeDisposable subscriptions = new();
    private ObjectPool<AudioSource> audioSourcePool;

    // Cached prefab loaded from Addressables
    private AudioSource cachedAudioSourcePrefab;
    private bool isInitialized;
    private bool isPrefabLoaded;

    public AudioService(Transform audioRoot, AudioConfig config, ISettingsSystem settingsSystem, IRuntimeAssetService assetService)
    {
      this.audioRoot = audioRoot;
      this.config = config;
      this.settingsSystem = settingsSystem;
      this.assetService = assetService;

      log.ForMethod(nameof(AudioService)).Verbose("AudioService constructor called with asset service: {Type}", assetService?.GetType().Name ?? "NULL");
    }

    #region IAudioService Members

    public void StopAllSounds()
    {
      if (audioSourcePool == null) return;

      foreach (AudioSource source in audioSourcePool.GetAll())
        if (source != null && source.gameObject != null && source.isPlaying)
          try
          {
            source.Stop();
            source.gameObject.SetActive(false);
          }
          catch (MissingReferenceException)
          {
            // Object was destroyed during shutdown, ignore
            log.ForMethod().Debug("AudioSource was destroyed during StopAllSounds");
          }
    }

    public void PlayOneShot(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default)
    {
      PlayOneShotAsync(clip, position, volume, pitch, audioType, ct).Forget();
    }

    public void PlayOneShot(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game,
      CancellationToken ct = default)
    {
      PlayOneShotAsync(PickRandomClip(clips), position, volume, pitch, audioType, ct).Forget();
    }

    public async UniTask PlayOneShotAsync(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game,
      CancellationToken ct = default)
    {
      await PlayOneShotAsync(PickRandomClip(clips), position, volume, pitch, audioType, ct);
    }

    public async UniTask PlayOneShotAsync(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game,
      CancellationToken ct = default)
    {
      if (clip == null) return;

      AudioSource audioSource = audioSourcePool.Get();
      try
      {
        // Configure audio source
        audioSource.transform.position = position ?? Vector3.zero;
        audioSource.outputAudioMixerGroup = GetMixerGroupForAudioType(audioType);
        audioSource.pitch = pitch;

        // Apply volume with mixer group consideration
        float finalVolume = volume * GetVolumeForAudioType(audioType);
        audioSource.PlayOneShot(clip, finalVolume);

        // Wait for audio to finish playing, handling potential destruction during shutdown
        await UniTask.WaitUntil(() =>
        {
          try
          {
            // Check if object is destroyed
            if (audioSource == null || audioSource.gameObject == null)
              return true; // Object destroyed, stop waiting

            return !audioSource.isPlaying;
          }
          catch (MissingReferenceException)
          {
            // Object was destroyed, stop waiting
            return true;
          }
        }, cancellationToken: ct);
      }
      catch (OperationCanceledException)
      {
        // Handle cancellation gracefully
      }
      finally
      {
        // Check if audioSource is still valid before releasing
        if (audioSource != null && audioSource.gameObject != null)
          try
          {
            audioSourcePool.Release(audioSource);
          }
          catch (MissingReferenceException)
          {
            // Object was destroyed during shutdown, ignore
            log.ForMethod().Debug("AudioSource was destroyed during shutdown, skipping pool release");
          }
      }
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      subscriptions?.Dispose();

      // Stop all sounds before disposing the pool
      StopAllSounds();

      // Dispose of the pool
      audioSourcePool?.Dispose();
      audioSourcePool = null;

      isInitialized = false;
    }

    #endregion

    public async UniTask InitializeAsync(ISettingsSystem settingsSystem, CancellationToken ct = default)
    {
      if (isInitialized)
      {
        log.ForMethod().Warning("AudioService already initialized, skipping");
        return;
      }

      log.ForMethod().Verbose("Initializing AudioService with settings integration");

      // Load the AudioSource prefab from Addressables if not already loaded
      await LoadAudioSourcePrefabAsync(ct);

      // Initialize the pool now that we have the prefab
      InitializePool();

      // Subscribe to volume changes
      if (settingsSystem?.AudioSettings != null)
      {
        log.ForMethod().Verbose("AudioSettings available, subscribing to volume changes");
        SubscribeToVolumeChanges();
      }
      else
      {
        log.ForMethod().Warning("AudioSettings is NULL - cannot subscribe to volume changes");
      }

      isInitialized = true;
      log.ForMethod().Verbose("AudioService initialized successfully");
    }

    private async UniTask LoadAudioSourcePrefabAsync(CancellationToken ct = default)
    {
      // Early check without lock for performance
      if (isPrefabLoaded && cachedAudioSourcePrefab != null)
      {
        log.ForMethod().Information("AudioSource prefab already loaded (early check)");
        return;
      }

      // Try to load from Addressables first using AssetReference
      if (config.AudioSourcePrefabReference != null && config.AudioSourcePrefabReference.RuntimeKeyIsValid())
      {
        try
        {
          log.ForMethod().Verbose("Loading AudioSource prefab from Addressable AssetReference");

          string assetKey = config.AudioSourcePrefabReference.RuntimeKey.ToString();
          bool isAlreadyLoading = false;

          // Use lock to serialize load attempts - prevents concurrent LoadAssetAsync calls
          bool hasExistingHandle = false;
          lock (assetLoadLock)
          {
            // Check if already loaded
            if (isPrefabLoaded && cachedAudioSourcePrefab != null)
            {
              log.ForMethod().Warning("Already loaded by this instance");
              return;
            }

            // Check if asset is already loaded in Addressables
            if (config.AudioSourcePrefabReference.Asset != null)
            {
              log.ForMethod().Verbose("AssetReference already loaded, using cached asset");
              GameObject loadedPrefab = config.AudioSourcePrefabReference.Asset as GameObject;
              if (loadedPrefab != null)
              {
                cachedAudioSourcePrefab = loadedPrefab.GetComponent<AudioSource>();
                isPrefabLoaded = true;
              }
              return;
            }

            // Check if another thread is already loading this asset
            isAlreadyLoading = activeLoads.Contains(assetKey);
            hasExistingHandle = config.AudioSourcePrefabReference.OperationHandle.IsValid();
          }

          // Try to load outside the lock to avoid blocking other threads
          GameObject prefab = null;

          if (hasExistingHandle || isAlreadyLoading)
          {
            // Another thread is loading - wait for it to complete
            log.ForMethod().Verbose("AssetReference is already being loaded by another thread, waiting for it");

            // Wait for the asset to be loaded by polling
            int maxAttempts = 20;
            int attempt = 0;
            while (config.AudioSourcePrefabReference.Asset == null && attempt < maxAttempts)
            {
              await UniTask.Delay(50, cancellationToken: ct);
              attempt++;
            }

            // Try to get it from the AssetReference
            if (config.AudioSourcePrefabReference.Asset != null)
              prefab = config.AudioSourcePrefabReference.Asset as GameObject;
          }
          else
          {
            // No load in progress - we can safely try to load
            // Register this load to prevent others from starting
            lock (assetLoadLock)
            {
              // Double-check - maybe another thread started loading
              if (activeLoads.Contains(assetKey))
                log.ForMethod().Debug("Another thread started loading, waiting");
              // Will fall through to polling
              else
                activeLoads.Add(assetKey);
            }

            try
            {
              // Check if we're the one who should load (we added ourselves to activeLoads)
              bool shouldLoad = false;
              lock (assetLoadLock)
              {
                // If we're in activeLoads and there's no handle, we're responsible for loading
                shouldLoad = activeLoads.Contains(assetKey) && !config.AudioSourcePrefabReference.OperationHandle.IsValid();
              }

              if (shouldLoad)
              {
                AsyncOperationHandle<GameObject> handle = config.AudioSourcePrefabReference.LoadAssetAsync<GameObject>();
                prefab = await handle.ToUniTask(cancellationToken: ct);
              }
              else
              {
                // Another thread is loading, wait for it
                log.ForMethod().Verbose("Waiting for another thread to finish loading");
                int maxAttempts = 20;
                int attempt = 0;
                while (config.AudioSourcePrefabReference.Asset == null && attempt < maxAttempts)
                {
                  await UniTask.Delay(50, cancellationToken: ct);
                  attempt++;
                }

                if (config.AudioSourcePrefabReference.Asset != null)
                  prefab = config.AudioSourcePrefabReference.Asset as GameObject;
              }
            }
            catch (Exception loadEx)
            {
              // Handle "already loaded" error gracefully by waiting for the existing load
              if (loadEx.Message.Contains("already been loaded") ||
                  loadEx.Message.Contains("Already loaded"))
              {
                log.ForMethod().Verbose("AssetReference was already loaded by another thread, waiting for it");

                // Wait for the asset to be loaded by polling
                int maxAttempts = 20;
                int attempt = 0;
                while (config.AudioSourcePrefabReference.Asset == null && attempt < maxAttempts)
                {
                  await UniTask.Delay(50, cancellationToken: ct);
                  attempt++;
                }

                // Try to get it from the AssetReference
                if (config.AudioSourcePrefabReference.Asset != null)
                  prefab = config.AudioSourcePrefabReference.Asset as GameObject;
              }
              else
              {
                throw; // Re-throw if it's a different error
              }
            }
            finally
            {
              // Remove from active loads
              lock (assetLoadLock)
              {
                activeLoads.Remove(assetKey);
              }
            }
          }

          // Update state with loaded prefab
          if (prefab != null)
            lock (assetLoadLock)
            {
              if (!isPrefabLoaded)
              {
                cachedAudioSourcePrefab = prefab.GetComponent<AudioSource>();
                log.ForMethod().Verbose("Successfully loaded AudioSource prefab from Addressables");
                isPrefabLoaded = true;
              }
            }
        }
        catch (Exception ex)
        {
          log.ForMethod().Warning(ex, "Failed to load AudioSource prefab from Addressables, using direct prefab fallback");
          lock (assetLoadLock)
          {
            if (!isPrefabLoaded)
            {
              cachedAudioSourcePrefab = config.AudioSourcePrefab;
              isPrefabLoaded = true;
            }
          }
        }
      }
      else
      {
        // No AssetReference configured, use direct prefab
        log.ForMethod().Verbose("No AssetReference configured, using direct prefab");
        lock (assetLoadLock)
        {
          if (!isPrefabLoaded)
          {
            cachedAudioSourcePrefab = config.AudioSourcePrefab;
            isPrefabLoaded = true;
          }
        }
      }

      if (cachedAudioSourcePrefab != null)
        cachedAudioSourcePrefab.gameObject.SetActive(false);
    }

    private void InitializePool()
    {
      if (audioSourcePool != null)
      {
        log.ForMethod().Warning("AudioSourcePool already initialized");
        return;
      }

      if (cachedAudioSourcePrefab == null)
      {
        log.ForMethod().Error("Cannot initialize pool - AudioSource prefab is not loaded");
        return;
      }

      log.ForMethod().Verbose("Initializing AudioSource pool");
      audioSourcePool = new ObjectPool<AudioSource>(
        CreateAudioSource,
        source =>
        {
          if (source != null && source.gameObject != null) source.gameObject.SetActive(true);
        },
        source =>
        {
          if (source != null && source.gameObject != null) source.gameObject.SetActive(false);
        },
        source =>
        {
          if (source != null && source.gameObject != null) Object.Destroy(source.gameObject);
        },
        defaultCapacity: config.InitialPoolSize,
        maxSize: config.MaxPoolSize
        );
      log.ForMethod().Verbose("AudioSource pool initialized with capacity: {Capacity}, maxSize: {MaxSize}", config.InitialPoolSize, config.MaxPoolSize);
    }

    private AudioSource CreateAudioSource()
    {
      if (cachedAudioSourcePrefab == null)
      {
        log.ForMethod().Error("Cannot create AudioSource - prefab is not loaded");
        return null;
      }

      if (audioRoot == null)
      {
        log.ForMethod().Error("Cannot create AudioSource - audioRoot is null");
        return null;
      }

      AudioSource audioSource = Object.Instantiate(cachedAudioSourcePrefab, audioRoot);

      // Set default mixer group (Game) for pooling - will be overridden per sound type
      audioSource.outputAudioMixerGroup = config.GameMixerGroup;

      // Configure spatial audio if enabled
      if (config.EnableSpatialAudio)
      {
        audioSource.spatialBlend = 1f; // 3D
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.maxDistance = 50f;
        audioSource.minDistance = 1f;
      }
      else
      {
        audioSource.spatialBlend = 0f; // 2D
      }

      return audioSource;
    }

    private void SubscribeToVolumeChanges()
    {
      if (settingsSystem?.AudioSettings == null)
      {
        log.ForMethod().Warning("SubscribeToVolumeChanges called but AudioSettings is null");
        return;
      }

      log.ForMethod().Verbose("Subscribing to volume changes - MasterMixerGroup: {MixerGroup}", config.MasterMixerGroup?.name ?? "NULL");

      // Subscribe to volume changes and apply them to mixer groups
      subscriptions.Add(settingsSystem.AudioSettings.MasterVolume.Property.Subscribe(volume =>
        SetMixerVolume("MasterVolume", volume)));

      subscriptions.Add(settingsSystem.AudioSettings.MusicVolume.Property.Subscribe(volume =>
        SetMixerVolume("MusicVolume", volume)));

      subscriptions.Add(settingsSystem.AudioSettings.GameVolume.Property.Subscribe(volume =>
        SetMixerVolume("GameVolume", volume)));

      subscriptions.Add(settingsSystem.AudioSettings.InterfaceVolume.Property.Subscribe(volume =>
        SetMixerVolume("InterfaceVolume", volume)));

      // Apply initial volume values to mixer
      log.ForMethod().Verbose("Applying initial volume values...");
      SetMixerVolume("MasterVolume", settingsSystem.AudioSettings.MasterVolume.Value);
      SetMixerVolume("MusicVolume", settingsSystem.AudioSettings.MusicVolume.Value);
      SetMixerVolume("GameVolume", settingsSystem.AudioSettings.GameVolume.Value);
      SetMixerVolume("InterfaceVolume", settingsSystem.AudioSettings.InterfaceVolume.Value);
    }

    private void SetMixerVolume(string parameterName, float volume)
    {
      // Convert linear volume (0-1) to decibels (-80 to 0)
      float dbVolume = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;

      // Apply to mixer groups
      if (config.MasterMixerGroup?.audioMixer != null)
      {
        bool success = config.MasterMixerGroup.audioMixer.SetFloat(parameterName, dbVolume);
        if (success)
          log.ForMethod().Verbose("Set mixer parameter {ParameterName} to {Volume} ({DBVolume} dB)", parameterName, volume, dbVolume);
        else
          log.ForMethod().Warning("Failed to set mixer parameter: {ParameterName}", parameterName);
      }
      else
      {
        log.ForMethod().Warning("Cannot set mixer volume - MasterMixerGroup or audioMixer is null");
      }
    }

    private AudioMixerGroup GetMixerGroupForAudioType(EAudioTypes audioType)
    {
      return audioType switch
      {
        EAudioTypes.Master => config.MasterMixerGroup,
        EAudioTypes.Music => config.MusicMixerGroup,
        EAudioTypes.Game => config.GameMixerGroup,
        EAudioTypes.Interface => config.InterfaceMixerGroup,
        _ => config.MasterMixerGroup
      };
    }

    private float GetVolumeForAudioType(EAudioTypes audioType)
    {
      if (settingsSystem?.AudioSettings == null) return 1f;

      return audioType switch
      {
        EAudioTypes.Master => settingsSystem.AudioSettings.MasterVolume.Value,
        EAudioTypes.Music => settingsSystem.AudioSettings.MusicVolume.Value,
        EAudioTypes.Game => settingsSystem.AudioSettings.GameVolume.Value,
        EAudioTypes.Interface => settingsSystem.AudioSettings.InterfaceVolume.Value,
        _ => 1f
      };
    }

    public static AudioClip PickRandomClip(IReadOnlyList<AudioClip> clips)
    {
      return clips[Random.Range(0, clips.Count)];
    }
  }
}