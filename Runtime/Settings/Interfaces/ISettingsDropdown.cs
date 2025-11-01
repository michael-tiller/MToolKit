using System;
using System.Collections.Generic;
using TMPro;

namespace MToolKit.Runtime.Settings.Interfaces
{
  public interface ISettingsDropdown : ISettingsElement
  {
    void ConfigureDropdown(string dropdownName, int initialIndex);
    void SetOptions(IEnumerable<string> options);
    int Value { get; set; }
    TMP_Dropdown Dropdown { get; }
    event Action<int> OnValueChanged;
  }
}