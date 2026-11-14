using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Prompts
{
  [CreateAssetMenu(fileName = "InputGlyphCatalog", menuName = "MToolKit/Input/Input Glyph Catalog")]
  public sealed class InputGlyphCatalog : ScriptableObject
  {
    [Serializable]
    public sealed class Entry
    {
      public InputGlyphFamily Family;
      public string ControlPath;
      public TMP_SpriteAsset SpriteAsset;
      public string SpriteName;
      public string Symbol;
      public string LocalizationKey;
      public string DisplayName;
      public Sprite ImageSprite;
      [Tooltip("Optional locale identifier. Empty entries are independent of the selected language.")]
      public string LocaleCode;
      [Tooltip("Exact literal label baked into a generated keyboard cap. Empty for authored glyphs.")]
      public string BakedLabel;
      public InputKeycapContentMode KeycapContent;
      public InputGlyphPlatform Platform;
    }

    [Serializable]
    public sealed class LayoutRule
    {
      public string Layout;
      public InputGlyphFamily Family;
    }

    [SerializeField] private Entry[] entries = new Entry[0];
    [SerializeField] private LayoutRule[] layoutRules = new LayoutRule[0];
    [SerializeField] private InputKeyboardGlyphMode keyboardGlyphMode;
    public IReadOnlyList<Entry> Entries => entries;
    public IReadOnlyList<LayoutRule> LayoutRules => layoutRules;
    public InputKeyboardGlyphMode KeyboardGlyphMode => keyboardGlyphMode;
    public event Action Changed;

    public void SetEntries(params Entry[] values)
    {
      entries = values ?? new Entry[0];
      Changed?.Invoke();
    }

    public void SetLayoutRules(params LayoutRule[] values)
    {
      layoutRules = values ?? new LayoutRule[0];
      Changed?.Invoke();
    }

    public void ConfigureKeyboardGlyphs(InputKeyboardGlyphMode mode)
    {
      keyboardGlyphMode = mode;
      Changed?.Invoke();
    }

    public bool TryGetEntry(InputGlyphFamily family, string controlPath, string localeCode, out Entry entry)
      => TryGetEntry(family, controlPath, localeCode, InputGlyphPlatform.Any, out entry);

    public bool TryGetEntry(InputGlyphFamily family, string controlPath, string localeCode, InputGlyphPlatform platform, out Entry entry)
    {
      foreach (var candidatePlatform in PlatformFallbacks(platform))
        if (FindEntry(family, controlPath, localeCode, null, candidatePlatform, out entry)) return true;
      entry = null;
      return false;
    }

    public bool TryGetKeycapEntry(string controlPath, string localeCode, string bakedLabel, InputGlyphPlatform platform, out Entry entry)
    {
      foreach (var candidatePlatform in PlatformFallbacks(platform))
      {
        if (TryGetNeutralSprite(controlPath, candidatePlatform, out entry)) return true;
        if (!string.IsNullOrWhiteSpace(bakedLabel)
          && FindEntry(InputGlyphFamily.KeyboardAndMouse, controlPath, localeCode, bakedLabel, candidatePlatform, out entry))
          return true;
      }
      entry = null;
      return false;
    }

    internal bool TryGetKeycapDefinition(string controlPath, string localeCode, InputGlyphPlatform platform, out Entry entry)
    {
      foreach (var candidatePlatform in PlatformFallbacks(platform))
      {
        if (TryGetNeutralSprite(controlPath, candidatePlatform, out entry)) return true;
        foreach (string locale in LocaleFallbacks(localeCode))
          foreach (var candidate in entries)
            if (candidate != null && candidate.Family == InputGlyphFamily.KeyboardAndMouse
              && candidate.Platform == candidatePlatform
              && string.Equals(candidate.ControlPath, controlPath, StringComparison.OrdinalIgnoreCase)
              && string.Equals(NormalizeLocale(candidate.LocaleCode), locale, StringComparison.OrdinalIgnoreCase)
              && (candidate.KeycapContent == InputKeycapContentMode.SpriteAndLabel
                || candidate.KeycapContent == InputKeycapContentMode.Label && !string.IsNullOrEmpty(candidate.BakedLabel)))
            { entry = candidate; return true; }
      }
      entry = null;
      return false;
    }

    /// <summary>Prefers shared icon caps, then text caps whose baked label still matches the displayed key.</summary>
    public bool TryGetKeycapEntry(string controlPath, string localeCode, string bakedLabel, out Entry entry)
      => TryGetKeycapEntry(controlPath, localeCode, bakedLabel, InputGlyphPlatform.Any, out entry);

    internal bool TryGetKeycapDefinition(string controlPath, string localeCode, out Entry entry)
      => TryGetKeycapDefinition(controlPath, localeCode, InputGlyphPlatform.Any, out entry);

    private bool TryGetNeutralSprite(string controlPath, InputGlyphPlatform platform, out Entry entry)
    {
      foreach (var candidate in entries)
        if (candidate != null && candidate.Family == InputGlyphFamily.KeyboardAndMouse && candidate.Platform == platform
          && candidate.KeycapContent == InputKeycapContentMode.Sprite
          && string.Equals(candidate.ControlPath, controlPath, StringComparison.OrdinalIgnoreCase)
          && NormalizeLocale(candidate.LocaleCode).Length == 0)
        { entry = candidate; return true; }
      entry = null;
      return false;
    }

    private void OnValidate() => Changed?.Invoke();

    /// <summary>First matching authored rule wins, before the built-in layout families.</summary>
    public InputGlyphFamily ResolveFamily(string layout)
    {
      foreach (var rule in layoutRules)
        if (rule != null && IsLayout(layout, rule.Layout)) return rule.Family;
      return FamilyForLayout(layout);
    }

    public static InputGlyphFamily FamilyForLayout(string layout)
    {
      if (IsLayout(layout, "Keyboard") || IsLayout(layout, "Mouse")) return InputGlyphFamily.KeyboardAndMouse;
      if (IsLayout(layout, "DualShockGamepad") || IsLayout(layout, "DualSenseGamepadHID")) return InputGlyphFamily.PlayStation;
      if (IsLayout(layout, "SwitchProControllerHID") || IsLayout(layout, "SwitchProController")) return InputGlyphFamily.Nintendo;
      if (IsLayout(layout, "XInputController")) return InputGlyphFamily.Xbox;
      return InputGlyphFamily.Generic;
    }

    public static InputGlyphPlatform PlatformForOperatingSystem(OperatingSystemFamily family) => family switch
    {
      OperatingSystemFamily.Windows => InputGlyphPlatform.Windows,
      OperatingSystemFamily.MacOSX => InputGlyphPlatform.MacOS,
      OperatingSystemFamily.Linux => InputGlyphPlatform.Linux,
      _ => InputGlyphPlatform.Any
    };

    internal static bool IsLayout(string actual, string parent) => !string.IsNullOrEmpty(actual)
      && !string.IsNullOrEmpty(parent) && (string.Equals(actual, parent, StringComparison.OrdinalIgnoreCase)
        || InputSystem.IsFirstLayoutBasedOnSecond(actual, parent));

    public bool TryGetEntry(InputGlyphFamily family, string controlPath, out Entry entry)
      => TryGetEntry(family, controlPath, null, out entry);

    private bool FindEntry(InputGlyphFamily family, string controlPath, string localeCode, string bakedLabel, InputGlyphPlatform platform,
      out Entry entry)
    {
      foreach (string locale in LocaleFallbacks(localeCode))
        foreach (var candidate in entries)
          if (candidate != null && candidate.Family == family && candidate.Platform == platform
            && string.Equals(candidate.ControlPath, controlPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeLocale(candidate.LocaleCode), locale, StringComparison.OrdinalIgnoreCase)
            && (bakedLabel == null
              ? candidate.KeycapContent == InputKeycapContentMode.Label && string.IsNullOrEmpty(candidate.BakedLabel)
              : (candidate.KeycapContent == InputKeycapContentMode.Label
                  || candidate.KeycapContent == InputKeycapContentMode.SpriteAndLabel)
                && string.Equals(candidate.BakedLabel, bakedLabel, StringComparison.Ordinal)))
          { entry = candidate; return true; }
      entry = null;
      return false;
    }

    private static IEnumerable<string> LocaleFallbacks(string localeCode)
    {
      string locale = NormalizeLocale(localeCode);
      while (locale.Length > 0)
      {
        yield return locale;
        int separator = locale.LastIndexOf('-');
        locale = separator > 0 ? locale.Substring(0, separator) : string.Empty;
      }
      yield return string.Empty;
    }

    private static IEnumerable<InputGlyphPlatform> PlatformFallbacks(InputGlyphPlatform platform)
    {
      if (platform == InputGlyphPlatform.Windows || platform == InputGlyphPlatform.MacOS || platform == InputGlyphPlatform.Linux)
        yield return platform;
      yield return InputGlyphPlatform.Any;
    }

    private static string NormalizeLocale(string localeCode) => (localeCode ?? string.Empty).Trim().Replace('_', '-');

    /// <summary>Shared authoring supports native TMP and consumers that still render a UI Image.</summary>
    public Sprite GetSprite(InputGlyphFamily family, string controlPath, InputGlyphPlatform platform)
    {
      if (!TryGetEntry(family, controlPath, null, platform, out var entry)) return null;
      if (entry.ImageSprite != null) return entry.ImageSprite;
      if (entry.SpriteAsset == null || string.IsNullOrEmpty(entry.SpriteName)) return null;
      int index = entry.SpriteAsset.GetSpriteIndexFromName(entry.SpriteName);
      return index < 0 ? null : (entry.SpriteAsset.spriteCharacterTable[index].glyph as TMP_SpriteGlyph)?.sprite;
    }

    public Sprite GetSprite(InputGlyphFamily family, string controlPath)
      => GetSprite(family, controlPath, InputGlyphPlatform.Any);
  }
}
