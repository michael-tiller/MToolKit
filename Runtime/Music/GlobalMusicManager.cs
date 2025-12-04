using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Utilities;
using Serilog;
using UnityEngine;
using UnityEngine.Audio;
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
        log.Debug("MusicManager assigned to mixer group: {MixerGroup}", musicMixerGroup.name);
      }
      else
      {
        log.Warning("MusicManager has no mixer group assigned - volume controls may not work");
      }

      currentSource = audioSource1;
      nextSource = audioSource2;
    }

    private void Start()
    {
      if (defaultMusicClip != null)
      {
        log.ForMethod().ForMethod().Debug("Playing default music clip: {clipName}", defaultMusicClip.name);
        PlayMusic(defaultMusicClip);
      }
      else
      {
        log.ForMethod().ForMethod().Verbose("No default music clip set");
      }
    }

    protected override void OnDestroy()
    {
      // Cancel any ongoing crossfade
      currentCrossfadeCts?.Cancel();
      currentCrossfadeCts?.Dispose();
      currentCrossfadeCts = null;

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

      // Cancel any ongoing crossfade
      if (currentCrossfadeCts != null)
      {
        currentCrossfadeCts.Cancel();
        currentCrossfadeCts.Dispose();
      }

      // Create new cancellation token source
      currentCrossfadeCts = new CancellationTokenSource();
      CancellationToken ct = currentCrossfadeCts.Token;

      // If nothing is playing, just play the new clip
      if (!currentSource.isPlaying && !nextSource.isPlaying)
      {
        currentSource.clip = audioClip;
        currentSource.volume = 1f;
        currentSource.Play();
        currentCrossfadeCts = null;
        return;
      }

      // Determine which source is currently active
      AudioSource activeSource = currentSource.isPlaying ? currentSource : nextSource;
      AudioSource inactiveSource = activeSource == currentSource ? nextSource : currentSource;

      // Setup the new clip on the inactive source
      inactiveSource.clip = audioClip;
      inactiveSource.volume = 0f;
      inactiveSource.Play();

      try
      {
        // Start crossfade
        await CrossfadeAsync(activeSource, inactiveSource, duration, ct);

        // Swap references for next time
        (currentSource, nextSource) = (nextSource, currentSource);
      }
      catch (OperationCanceledException)
      {
        // Crossfade was cancelled - handle gracefully
      }
      finally
      {
        currentCrossfadeCts?.Dispose();
        currentCrossfadeCts = null;
      }
    }

    private async UniTask CrossfadeAsync(AudioSource fadeOut, AudioSource fadeIn, float duration, CancellationToken ct)
    {
      float elapsed = 0f;
      float fadeOutStartVolume = fadeOut.volume;
      float fadeInTargetVolume = 1f;

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
        currentCrossfadeCts.Dispose();
        currentCrossfadeCts = null;
      }

      if (currentSource.isPlaying)
        currentSource.Stop();
      if (nextSource.isPlaying)
        nextSource.Stop();

      log.Information("Music stopped");
    }

    private bool isPlayingInternal => currentSource.isPlaying || nextSource.isPlaying;

    private bool isPausedInternal => (currentSource.time > 0f && !currentSource.isPlaying) || (nextSource.time > 0f && !nextSource.isPlaying);

    #endregion
  }
}