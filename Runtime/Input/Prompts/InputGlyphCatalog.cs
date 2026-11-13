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
    }

    [Serializable]
    public sealed class LayoutRule
    {
      public string Layout;
      public InputGlyphFamily Family;
    }

    [SerializeField] private Entry[] entries = new Entry[0];
    [SerializeField] private LayoutRule[] layoutRules = new LayoutRule[0];
    public IReadOnlyList<Entry> Entries => entries;
    public IReadOnlyList<LayoutRule> LayoutRules => layoutRules;
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

    internal static bool IsLayout(string actual, string parent) => !string.IsNullOrEmpty(actual)
      && !string.IsNullOrEmpty(parent) && (string.Equals(actual, parent, StringComparison.OrdinalIgnoreCase)
        || InputSystem.IsFirstLayoutBasedOnSecond(actual, parent));

    public bool TryGetEntry(InputGlyphFamily family, string controlPath, out Entry entry)
    {
      foreach (var candidate in entries)
        if (candidate != null && candidate.Family == family
          && string.Equals(candidate.ControlPath, controlPath, StringComparison.OrdinalIgnoreCase))
        { entry = candidate; return true; }
      entry = null;
      return false;
    }

    /// <summary>Shared authoring supports native TMP and consumers that still render a UI Image.</summary>
    public Sprite GetSprite(InputGlyphFamily family, string controlPath)
    {
      if (!TryGetEntry(family, controlPath, out var entry)) return null;
      if (entry.ImageSprite != null) return entry.ImageSprite;
      if (entry.SpriteAsset == null || string.IsNullOrEmpty(entry.SpriteName)) return null;
      int index = entry.SpriteAsset.GetSpriteIndexFromName(entry.SpriteName);
      return index < 0 ? null : (entry.SpriteAsset.spriteCharacterTable[index].glyph as TMP_SpriteGlyph)?.sprite;
    }
  }
}
