using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using InputKey = UnityEngine.InputSystem.Key;

namespace MToolKit.Editor.Input
{
  public enum InputKeycapWidthRule { FitLabel = 0, Fixed = 1 }
  public enum InputKeycapImageMode { Simple = 0, Sliced = 1 }
  public enum InputKeycapLocaleSource { ProfileLocales = 0, ProjectEnabled = 1 }

  [CreateAssetMenu(fileName = "InputKeycapBakeProfile", menuName = "MToolKit/Input Glyphs/Keycap Bake Profile")]
  public sealed class InputKeycapBakeProfile : ScriptableObject
  {
    [Serializable]
    public sealed class LocaleFontOverride
    {
      public string LocaleCode;
      public TMP_FontAsset Font;
    }
    [Serializable]
    public sealed class Key
    {
      public string ControlPath;
      public string SpriteName;
      public string Label;
      public string LocalizationKey;
      [Tooltip("Any preserves platform-independent behavior. OS-specific variants need distinct semantic sprite names.")]
      public InputGlyphPlatform Platform;
      [Tooltip("Label renders text; Sprite renders neutral artwork; SpriteAndLabel puts artwork and its localized name inside one cap.")]
      public InputKeycapContentMode ContentMode;
      [Tooltip("Optional central artwork. Sprite mode keeps a readable label until artwork is assigned.")]
      public Sprite ContentSprite;
      [Tooltip("Maximum central artwork size in baked pixels. Native Image preserves the sprite aspect ratio.")]
      public Vector2 SpriteSize = new(32, 32);
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
    [Min(0), Tooltip("Native horizontal gap between the icon and localized text in SpriteAndLabel caps.")]
    public int SpriteLabelSpacing = 6;
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
    [Tooltip("ProjectEnabled follows Unity Localization's enabled project locales. ProfileLocales uses only the manual list.")]
    public InputKeycapLocaleSource LocaleSource;
    [Tooltip("Used only when Locale Source is ProfileLocales. Ignored by ProjectEnabled.")]
    public string[] Locales = { "en" };
    public LocaleFontOverride[] LocaleFontOverrides = Array.Empty<LocaleFontOverride>();
    public Key[] Keys;
    [SerializeField, HideInInspector] private string outputOwner;
    [SerializeField, HideInInspector] private TMP_SpriteAsset[] bakedAssets = Array.Empty<TMP_SpriteAsset>();

    internal IReadOnlyList<TMP_SpriteAsset> PreviousAssets(string owner)
      => string.Equals(owner, outputOwner, StringComparison.Ordinal) ? bakedAssets ?? Array.Empty<TMP_SpriteAsset>() : Array.Empty<TMP_SpriteAsset>();

    internal void RememberAssets(string owner, TMP_SpriteAsset[] assets)
    { outputOwner = owner; bakedAssets = assets; }

    private void OnEnable()
    {
      // Input System may initialize Unity objects while loading layout metadata. Unity
      // forbids that from a ScriptableObject field initializer, but allows it here.
      if (Keys == null) Keys = CreateStandardKeys();
    }

    /// <summary>Adds missing installed physical keys without replacing authored rows or presentation settings.</summary>
    public bool EnsureStandardKeys()
    {
      var existing = Keys ?? Array.Empty<Key>();
      var paths = new HashSet<string>(existing.Where(key => key != null && key.Platform == InputGlyphPlatform.Any
          && !string.IsNullOrEmpty(key.ControlPath))
        .Select(key => key.ControlPath), StringComparer.OrdinalIgnoreCase);
      var missing = CreateStandardKeys().Where(key => paths.Add(key.ControlPath)).ToArray();
      if (missing.Length == 0) return false;
      Keys = existing.Concat(missing).ToArray();
      EditorUtility.SetDirty(this);
      return true;
    }

    public static Key[] CreateStandardKeys()
    {
      var keys = new List<Key>();
      for (char c = 'a'; c <= 'z'; c++) keys.Add(new Key { ControlPath = c.ToString(), SpriteName = "keycap-" + c, Label = char.ToUpperInvariant(c).ToString() });
      for (char c = '0'; c <= '9'; c++) keys.Add(new Key { ControlPath = c.ToString(), SpriteName = "keycap-" + c, Label = c.ToString() });
      Add("enter", "Enter", 112); Add("backspace", "Backspace", 0); Add("space", "Space", 160);
      Add("leftShift", "LShift", 112); Add("rightShift", "RShift", 112);
      Add("tab", "Tab", 96); Add("escape", "Escape", 0);
      Add("leftCtrl", "LCtrl", 96); Add("rightCtrl", "RCtrl", 96);
      Add("leftAlt", "LAlt", 96); Add("rightAlt", "RAlt", 96);
      Add("upArrow", "Up Arrow", 0); Add("downArrow", "Down Arrow", 0);
      Add("leftArrow", "Left Arrow", 0); Add("rightArrow", "Right Arrow", 0);
      // Read the installed layout rather than creating a device or truncating extended keys.
      var layout = InputSystem.LoadLayout<Keyboard>();
      if (layout == null) throw new InvalidOperationException("The Input System Keyboard layout must be registered before creating keycap defaults.");
      var existing = new HashSet<string>(keys.Select(key => key.ControlPath), StringComparer.OrdinalIgnoreCase);
      var punctuation = new HashSet<InputKey> {
        InputKey.Backquote, InputKey.Quote, InputKey.Semicolon, InputKey.Comma, InputKey.Period,
        InputKey.Slash, InputKey.Backslash, InputKey.LeftBracket, InputKey.RightBracket, InputKey.Minus, InputKey.Equals
      };
      foreach (var control in layout.controls.Where(item => !item.isSynthetic
          && string.Equals(item.layout.ToString(), "Key", StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.bit))
      {
        var keyCode = (InputKey)control.bit;
        if (keyCode == InputKey.None || !Enum.IsDefined(typeof(InputKey), keyCode)) continue;
        string controlPath = control.name.ToString();
        if (!existing.Add(controlPath)) continue;
        string semantic = System.Text.RegularExpressions.Regex.Replace(controlPath, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
        keys.Add(new Key {
          ControlPath = controlPath, SpriteName = "keycap-" + semantic,
          Label = string.IsNullOrEmpty(control.displayName) ? controlPath : control.displayName,
          LocalizationKey = punctuation.Contains(keyCode) ? null : "input.key." + controlPath,
          WidthRule = InputKeycapWidthRule.FitLabel
        });
      }
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
