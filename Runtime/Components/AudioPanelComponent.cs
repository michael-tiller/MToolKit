using System;
using System.Collections.Generic;
using MToolKit.Runtime.Audio.Config;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Settings.Audio;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Components
{
  public class AudioPanelComponent : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioPanelComponent>().ForFeature("Components"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Tooltip("Reusable open/close sound package. When unset, legacy inline settings are used.")]
    [Required]
    private AudioPanelConfig audioConfig;

    [SerializeField]
    [Tooltip("Play the open sound automatically when this GameObject is enabled. Disable when visibility is managed without activation, such as by a CanvasGroup.")]
    private bool playOnEnable = true;

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [HideInInspector]
    [Required]
    [ValidateInput("@openAudioClips != null && openAudioClips.Count > 0", "At least one panel-open audio clip is required")]
    private List<AudioClip> openAudioClips = new();

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [HideInInspector]
    [Required]
    private List<AudioClip> closeAudioClips = new();

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [HideInInspector]
    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for the panel-open sound")]
    private float openVolume = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [HideInInspector]
    [Range(0.5f, 2f)]
    [Tooltip("Pitch for the panel-open sound")]
    private float openPitch = 1f;

    [SerializeField]
    [HideInInspector]
    [Range(0f, 1f)]
    private float closeVolume = 1f;

    [SerializeField]
    [HideInInspector]
    [Range(0.5f, 2f)]
    private float closePitch = 1f;

    private IAudioService audioService;

    private void OnEnable()
    {
      if (playOnEnable)
        PlayOpenSound();
    }

    public void PlayOpenSound()
    {
      if (audioService == null)
        TryResolveAudioService();

      AudioCueConfig cue = audioConfig != null ? audioConfig.open : null;
      IReadOnlyList<AudioClip> clips = cue?.clips ?? openAudioClips;
      if (audioService == null || (cue != null && !cue.enabled) || clips == null || clips.Count == 0)
        return;

      try
      {
        audioService.PlayOneShot(clips, volume: cue?.volume ?? openVolume, pitch: cue?.pitch ?? openPitch,
          audioType: cue?.audioType ?? EAudioTypes.Interface);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to play panel-open sound on {0}", gameObject.name);
      }
    }

    public void PlayCloseSound()
    {
      if (audioService == null)
        TryResolveAudioService();
      AudioCueConfig cue = audioConfig != null ? audioConfig.close : null;
      IReadOnlyList<AudioClip> clips = cue?.clips ?? closeAudioClips;
      if (audioService == null || (cue != null && !cue.enabled) || clips == null || clips.Count == 0)
        return;
      try
      {
        audioService.PlayOneShot(clips, volume: cue?.volume ?? closeVolume, pitch: cue?.pitch ?? closePitch,
          audioType: cue?.audioType ?? EAudioTypes.Interface);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to play panel-close sound on {0}", gameObject.name);
      }
    }

    private void TryResolveAudioService()
    {
      try
      {
        if (GlobalInstaller.Instance != null)
          audioService = GlobalInstaller.Instance.Container.Resolve<IAudioService>();
        else
          log.ForMethod().Warning("GlobalInstaller.Instance is null for {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to resolve IAudioService for {0}", gameObject.name);
      }
    }

    [Button("Test Panel Open Sound")]
    [PropertyOrder(100)]
    private void TestPanelOpenSound()
    {
      if (Application.isPlaying && (audioConfig?.open?.clips is {Count: > 0} || openAudioClips is {Count: > 0}))
        PlayOpenSound();
      else
        log.ForMethod().Warning("Cannot test panel-open sound - not in play mode or no clips assigned");
    }
  }
}
