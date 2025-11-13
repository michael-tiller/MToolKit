using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Settings.Interfaces;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MToolKit.Runtime.Settings.UI.Abstract
{
  public abstract class AbstractSettingsDropdown : AbstractSettingsElement, ISettingsDropdown
  {
    [SerializeField]
    [Required]
    protected TMP_Dropdown dropdown;

    protected virtual void OnEnable()
    {
      dropdown.onValueChanged.AddListener(OnDropdownValueChangedHandler);
    }

    protected virtual void OnDisable()
    {
      dropdown.onValueChanged.RemoveListener(OnDropdownValueChangedHandler);
    }

    public TMP_Dropdown Dropdown => dropdown;

    public int Value
    {
      get => dropdown.value;
      set => UpdateDropdownValue(value);
    }

    public event Action<int> OnValueChanged;

    public virtual void ConfigureDropdown(string dropdownName, int initialIndex)
    {
      Name = dropdownName;
      UpdateDropdownValue(initialIndex);
    }

    public virtual void SetOptions(IEnumerable<string> options)
    {
      if (dropdown == null) return;
      dropdown.ClearOptions();
      dropdown.AddOptions(options.ToList());
    }

    public virtual void UpdateDropdownValue(int index)
    {
      if (dropdown.value == index) return;
      dropdown.value = index;
    }

    protected virtual void OnDropdownValueChangedHandler(int newIndex)
    {
      OnValueChanged?.Invoke(newIndex);
    }
  }
}