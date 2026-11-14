using System;
using MToolKit.Runtime.Input.Prompts;
using UnityEngine;

namespace MToolKit.Editor.Input
{
  [CreateAssetMenu(fileName = "InputGlyphAtlasSource", menuName = "MToolKit/Input Glyphs/Atlas Source")]
  public sealed class InputGlyphAtlasSource : ScriptableObject
  {
    [Serializable]
    public sealed class Glyph
    {
      public InputGlyphEntryKind Kind;
      public string DeviceLayout;
      public string ControlPath;
      public string SpriteName;
      public Sprite SourceSprite;
      public string LocaleCode;
      public string BakedLabel;
      public InputKeycapContentMode KeycapContent;
      public InputGlyphPlatform Platform;
      public string Symbol;
      public string LocalizationKey;
      public string DisplayName;
    }
    public InputGlyphCatalog Catalog;
    public InputGlyphFamily Family;
    public string SpriteAssetPath = "Assets/InputGlyphs/MTK_InputGlyphs.asset";
    public Glyph[] Glyphs = Array.Empty<Glyph>();
  }
}
