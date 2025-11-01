using System;
using UnityEngine.UI;

namespace MToolKit.Runtime.Settings.Interfaces
{
  public interface ISettingsToggle
  {
    Toggle Toggle  { get; }
    bool Value { get; set; }

    event Action<bool> OnValueChanged;
    void ConfigureToggle(string toggleName, bool initialState);
    void UpdateToggleValue(bool newValue);
  }
}