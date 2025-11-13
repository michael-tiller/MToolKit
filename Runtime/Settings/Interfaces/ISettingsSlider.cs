using System;
using UnityEngine.UI;

namespace MToolKit.Runtime.Settings.Interfaces
{
  public interface ISettingsSlider : ISettingsElement
  {
    float Value { get; set; }
    Slider Slider { get; }

    /// <summary>
    ///   This will configure the slider's label and set it's normalized value.
    /// </summary>
    /// <param name="sliderName"></param>
    /// <param name="normalizedValue"></param>
    void ConfigureSlider(string sliderName, float normalizedValue);

    void UpdateSliderValue(float normalizedValue);
    event Action<float> OnValueChanged;
  }
}