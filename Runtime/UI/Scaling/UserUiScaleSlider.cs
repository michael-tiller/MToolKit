using System;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace MToolKit.Runtime.UI.Scaling
{
  /// <summary>Integer percentage positions provide 1% pointer, keyboard and controller steps.</summary>
  [DisallowMultipleComponent]
  public sealed class UserUiScaleSlider : MonoBehaviour
  {
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text percentage;
    [SerializeField] private TMP_Text label;
    private ReactiveSetting<float> setting;
    private IDisposable subscription;
    private LocalizedString localizedLabel;
    private bool revealPending;
    private int revealGeneration;

    [Inject]
    public void BindSettings(ISettingsSystem settings)
    {
      if (settings?.GraphicsSettings != null) Bind(settings.GraphicsSettings.UiScale);
    }

    public void Configure(Slider control, TMP_Text percentageText, ReactiveSetting<float> preference)
    {
      Detach();
      slider = control;
      percentage = percentageText;
      Bind(preference);
    }

    public void Bind(ReactiveSetting<float> preference)
    {
      Detach();
      setting = preference;
      if (isActiveAndEnabled) Attach();
    }

    public void ResetToDefault()
    {
      if (setting == null) return;
      setting.Value = UiScalePolicy.Default;
      QueueReveal();
    }

    private void OnEnable() => Attach();

    private void Attach()
    {
      Detach();
      if (slider == null || setting?.Property == null || setting.Property.IsDisposed) return;
      slider.minValue = UiScalePolicy.Minimum * 100f;
      slider.maxValue = UiScalePolicy.Maximum * 100f;
      slider.wholeNumbers = true;
      if (label != null)
      {
        if (!Application.isPlaying) label.text = setting.Name;
        else
        {
          localizedLabel = new LocalizedString("Default String Table", setting.Name);
          localizedLabel.StringChanged += UpdateLabel;
        }
      }
      subscription = setting.Property.Subscribe(UpdateControl);
      slider.onValueChanged.AddListener(ChangePreference);
    }

    private void UpdateControl(float value)
    {
      int percent = UiScalePolicy.Percentage(value);
      slider.SetValueWithoutNotify(percent);
      if (percentage != null) percentage.SetText("{0}%", percent);
    }

    private void ChangePreference(float percent)
    {
      if (setting == null) return;
      setting.Value = UiScalePolicy.Normalize(percent / 100f);
      QueueReveal();
    }

    private void UpdateLabel(string value)
    {
      if (label != null) label.text = value;
    }

    private void QueueReveal()
    {
      if (!Application.isPlaying || !isActiveAndEnabled || slider == null || !slider.isActiveAndEnabled
        || slider.GetComponentInParent<ScrollRect>() == null || revealPending) return;
      revealPending = true;
      RevealAfterLayoutAsync(revealGeneration).Forget();
    }

    private async UniTask RevealAfterLayoutAsync(int generation)
    {
      // CanvasScaler and layout finish the live preview before revealing this control once.
      bool canceled = await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate,
        this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
      if (canceled || this == null || generation != revealGeneration) return;
      revealPending = false;
      if (isActiveAndEnabled && slider != null && slider.isActiveAndEnabled
        && slider.GetComponentInParent<ScrollRect>() != null)
        ScrollIntoViewOnSelect.EnsureVisible(slider.transform as RectTransform);
    }

    private void Detach()
    {
      revealGeneration++;
      revealPending = false;
      if (localizedLabel != null) localizedLabel.StringChanged -= UpdateLabel;
      localizedLabel = null;
      subscription?.Dispose();
      subscription = null;
      if (slider != null) slider.onValueChanged.RemoveListener(ChangePreference);
    }

    private void OnDisable() => Detach();
  }
}
