using System;
using System.Collections.Generic;
using UnityEngine;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Audio.Interface;
using Sirenix.OdinInspector;
using VContainer;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Installer;

namespace MToolKit.Runtime.Components
{
  [RequireComponent(typeof(Button))]
  public class AudioButtonComponent : MonoBehaviour, ISelectHandler, IDeselectHandler
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioButtonComponent>().ForFeature("Components"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private IAudioService audioService;

    [BoxGroup("Audio Clips")]
    [SerializeField, Required, ValidateInput("@clickAudioClips != null && clickAudioClips.Count > 0", "At least one click audio clip is required")]
    private List<AudioClip> clickAudioClips = new List<AudioClip>();

    [BoxGroup("Audio Clips")]
    [SerializeField, Required, ValidateInput("@selectAudioClips != null && selectAudioClips.Count > 0", "At least one select audio clip is required")]
    private List<AudioClip> selectAudioClips = new List<AudioClip>();

    [BoxGroup("Audio Settings")]
    [SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for click sounds")]
    private float clickVolume = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField, Range(0f, 1f), Tooltip("Volume multiplier for select sounds")]
    private float selectVolume = 0.8f;

    [BoxGroup("Audio Settings")]
    [SerializeField, Range(0.5f, 2f), Tooltip("Pitch variation for click sounds")]
    private float clickPitch = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField, Range(0.5f, 2f), Tooltip("Pitch variation for select sounds")]
    private float selectPitch = 1f;

    [BoxGroup("Cooldown Settings")]
    [SerializeField, MinValue(0f), Tooltip("Minimum time between click sounds to prevent spam")]
    private float clickCooldown = 0.1f;

    [BoxGroup("Cooldown Settings")]
    [SerializeField, MinValue(0f), Tooltip("Minimum time between select sounds to prevent spam")]
    private float selectCooldown = 0.05f;

    [SerializeField, Required]
    private Button button;

    private float lastClickTime = 0f;
    private float lastSelectTime = 0f;

    private void Reset()
    {
      button = GetComponent<Button>();
    }

    private void Start()
    {
      if (button == null)
      {
        log.ForMethod(nameof(Start)).Error("Button component not found on {0}", gameObject.name);
        return;
      }

      // Always try to resolve audioService from GlobalInstaller
      TryResolveAudioService();

      // Validate that audioService is available
      if (audioService == null)
      {
        log.ForMethod(nameof(Start)).Error("IAudioService could not be resolved for AudioButtonComponent on {0}. Make sure AudioPlugin is loaded.", gameObject.name);
        return;
      }

      button.onClick.AddListener(OnClick);
      log.ForMethod(nameof(Start)).Debug("AudioButtonComponent initialized for {0}", gameObject.name);
    }

    private void TryResolveAudioService()
    {
      try
      {
        // Use GlobalInstaller.Instance to resolve IAudioService
        if (GlobalInstaller.Instance != null)
        {
          audioService = GlobalInstaller.Instance.Container.Resolve<IAudioService>();
          log.ForMethod(nameof(TryResolveAudioService)).Debug("Successfully resolved IAudioService from GlobalInstaller.Instance for {0}", gameObject.name);
          return;
        }

        log.ForMethod(nameof(TryResolveAudioService)).Warning("GlobalInstaller.Instance is null for {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod(nameof(TryResolveAudioService)).Warning(ex, "Failed to resolve IAudioService for {0}", gameObject.name);
      }
    }

    private void OnDestroy()
    {
      if (button != null)
      {
        button.onClick.RemoveListener(OnClick);
      }
    }

    private void OnClick()
    {
      if (audioService == null)
      {
        log.ForMethod(nameof(OnClick)).Warning("AudioService is null on {0}, cannot play click sound", gameObject.name);
        return;
      }

      if (Time.time - lastClickTime < clickCooldown)
      {
        log.ForMethod(nameof(OnClick)).Debug("Click sound skipped due to cooldown on {0}", gameObject.name);
        return;
      }

      if (clickAudioClips == null || clickAudioClips.Count == 0)
      {
        log.ForMethod(nameof(OnClick)).Warning("No click audio clips assigned to {0}", gameObject.name);
        return;
      }

      try
      {
        audioService.PlayOneShot(clickAudioClips, volume: clickVolume, pitch: clickPitch, audioType: EAudioTypes.Interface);
        lastClickTime = Time.time;
        log.ForMethod(nameof(OnClick)).Debug("Click sound played on {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod(nameof(OnClick)).Error(ex, "Failed to play click sound on {0}", gameObject.name);
      }
    }

    public void OnSelect(BaseEventData eventData)
    {
      if (audioService == null)
      {
        log.ForMethod(nameof(OnSelect)).Verbose("AudioService is null on {0}, cannot play select sound", gameObject.name);
        return;
      }

      if (Time.time - lastSelectTime < selectCooldown)
      {
        log.ForMethod(nameof(OnSelect)).Debug("Select sound skipped due to cooldown on {0}", gameObject.name);
        return;
      }

      if (selectAudioClips == null || selectAudioClips.Count == 0)
      {
        log.ForMethod(nameof(OnSelect)).Warning("No select audio clips assigned to {0}", gameObject.name);
        return;
      }

      try
      {
        audioService.PlayOneShot(selectAudioClips, volume: selectVolume, pitch: selectPitch, audioType: EAudioTypes.Interface);
        lastSelectTime = Time.time;
        log.ForMethod(nameof(OnSelect)).Debug("Select sound played on {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod(nameof(OnSelect)).Error(ex, "Failed to play select sound on {0}", gameObject.name);
      }
    }

    public void OnDeselect(BaseEventData eventData)
    {
      // Optional: Could play a deselect sound here if needed
      log.ForMethod(nameof(OnDeselect)).Debug("Button deselected: {0}", gameObject.name);
    }

    [Button("Test Click Sound")]
    [PropertyOrder(100)]
    private void TestClickSound()
    {
      if (Application.isPlaying && clickAudioClips != null && clickAudioClips.Count > 0)
      {
        OnClick();
      }
      else
      {
        log.ForMethod(nameof(TestClickSound)).Warning("Cannot test click sound - not in play mode or no clips assigned");
      }
    }

    [Button("Test Select Sound")]
    [PropertyOrder(101)]
    private void TestSelectSound()
    {
      if (Application.isPlaying && selectAudioClips != null && selectAudioClips.Count > 0)
      {
        OnSelect(null);
      }
      else
      {
        log.ForMethod(nameof(TestSelectSound)).Warning("Cannot test select sound - not in play mode or no clips assigned");
      }
    }

  }
}