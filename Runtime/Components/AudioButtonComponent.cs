using System;
using System.Collections.Generic;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Settings.Audio;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Components
{
  [RequireComponent(typeof(Button))]
  public class AudioButtonComponent : MonoBehaviour, ISelectHandler, IDeselectHandler
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioButtonComponent>().ForFeature("Components"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [Required]
    [ValidateInput("@clickAudioClips != null && clickAudioClips.Count > 0", "At least one click audio clip is required")]
    private List<AudioClip> clickAudioClips = new();

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [Required]
    [ValidateInput("@selectAudioClips != null && selectAudioClips.Count > 0", "At least one select audio clip is required")]
    private List<AudioClip> selectAudioClips = new();

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for click sounds")]
    private float clickVolume = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for select sounds")]
    private float selectVolume = 0.8f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0.5f, 2f)]
    [Tooltip("Pitch variation for click sounds")]
    private float clickPitch = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0.5f, 2f)]
    [Tooltip("Pitch variation for select sounds")]
    private float selectPitch = 1f;

    [BoxGroup("Cooldown Settings")]
    [SerializeField]
    [MinValue(0f)]
    [Tooltip("Minimum time between click sounds to prevent spam")]
    private float clickCooldown = 0.1f;

    [BoxGroup("Cooldown Settings")]
    [SerializeField]
    [MinValue(0f)]
    [Tooltip("Minimum time between select sounds to prevent spam")]
    private float selectCooldown = 0.05f;

    [SerializeField]
    [Required]
    private Button button;

    private IAudioService audioService;

    private float lastClickTime;
    private float lastSelectTime;

    private void Reset()
    {
      button = GetComponent<Button>();
    }

    private void Start()
    {
      if (button == null)
      {
        log.ForMethod().Error("Button component not found on {0}", gameObject.name);
        return;
      }

      // Always try to resolve audioService from GlobalInstaller
      TryResolveAudioService();

      // Validate that audioService is available
      if (audioService == null)
      {
        log.ForMethod().Error("IAudioService could not be resolved for AudioButtonComponent on {0}. Make sure AudioPlugin is loaded.", gameObject.name);
        return;
      }

      button.onClick.AddListener(OnClick);
      log.ForMethod().Verbose("AudioButtonComponent initialized for {0}", gameObject.name);
    }

    private void OnDestroy()
    {
      if (button != null)
        button.onClick.RemoveListener(OnClick);
    }

    public void OnDeselect(BaseEventData eventData)
    {
      // Optional: Could play a deselect sound here if needed
      log.ForMethod().Verbose("Button deselected: {0}", gameObject.name);
    }

    public void OnSelect(BaseEventData eventData)
    {
      if (audioService == null)
      {
        log.ForMethod().Verbose("AudioService is null on {0}, cannot play select sound", gameObject.name);
        return;
      }

      if (Time.time - lastSelectTime < selectCooldown)
      {
        log.ForMethod().Debug("Select sound skipped due to cooldown on {0}", gameObject.name);
        return;
      }

      if (selectAudioClips == null || selectAudioClips.Count == 0)
      {
        log.ForMethod().Warning("No select audio clips assigned to {0}", gameObject.name);
        return;
      }

      try
      {
        audioService.PlayOneShot(selectAudioClips, volume: selectVolume, pitch: selectPitch, audioType: EAudioTypes.Interface);
        lastSelectTime = Time.time;
        log.ForMethod().Verbose("Select sound played on {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to play select sound on {0}", gameObject.name);
      }
    }

    private void TryResolveAudioService()
    {
      try
      {
        // Use GlobalInstaller.Instance to resolve IAudioService
        if (GlobalInstaller.Instance != null)
        {
          audioService = GlobalInstaller.Instance.Container.Resolve<IAudioService>();
          log.ForMethod().Verbose("Successfully resolved IAudioService from GlobalInstaller.Instance for {0}", gameObject.name);
          return;
        }

        log.ForMethod().Warning("GlobalInstaller.Instance is null for {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to resolve IAudioService for {0}", gameObject.name);
      }
    }

    private void OnClick()
    {
      if (audioService == null)
      {
        log.ForMethod().Warning("AudioService is null on {0}, cannot play click sound", gameObject.name);
        return;
      }

      if (Time.time - lastClickTime < clickCooldown)
      {
        log.ForMethod().Debug("Click sound skipped due to cooldown on {0}", gameObject.name);
        return;
      }

      if (clickAudioClips == null || clickAudioClips.Count == 0)
      {
        log.ForMethod().Warning("No click audio clips assigned to {0}", gameObject.name);
        return;
      }

      try
      {
        audioService.PlayOneShot(clickAudioClips, volume: clickVolume, pitch: clickPitch, audioType: EAudioTypes.Interface);
        lastClickTime = Time.time;
        log.ForMethod().Verbose("Click sound played on {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to play click sound on {0}", gameObject.name);
      }
    }

    [Button("Test Click Sound")]
    [PropertyOrder(100)]
    private void TestClickSound()
    {
      if (Application.isPlaying && clickAudioClips is {Count: > 0 })
        OnClick();
      else
        log.ForMethod().Warning("Cannot test click sound - not in play mode or no clips assigned");
    }

    [Button("Test Select Sound")]
    [PropertyOrder(101)]
    private void TestSelectSound()
    {
      if (Application.isPlaying && selectAudioClips is {Count: > 0 })
        OnSelect(null);
      else
        log.ForMethod().Warning("Cannot test select sound - not in play mode or no clips assigned");
    }
  }
}