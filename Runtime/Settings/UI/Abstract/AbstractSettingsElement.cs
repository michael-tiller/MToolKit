using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Settings.Interfaces;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MToolKit.Runtime.Settings.UI.Abstract
{
  public abstract class AbstractSettingsElement : MonoBehaviour, ISettingsElement
  {
    [SerializeField]
    [Required]
    protected TextMeshProUGUI label;

    public string Name
    {
      get => label.text;
      set => label.text = LocalizationHelper.GetLocalizedString(value);
    }
  }
}