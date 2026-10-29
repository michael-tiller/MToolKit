using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using TMPro;
using UnityEngine;
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
      if (setting != null) setting.Value = UiScalePolicy.Default;
    }

    private void OnEnable() => Attach();

    private void Attach()
    {
      Detach();
      if (slider == null || setting?.Property == null || setting.Property.IsDisposed) return;
      slider.minValue = UiScalePolicy.Minimum * 100f;
      slider.maxValue = UiScalePolicy.Maximum * 100f;
      slider.wholeNumbers = true;
      if (label != null) label.text = setting.Name;
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
      if (setting != null) setting.Value = UiScalePolicy.Normalize(percent / 100f);
    }

    private void Detach()
    {
      subscription?.Dispose();
      subscription = null;
      if (slider != null) slider.onValueChanged.RemoveListener(ChangePreference);
    }

    private void OnDisable() => Detach();
  }
}
