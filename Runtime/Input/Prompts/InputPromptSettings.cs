using System;
using UnityEngine;

namespace MToolKit.Runtime.Input.Prompts
{
  [CreateAssetMenu(fileName = "InputPromptSettings", menuName = "MToolKit/Input/Prompt Settings")]
  public sealed class InputPromptSettings : ScriptableObject
  {
    [SerializeField] private InputGlyphCatalog catalog;
    [SerializeField] private InputPromptStyle style;
    [SerializeField] private bool overrideFamily;
    [SerializeField] private InputGlyphFamily family;
    [Tooltip("Optional explicit binding group for previews. This selects bindings; family only changes their appearance.")]
    [SerializeField] private string bindingGroup;
    [Tooltip("Unity Localization collection containing semantic input key names and sentence templates.")]
    [SerializeField] private string localizationTable = "";
    public InputGlyphCatalog Catalog => catalog;
    public InputPromptStyle Style => style;
    public bool OverrideFamily => overrideFamily;
    public InputGlyphFamily Family => family;
    public string BindingGroup => bindingGroup;
    public string LocalizationTable => localizationTable;
    public event Action Changed;

    public void Configure(InputGlyphCatalog glyphCatalog, InputPromptStyle promptStyle = InputPromptStyle.Auto,
      InputGlyphFamily? familyOverride = null, string selectedBindingGroup = null, string table = null)
    {
      catalog = glyphCatalog;
      style = promptStyle;
      overrideFamily = familyOverride.HasValue;
      family = familyOverride ?? InputGlyphFamily.KeyboardAndMouse;
      bindingGroup = selectedBindingGroup;
      if (table != null) localizationTable = table;
      NotifyChanged();
    }

    public void NotifyChanged() => Changed?.Invoke();
    private void OnValidate() => NotifyChanged();
  }
}
