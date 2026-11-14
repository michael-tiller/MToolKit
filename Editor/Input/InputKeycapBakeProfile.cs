using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor.Localization;
using UnityEngine;

namespace MToolKit.Editor.Input
{
  public enum InputKeycapWidthRule { FitLabel = 0, Fixed = 1 }
  public enum InputKeycapImageMode { Simple = 0, Sliced = 1 }

  [CreateAssetMenu(fileName = "InputKeycapBakeProfile", menuName = "MToolKit/Input Glyphs/Keycap Bake Profile")]
  public sealed class InputKeycapBakeProfile : ScriptableObject
  {
    [Serializable]
    public sealed class Key
    {
      public string ControlPath;
      public string SpriteName;
      public string Label;
      public string LocalizationKey;
      public InputKeycapWidthRule WidthRule;
      public int FixedWidth;
    }

    public InputGlyphCatalog Catalog;
    public StringTableCollection LocalizationCollection;
    public string SpriteAssetPath = "Assets/InputGlyphs/MTK_KeyboardKeycaps.asset";
    public TMP_FontAsset Font;
    [Min(1)] public int FontSize = 32;
    [Min(1)] public int SharedHeight = 64;
    [Min(1)] public int MinimumWidth = 64;
    [Min(0)] public int HorizontalPadding = 10;
    [Min(0)] public int VerticalPadding = 8;
    [Min(0)] public int BorderWidth = 2;
    public Sprite BorderSprite;
    public InputKeycapImageMode BorderImageMode;
    public Sprite BackgroundSprite;
    public InputKeycapImageMode BackgroundImageMode;
    [Tooltip("Optional UGUI prefab for any authored/procedural keycap background. No vendor dependency is required.")]
    public GameObject BackgroundPrefab;
    public Color32 BackgroundColor = new(28, 32, 40, 255);
    public Color32 BorderColor = new(112, 124, 144, 255);
    public Color32 TextColor = new(255, 255, 255, 255);
    public bool UniformCharacters = true;
    public string[] Locales = { "en" };
    public Key[] Keys = CreateStandardKeys();

    public static Key[] CreateStandardKeys()
    {
      var keys = new List<Key>();
      for (char c = 'a'; c <= 'z'; c++) keys.Add(new Key { ControlPath = c.ToString(), SpriteName = "keycap-" + c, Label = char.ToUpperInvariant(c).ToString() });
      for (char c = '0'; c <= '9'; c++) keys.Add(new Key { ControlPath = c.ToString(), SpriteName = "keycap-" + c, Label = c.ToString() });
      Add("enter", "Enter", 112); Add("backspace", "Backspace", 0); Add("space", "Space", 160);
      Add("leftShift", "Shift", 112); Add("rightShift", "Shift", 112);
      Add("tab", "Tab", 96); Add("escape", "Escape", 0);
      Add("leftCtrl", "Ctrl", 96); Add("rightCtrl", "Ctrl", 96);
      Add("leftAlt", "Alt", 96); Add("rightAlt", "Alt", 96);
      Add("upArrow", "Up Arrow", 0); Add("downArrow", "Down Arrow", 0);
      Add("leftArrow", "Left Arrow", 0); Add("rightArrow", "Right Arrow", 0);
      return keys.ToArray();
      void Add(string control, string label, int width, string localization = null)
      {
        var semantic = System.Text.RegularExpressions.Regex.Replace(control, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
        keys.Add(new Key { ControlPath = control, SpriteName = "keycap-" + semantic, Label = label,
          LocalizationKey = "input.key." + (localization ?? control), WidthRule = width > 0 ? InputKeycapWidthRule.Fixed : InputKeycapWidthRule.FitLabel, FixedWidth = width });
      }
    }
  }
}