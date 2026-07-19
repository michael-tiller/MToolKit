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
  [RequireComponent(typeof(Scrollbar))]
  public class AudioScrollbarComponent : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioScrollbarComponent>().ForFeature("Components"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [BoxGroup("Audio Clips")]
    [SerializeField]
    [Required]
    [ValidateInput("@moveAudioClips != null && moveAudioClips.Count > 0", "At least one movement audio clip is required")]
    private List<AudioClip> moveAudioClips = new();

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume multiplier for scrollbar movement sounds")]
    private float moveVolume = 1f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [Range(0.5f, 2f)]
    [Tooltip("Pitch for scrollbar movement sounds")]
    private float movePitch = 1f;

    [BoxGroup("Cooldown Settings")]
    [SerializeField]
    [MinValue(0f)]
    [Tooltip("Minimum time between movement sounds to prevent spam")]
    private float moveCooldown = 0.05f;

    [SerializeField]
    [Required]
    private Scrollbar scrollbar;

    private IAudioService audioService;
    private float lastMoveTime = float.NegativeInfinity;
    private bool pointerActive;

    private void Reset()
    {
      scrollbar = GetComponent<Scrollbar>();
    }

    private void Start()
    {
      if (scrollbar == null)
      {
        log.ForMethod().Error("Scrollbar component not found on {0}", gameObject.name);
        return;
      }

      TryResolveAudioService();
      if (audioService == null)
      {
        log.ForMethod().Error("IAudioService could not be resolved for AudioScrollbarComponent on {0}. Make sure AudioPlugin is loaded.", gameObject.name);
        return;
      }

      log.ForMethod().Verbose("AudioScrollbarComponent initialized for {0}", gameObject.name);
    }

    private void OnEnable()
    {
      if (scrollbar == null)
        scrollbar = GetComponent<Scrollbar>();
      scrollbar?.onValueChanged.RemoveListener(OnValueChanged);
      scrollbar?.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
      pointerActive = false;
      if (scrollbar != null)
        scrollbar.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void TryResolveAudioService()
    {
      try
      {
        if (GlobalInstaller.Instance != null)
        {
          audioService = GlobalInstaller.Instance.Container.Resolve<IAudioService>();
          return;
        }

        log.ForMethod().Warning("GlobalInstaller.Instance is null for {0}", gameObject.name);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to resolve IAudioService for {0}", gameObject.name);
      }
    }

    private void OnValueChanged(float value)
    {
      if (!pointerActive || !IsVisibleAndInteractable())
        return;

      if (audioService == null || moveAudioClips == null || moveAudioClips.Count == 0)
        return;

      if (Time.time - lastMoveTime < moveCooldown)
        return;

      try
      {
        audioService.PlayOneShot(moveAudioClips, volume: moveVolume, pitch: movePitch, audioType: EAudioTypes.Interface);
        lastMoveTime = Time.time;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to play scrollbar movement sound on {0}", gameObject.name);
      }
    }

    public void OnPointerDown(PointerEventData eventData) => pointerActive = true;
    public void OnPointerUp(PointerEventData eventData) => pointerActive = false;

    private bool IsVisibleAndInteractable()
    {
      if (!isActiveAndEnabled || scrollbar == null || !scrollbar.IsActive() || !scrollbar.IsInteractable())
        return false;

      foreach (CanvasGroup group in GetComponentsInParent<CanvasGroup>(true))
      {
        if (group.alpha <= 0f || !group.interactable || !group.blocksRaycasts)
          return false;
        if (group.ignoreParentGroups)
          break;
      }
      return true;
    }

    [Button("Test Movement Sound")]
    [PropertyOrder(100)]
    private void TestMovementSound()
    {
      if (Application.isPlaying && moveAudioClips is {Count: > 0})
        OnValueChanged(scrollbar != null ? scrollbar.value : 0f);
      else
        log.ForMethod().Warning("Cannot test movement sound - not in play mode or no clips assigned");
    }
  }
}
