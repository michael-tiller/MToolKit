using System;
using MToolKit.Runtime.Settings.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Runtime.Settings.UI.Abstract
{
  public abstract class AbstractSettingsToggle : AbstractSettingsElement, ISettingsToggle
  {
    [SerializeField]
    [Required]
    protected Toggle toggle;

    protected virtual void OnEnable()
    {
      if (toggle != null)
        toggle.onValueChanged.AddListener(OnToggleValueChangedHandler);
    }

    protected virtual void OnDisable()
    {
      if (toggle != null)
        toggle.onValueChanged.RemoveListener(OnToggleValueChangedHandler);
    }

    public Toggle Toggle => toggle;

    public bool Value
    {
      get => toggle.isOn;
      set => UpdateToggleValue(value);
    }

    public event Action<bool> OnValueChanged;

    public virtual void ConfigureToggle(string toggleName, bool initialState)
    {
      Name = toggleName;
      UpdateToggleValue(initialState);
    }

    public virtual void UpdateToggleValue(bool newValue)
    {
      if (toggle.isOn == newValue)
        return;
      toggle.isOn = newValue;
    }

    protected virtual void OnToggleValueChangedHandler(bool newValue)
    {
      OnValueChanged?.Invoke(newValue);
    }
  }
}