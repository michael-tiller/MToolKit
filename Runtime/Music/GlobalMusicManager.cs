using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Music
{
  /// <summary>
  ///   Singleton music manager that handles continuous playback and smooth crossfading between audio sources.
  ///   Managed by the Bootstrap scene and persists for the application lifetime.
  /// </summary>
  /// <remarks>
  ///   <para>This singleton provides persistent music playback across all scenes with seamless transitions.</para>
  ///   <para><strong>Lifespan:</strong> Instantiated in the Bootstrap scene and persists until application shutdown.</para>
  ///   <para>
  ///     <strong>Key Features:</strong>
  ///   </para>
  ///   <list type="bullet">
  ///     <item>
  ///       <description>Cross-scene playback continuity</description>
  ///     </item>
  ///     <item>
  ///       <description>UniTask-based crossfading for smooth transitions</description>
  ///     </item>
  ///     <item>
  ///       <description>Reactive volume settings integration via ISettingsSystem</description>
  ///     </item>
  ///     <item>
  ///       <description>State management (play, pause, resume, stop)</description>
  ///     </item>
  ///     <item>
  ///       <description>Dual AudioSource architecture for seamless crossfading</description>
  ///     </item>
  ///   </list>
  /// </remarks>
  public class GlobalMusicManager : Singleton<GlobalMusicManager>, IMusicManager
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GlobalMusicManager>().ForFeature("Music"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    protected override bool selfCreate => true;
    protected override bool dontDestroyOnLoad => true;

    private AudioSource audioSource1, audioSource2;
    private CancellationTokenSource currentCrossfadeCts;
    private AudioSource currentSource;
    private AudioSource nextSource;
    private ISettingsSystem settingsSystem;
    private long crossfadeGeneration;
    private float playbackTargetVolume = 1f;

    [SerializeField]
    private AudioClip defaultMusicClip;

    [SerializeField]
    private AudioMixerGroup musicMixerGroup;

    protected override void Awake()
    {
      base.Awake();

      // Initialize audio sources
      audioSource1 = gameObject.AddComponent<AudioSource>();
      audioSource2 = gameObject.AddComponent<AudioSource>();

      // Set default properties
      audioSource1.loop = true;
      audioSource2.loop = true;
      audioSource1.volume = 0f;
      audioSource2.volume = 0f;

      // Assign mixer group if configured
      if (musicMixerGroup != null)
      {
        audioSource1.outputAudioMixerGroup = musicMixerGroup;
        audioSource2.outputAudioMixerGroup = musicMixerGroup;
        log.Verbose("MusicManager assigned to mixer group: {MixerGroup}", musicMixerGroup.name);
      }
      else
      {
        log.Warning("MusicManager has no mixer group assigned - volume controls may not work");
      }

      currentSource = audioSource1;
      nextSource = audioSource2;
    }

    [Inject]
    public void Construct(ISettingsSystem settings) => settingsSystem = settings;

    private void Start()
    {
      if (defaultMusicClip != null)
      {
        log.ForMethod().ForMethod().Verbose("Playing default music clip: {clipName}", defaultMusicClip.name);
        StartDefaultMusicAsync(defaultMusicClip).Forget();
      }
      else
      {
        log.ForMethod().ForMethod().Verbose("No default music clip set");
      }
    }

    private async UniTask StartDefaultMusicAsync(AudioClip clip)
    {
      long startupGeneration = crossfadeGeneration;
      ISettingsSystem settings = settingsSystem;
      if (settings == null && GlobalInstaller.Instance != null && GlobalInstaller.Instance.Container != null)
        GlobalInstaller.Instance.Container.TryResolve<ISettingsSystem>(out settings);

      if (settings == null)
      {
        log.ForMethod().Warning("Settings are unavailable; default music startup is deferred");
        return;
      }

      await settings.Initialization;
      if (startupGeneration != crossfadeGeneration) return;
      ApplyLoadedMixerSettings(settings);
      if (startupGeneration != crossfadeGeneration) return;
      await PlayMusicAsync(clip, 2f);
    }

    private void ApplyLoadedMixerSettings(ISettingsSystem settings)
    {
      AudioMixer mixer = musicMixerGroup?.audioMixer;
      if (settings?.AudioSettings == null)
        return;

      playbackTargetVolume = settings.AudioSettings.MasterVolume.Value *
                             settings.AudioSettings.MusicVolume.Value;
      if (mixer == null)
        return;

      bool masterApplied = SetMixerVolume(mixer, "MasterVolume", settings.AudioSettings.MasterVolume.Value);
      bool musicApplied = SetMixerVolume(mixer, "MusicVolume", settings.AudioSettings.MusicVolume.Value);
      if (masterApplied && musicApplied)
        playbackTargetVolume = 1f;
    }

    private static bool SetMixerVolume(AudioMixer mixer, string parameter, float volume) =>
      mixer.SetFloat(parameter, volume > 0f ? Mathf.Log10(volume) * 20f : -80f);

    protected override void OnDestroy()
    {
      // Cancel any ongoing crossfade
      currentCrossfadeCts?.Cancel();
      currentCrossfadeCts = null;
      crossfadeGeneration++;

      base.OnDestroy();
    }

    #region IMusicManager Implementation

    /// <summary>
    ///   Plays music with optional crossfade duration. Returns immediately.
    /// </summary>
    /// <param name="audioClip">The audio clip to play</param>
    /// <param name="duration">Crossfade duration in seconds (default: 2f)</param>
    public void PlayMusic(AudioClip audioClip, float duration = 2f)
    {
      PlayMusicInternal(audioClip, duration);
    }

    /// <summary>
    ///   Pauses the currently playing music.
    /// </summary>
    public void Pause()
    {
      PauseInternal();
    }

    /// <summary>
    ///   Resumes the paused music.
    /// </summary>
    public void Resume()
    {
      ResumeInternal();
    }

    /// <summary>
    ///   Stops the currently playing music and cancels any ongoing crossfade.
    /// </summary>
    public void Stop()
    {
      StopInternal();
    }

    /// <summary>
    ///   Gets whether music is currently playing.
    /// </summary>
    public bool IsPlaying => isPlayingInternal;

    /// <summary>
    ///   Gets whether music is currently paused.
    /// </summary>
    public bool IsPaused => isPausedInternal;

    #endregion

    #region Instance Implementation

    private void PlayMusicInternal(AudioClip audioClip, float duration)
    {
      PlayMusicAsync(audioClip, duration).Forget();
    }

    private async UniTask PlayMusicAsync(AudioClip audioClip, float duration)
    {
      if (audioClip == null)
      {
        log.Warning("Attempted to play null AudioClip");
        return;
      }

      currentCrossfadeCts?.Cancel();
      var operationCts = new CancellationTokenSource();
      currentCrossfadeCts = operationCts;
      long generation = ++crossfadeGeneration;
      CancellationToken ct = operationCts.Token;

      // If nothing is playing, just play the new clip
      if (!currentSource.isPlaying && !nextSource.isPlaying)
      {
        currentSource.clip = audioClip;
        currentSource.volume = playbackTargetVolume;
        currentSource.Play();
        if (generation == crossfadeGeneration)
          nextSource.volume = 0f;
        if (ReferenceEquals(currentCrossfadeCts, operationCts))
          currentCrossfadeCts = null;
        operationCts.Dispose();
        return;
      }

      // Determine which source is currently active
      AudioSource activeSource = currentSource.isPlaying ? currentSource : nextSource;
      AudioSource inactiveSource = activeSource == currentSource ? nextSource : currentSource;

      // Setup the new clip on the inactive source
      inactiveSource.Stop();
      inactiveSource.clip = audioClip;
      inactiveSource.volume = 0f;
      inactiveSource.Play();

      try
      {
        // Start crossfade
        await CrossfadeAsync(activeSource, inactiveSource, duration, ct);

        if (generation == crossfadeGeneration)
        {
          currentSource = inactiveSource;
          nextSource = activeSource;
        }
      }
      catch (OperationCanceledException)
      {
        // Crossfade was cancelled - handle gracefully
      }
      finally
      {
        if (ReferenceEquals(currentCrossfadeCts, operationCts))
          currentCrossfadeCts = null;
        operationCts.Dispose();
      }
    }

    private async UniTask CrossfadeAsync(AudioSource fadeOut, AudioSource fadeIn, float duration, CancellationToken ct)
    {
      float elapsed = 0f;
      float fadeOutStartVolume = fadeOut.volume;
      float fadeInTargetVolume = playbackTargetVolume;

      while (elapsed < duration && !ct.IsCancellationRequested)
      {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        fadeOut.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);
        fadeIn.volume = Mathf.Lerp(0f, fadeInTargetVolume, t);

        await UniTask.DelayFrame(1, cancellationToken: ct);
      }

      // Ensure final values
      if (!ct.IsCancellationRequested)
      {
        fadeOut.volume = 0f;
        fadeIn.volume = fadeInTargetVolume;
        fadeOut.Stop();
      }
    }

    private void PauseInternal()
    {
      if (currentSource.isPlaying)
      {
        currentSource.Pause();
        log.Information("Music paused");
      }
      if (nextSource.isPlaying)
        nextSource.Pause();
    }

    private void ResumeInternal()
    {
      if (currentSource.time > 0f)
      {
        currentSource.UnPause();
        log.Information("Music resumed");
      }
      if (nextSource.time > 0f)
        nextSource.UnPause();
    }

    private void StopInternal()
    {
      // Cancel any ongoing crossfade
      if (currentCrossfadeCts != null)
      {
        currentCrossfadeCts.Cancel();
        currentCrossfadeCts = null;
      }

      crossfadeGeneration++;

      if (currentSource.isPlaying)
        currentSource.Stop();
      if (nextSource.isPlaying)
        nextSource.Stop();
      currentSource.volume = 0f;
      nextSource.volume = 0f;

      log.Information("Music stopped");
    }

    private bool isPlayingInternal => currentSource.isPlaying || nextSource.isPlaying;

    private bool isPausedInternal => (currentSource.time > 0f && !currentSource.isPlaying) || (nextSource.time > 0f && !nextSource.isPlaying);

    // Narrow deterministic seams for package PlayMode regression tests.
    public void InitializeForTests(AudioSource first, AudioSource second, ISettingsSystem settings)
    {
      audioSource1 = first;
      audioSource2 = second;
      currentSource = first;
      nextSource = second;
      settingsSystem = settings;
    }

    public UniTask StartDefaultMusicAsyncForTests(AudioClip clip) => StartDefaultMusicAsync(clip);
    public UniTask PlayMusicAsyncForTests(AudioClip clip, float duration) => PlayMusicAsync(clip, duration);

    #endregion
  }
}
