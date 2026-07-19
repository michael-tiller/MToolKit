using System;
using System.Collections.Generic;
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
    [Tooltip("Play the open sound automatically when this GameObject is enabled. Disable when visibility is managed without activation, such as by a CanvasGroup.")]
    private bool playOnEnable = true;

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [Required]
    [ValidateInput("@openAudioClips != null && openAudioClips.Count > 0", "At least one panel-open audio clip is required")]
    private List<AudioClip> openAudioClips = new();

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [Required]
    private List<AudioClip> closeAudioClips = new();

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for the panel-open sound")]
    private float openVolume = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0.5f, 2f)]
    [Tooltip("Pitch for the panel-open sound")]
    private float openPitch = 1f;

    [SerializeField, Range(0f, 1f)] private float closeVolume = 1f;
    [SerializeField, Range(0.5f, 2f)] private float closePitch = 1f;

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

      if (audioService == null || openAudioClips == null || openAudioClips.Count == 0)
        return;

      try
      {
        audioService.PlayOneShot(openAudioClips, volume: openVolume, pitch: openPitch, audioType: EAudioTypes.Interface);
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
      if (audioService == null || closeAudioClips == null || closeAudioClips.Count == 0)
        return;
      try
      {
        audioService.PlayOneShot(closeAudioClips, volume: closeVolume, pitch: closePitch, audioType: EAudioTypes.Interface);
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
      if (Application.isPlaying && openAudioClips is {Count: > 0})
        PlayOpenSound();
      else
        log.ForMethod().Warning("Cannot test panel-open sound - not in play mode or no clips assigned");
    }
  }
}
