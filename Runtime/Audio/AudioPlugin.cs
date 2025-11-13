using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Audio.Config;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Audio
{
  /// <summary>
  ///   Audio Plugin that registers the AudioService globally with proper mixer integration and settings binding.
  ///   Provides audio playback service with Unity Audio Mixer integration and reactive settings.
  ///   Interface sounds need to persist across scenes, so this is registered globally.
  /// </summary>
  public sealed class AudioPlugin : AbstractGamePlugin, IDependencyDeclaration, IRuntimePlugin
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioPlugin>().ForFeature("Audio"));
    private new static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private AudioConfig config;

    private readonly SemaphoreSlim initializationSemaphore = new(1, 1);
    private IAudioService audioService;
    private CancellationTokenSource cts;

    private IObjectResolver resolver;

    [ShowInInspector]
    [ReadOnly]
    private AudioService audioServiceValue => audioService as AudioService;

    private void OnDestroy()
    {
      cts?.Cancel();
      cts?.Dispose();

      initializationSemaphore?.Dispose();

      // Dispose audio service if it implements IDisposable
      if (audioService is IDisposable disposableService)
        disposableService.Dispose();
    }

    public IEnumerable<Type> RequiredServices => new[] { typeof(ISettingsSystem) };
    public IEnumerable<Type> OptionalServices => Array.Empty<Type>();


    public override void Register(IContainerBuilder builder)
    {
      log.ForMethod().Debug("AudioPlugin Register called");

      // Register the config
      builder.RegisterInstance(config).As<AudioConfig>();

      DontDestroyOnLoad(gameObject);

      // Register the AudioService with factory pattern to handle dependencies
      builder.Register<IAudioService>(resolver =>
      {
        try
        {
          AudioConfig audioConfig = resolver.Resolve<AudioConfig>();
          ISettingsSystem settingsSystem = resolver.Resolve<ISettingsSystem>();
          IRuntimeAssetService assetService = resolver.Resolve<IRuntimeAssetService>();

          // Create audio root transform
          Transform audioRoot = CreateAudioRoot();

          return new AudioService(audioRoot, audioConfig, settingsSystem, assetService);
        }
        catch (VContainerException ex)
        {
          log.ForMethod().Error(ex, "Failed to resolve dependencies for AudioService");
          throw;
        }
      }, Lifetime.Singleton);

      log.ForMethod().Information("AudioPlugin Register completed - IAudioService registered");
    }

    public void PerformSetup(IObjectResolver objectResolver)
    {
      log.ForMethod().Information("AudioPlugin PerformSetup called");
      resolver = objectResolver;
      cts = new CancellationTokenSource();
    }

    public void PerformRuntimeInitialization(IObjectResolver objectResolver)
    {
      log.ForMethod().Information("AudioPlugin PerformRuntimeInitialization called");

      // Use semaphore to ensure only one initialization happens
      bool isAcquired = false;
      try
      {
        // Try to acquire semaphore (with 100ms timeout to avoid deadlock)
        isAcquired = initializationSemaphore.Wait(100);
        if (!isAcquired)
        {
          log.ForMethod().Warning("Could not acquire initialization semaphore (timed out) - another initialization is in progress");
          return;
        }

        // Ensure cts is initialized
        cts ??= new CancellationTokenSource();

        // Initialize immediately
        if (resolver == null)
        {
          log.ForMethod().Error("Resolver is NULL - cannot initialize");
          return;
        }

        log.ForMethod().Debug("Resolving IAudioService...");
        audioService = resolver.Resolve<IAudioService>();
        log.ForMethod().Information("Resolved IAudioService: {Type}", audioService?.GetType().Name ?? "NULL");

        ISettingsSystem settingsSystem = resolver.Resolve<ISettingsSystem>();
        log.ForMethod().Information("Resolved ISettingsSystem: {Type}", settingsSystem?.GetType().Name ?? "NULL");

        if (audioService is AudioService service && settingsSystem != null)
        {
          log.ForMethod().Information("Calling InitializeAsync on AudioService");
          service.InitializeAsync(settingsSystem, cts.Token).Forget();
          log.ForMethod().Information("AudioService initialization started");
        }
        else
        {
          log.ForMethod().Warning("Could not initialize AudioService - service or settingsSystem is invalid");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize Audio");
      }
      finally
      {
        if (isAcquired)
          initializationSemaphore.Release();
      }
    }

    public void Initialize(IObjectResolver resolver)
    {
      log.ForMethod().Information("AudioPlugin Initialize called");
      PerformSetup(resolver);
      if (AreDependenciesReady(resolver))
        PerformRuntimeInitialization(resolver);
      else
        log.ForMethod().Warning("AudioPlugin dependencies not ready for runtime initialization");
    }

    public bool AreDependenciesReady(IObjectResolver resolver)
    {
      // Check if all required services are available
      bool ready = resolver.TryResolve<ISettingsSystem>(out _);
      if (ready)
        log.ForMethod().Information("AudioPlugin dependencies are ready - ISettingsSystem found");
      else
        log.ForMethod().Warning("AudioPlugin dependencies not ready - ISettingsSystem not found");
      return ready;
    }

    private async UniTaskVoid InitializeAsync(CancellationToken ct)
    {
      log.ForMethod().Information("AudioPlugin InitializeAsync started");

      if (resolver == null)
      {
        log.ForMethod().Error("Resolver is NULL in InitializeAsync - skipping initialization");
        return;
      }

      // Wait a frame to ensure everything is ready
      await UniTask.Yield(ct);

      try
      {
        log.ForMethod().Information("Resolving IAudioService...");
        audioService = resolver.Resolve<IAudioService>();
        log.ForMethod().Information("Resolved IAudioService: {Type}", audioService?.GetType().Name ?? "NULL");

        if (audioService == null)
        {
          log.ForMethod().Error("audioService is NULL after resolve");
          return;
        }

        log.ForMethod().Information("Resolving ISettingsSystem...");
        ISettingsSystem settingsSystem = resolver.Resolve<ISettingsSystem>();
        log.ForMethod().Information("Resolved ISettingsSystem: {Type}", settingsSystem?.GetType().Name ?? "NULL");

        if (settingsSystem == null)
        {
          log.ForMethod().Error("settingsSystem is NULL after resolve");
          return;
        }

        // Initialize audio service with settings binding
        if (audioService is AudioService service)
        {
          log.ForMethod().Information("Calling InitializeAsync on AudioService");
          await service.InitializeAsync(settingsSystem, ct);
          log.ForMethod().Information("Audio runtime initialized successfully");
        }
        else
        {
          log.ForMethod().Error("AudioService is not AudioService type: {Type}", audioService?.GetType().Name ?? "NULL");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize Audio runtime");
      }
    }

    /// <summary>
    ///   Creates the audio root transform for organizing audio sources.
    ///   Global audio root persists across all scenes for interface sounds.
    /// </summary>
    private Transform CreateAudioRoot()
    {
      GameObject audioRootObject = new("[Global] AudioRoot");

      // Ensure it persists across scenes - critical for interface sounds
      DontDestroyOnLoad(audioRootObject);

      return audioRootObject.transform;
    }
  }
}