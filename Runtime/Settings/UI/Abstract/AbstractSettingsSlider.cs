using System;
using MToolKit.Runtime.Settings.Interfaces;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Runtime.Settings.UI.Abstract
{
  public abstract class AbstractSettingsSlider : AbstractSettingsElement, ISettingsSlider
  {

    [SerializeField, Required] protected TextMeshProUGUI pct;
    [SerializeField, Required] protected Slider slider;
    
    public Slider Slider => slider;

    public event Action<float> OnValueChanged;
    public float Value { get => slider.value; set => UpdateSliderValue(value); }
    
    protected virtual void OnEnable()
    {
      slider.onValueChanged.AddListener(OnSliderValueChangedHandler);
      OnValueChanged -= UpdatePercentageText;
      OnValueChanged += UpdatePercentageText;
    }
    protected virtual void OnDisable()
    {
      slider.onValueChanged.RemoveListener(OnSliderValueChangedHandler);
      OnValueChanged -= UpdatePercentageText;
    }
    
    protected virtual void OnSliderValueChangedHandler(float value)
    {
      OnValueChanged?.Invoke(value);
    }
    
    public virtual void ConfigureSlider(string sliderName, float normalizedValue)
    {
      Name = sliderName;
      UpdateSliderValue(normalizedValue);
    }

    public virtual void UpdateSliderValue(float normalizedValue)
    {
      if (Mathf.Approximately(slider.value, normalizedValue)) return;
      
      slider.value = Mathf.Clamp01(normalizedValue);
      UpdatePercentageText(normalizedValue);
    }
    
    protected virtual void UpdatePercentageText(float value)
    {
      int percentage = Mathf.RoundToInt(value * 100f);
      pct.text = $"{percentage}%";
    }
  }

  
}