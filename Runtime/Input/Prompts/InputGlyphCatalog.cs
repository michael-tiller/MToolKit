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
      public InputGlyphEntryKind Kind;
      public string DeviceLayout;
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

    /// <summary>Native layout specificity precedes platform and locale fallback; authored order breaks equal matches.</summary>
    public bool TryGetDeviceEntry(string deviceLayout, string localeCode, InputGlyphPlatform platform, out Entry entry)
    {
      for (string layout = deviceLayout; !string.IsNullOrEmpty(layout); layout = InputSystem.GetNameOfBaseLayout(layout))
        foreach (var candidatePlatform in PlatformFallbacks(platform))
          foreach (string locale in LocaleFallbacks(localeCode))
            foreach (var candidate in entries)
              if (candidate != null && candidate.Kind == InputGlyphEntryKind.Device
                && string.Equals(candidate.DeviceLayout, layout, StringComparison.OrdinalIgnoreCase)
                && candidate.Platform == candidatePlatform
                && string.Equals(NormalizeLocale(candidate.LocaleCode), locale, StringComparison.OrdinalIgnoreCase))
              { entry = candidate; return true; }
      entry = null;
      return false;
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
            if (candidate != null && candidate.Kind == InputGlyphEntryKind.Binding
              && candidate.Family == InputGlyphFamily.KeyboardAndMouse
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
        if (candidate != null && candidate.Kind == InputGlyphEntryKind.Binding
          && candidate.Family == InputGlyphFamily.KeyboardAndMouse && candidate.Platform == platform
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
          if (candidate != null && candidate.Kind == InputGlyphEntryKind.Binding
            && candidate.Family == family && candidate.Platform == platform
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
      => GetOriginalSprite(family, controlPath, platform)
        ?? (family != InputGlyphFamily.KeyboardAndMouse && family != InputGlyphFamily.Generic
          ? GetOriginalSprite(InputGlyphFamily.Generic, controlPath, platform) : null);

    // Legacy adapters can insert their still-valid original-family artwork before the Generic stage.
    internal Sprite GetOriginalSprite(InputGlyphFamily family, string controlPath, InputGlyphPlatform platform)
    {
      if (!TryGetEntry(family, controlPath, null, platform, out var entry)) return null;
      if (entry.ImageSprite != null) return entry.ImageSprite;
      if (entry.SpriteAsset == null || string.IsNullOrEmpty(entry.SpriteName)) return null;
      int index = entry.SpriteAsset.GetSpriteIndexFromName(entry.SpriteName);
      return index < 0 || index >= entry.SpriteAsset.spriteCharacterTable.Count ? null
        : (entry.SpriteAsset.spriteCharacterTable[index]?.glyph as TMP_SpriteGlyph)?.sprite;
    }

    internal static bool HasUsableTmpSprite(Entry entry)
    {
      var asset = entry?.SpriteAsset;
      if (asset == null || asset.material == null || asset.spriteSheet == null
        || asset.material.mainTexture != asset.spriteSheet || string.IsNullOrEmpty(entry.SpriteName)) return false;
      int index = asset.GetSpriteIndexFromName(entry.SpriteName);
      if (index < 0 || index >= asset.spriteCharacterTable.Count) return false;
      var character = asset.spriteCharacterTable[index];
      var glyph = character?.glyph as TMP_SpriteGlyph;
      if (glyph == null || glyph.index != character.glyphIndex || !asset.spriteGlyphTable.Contains(glyph)) return false;
      var rect = glyph.glyphRect;
      return rect.width > 0 && rect.height > 0 && rect.x >= 0 && rect.y >= 0
        && rect.x + rect.width <= asset.spriteSheet.width && rect.y + rect.height <= asset.spriteSheet.height
        && glyph.metrics.width > 0 && glyph.metrics.height > 0;
    }

    public Sprite GetSprite(InputGlyphFamily family, string controlPath)
      => GetSprite(family, controlPath, InputGlyphPlatform.Any);
  }
}
