using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Editor.Input;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Samples.Editor
{
  /// <summary>Native, project-independent sample glyphs built through MTK's existing input authoring tools.</summary>
  public static class MenuGalleryGlyphAuthoring
  {
    public static InputPromptSettings Create(string outputRoot, TMP_FontAsset font)
    {
      if (font == null) throw new ArgumentNullException(nameof(font));
      string folder = outputRoot + "/Input/Glyphs";
      if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length > 0)
        throw new InvalidOperationException("Preserve existing glyph assets; rebuild their authored keycap/atlas sources instead: " + folder);
      InputGlyphSpriteAssetBuilder.EnsureFolder(folder);
      // TMP registers named sprite assets globally. The saved folder identity keeps fresh galleries distinct
      // while retaining the same atlas names when their existing authored recipes are rebaked.
      string identity = AssetDatabase.AssetPathToGUID(folder);
      if (!Guid.TryParseExact(identity, "N", out _))
        throw new InvalidOperationException("The glyph destination needs a persistent asset-folder identity: " + folder);
      var catalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
      AssetDatabase.CreateAsset(catalog, folder + "/MenuGlyphCatalog.asset");
      var keyboard = ScriptableObject.CreateInstance<InputKeycapBakeProfile>();
      keyboard.Catalog = catalog; keyboard.Font = font; keyboard.FontSize = 40;
      keyboard.SpriteAssetPath = folder + "/K" + identity + ".asset";
      keyboard.Locales = new[] { "en" };
      var required = new HashSet<string>(new[] { "enter", "escape", "tab", "backspace", "pageUp", "pageDown", "f1", "f2", "f8", "minus", "equals", "space", "leftShift", "leftCtrl", "upArrow", "downArrow", "leftArrow", "rightArrow" }, StringComparer.OrdinalIgnoreCase);
      keyboard.Keys = InputKeycapBakeProfile.CreateStandardKeys().Where(key => required.Contains(key.ControlPath)
        || key.ControlPath.Length == 1).ToArray();
      AssetDatabase.CreateAsset(keyboard, folder + "/KeyboardGlyphRecipe.asset");
      if (InputKeycapBaker.Bake(keyboard) == null) throw new InvalidOperationException("The sample keyboard glyph atlas was not generated.");
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);

      var sprites = new List<Sprite>();
      var names = new List<InputGlyphSpriteAssetBuilder.NamedSprite>();
      var rows = new List<(InputGlyphFamily family, string control, string semantic, string label)>();
      void Add(InputGlyphFamily family, string control, string semantic, string label, Sprite sprite)
      { sprites.Add(sprite); names.Add(new InputGlyphSpriteAssetBuilder.NamedSprite(semantic, sprite)); rows.Add((family, control, semantic, label)); }
      try
      {
        foreach (string control in new[] { "leftButton", "rightButton", "middleButton" })
          Add(InputGlyphFamily.KeyboardAndMouse, control, "menu-mouse-" + control.Replace("Button", "").ToLowerInvariant(), control,
            InputGlyphStarterArt.MouseButton(control));
        foreach (string control in new[] { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth" })
          Add(InputGlyphFamily.Generic, control, "menu-gamepad-" + control.Replace("button", "").ToLowerInvariant(), control,
            InputGlyphStarterArt.FacePosition(control));
        var controls = new Dictionary<string, string> { ["leftShoulder"] = "LB", ["rightShoulder"] = "RB",
          ["leftTrigger"] = "LT", ["rightTrigger"] = "RT", ["start"] = "Menu", ["select"] = "View", ["rightStickPress"] = "RS" };
        foreach (var pair in controls)
        {
          var texture = InputKeycapBaker.Render(keyboard, pair.Value);
          var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
          string semantic = "menu-gamepad-" + pair.Key.ToLowerInvariant(); sprite.name = semantic;
          Add(InputGlyphFamily.Generic, pair.Key, semantic, pair.Value, sprite);
        }
        var atlas = InputGlyphSpriteAssetBuilder.Bake(folder + "/C" + identity + ".asset", names);
        var entries = new List<InputGlyphCatalog.Entry>(catalog.Entries);
        foreach (var row in rows) entries.Add(new InputGlyphCatalog.Entry { Family = row.family, ControlPath = row.control,
          SpriteAsset = atlas, SpriteName = row.semantic, ImageSprite = InputGlyphSpriteAssetBuilder.GetSprite(atlas, row.semantic),
          // These are ordinary binding glyphs; generated keyboard-cap metadata belongs to the keycap baker.
          DisplayName = row.label });
        catalog.SetEntries(entries.ToArray());
      }
      finally
      {
        foreach (var sprite in sprites) if (sprite != null) { var texture = sprite.texture; Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
      }
      var settings = ScriptableObject.CreateInstance<InputPromptSettings>();
      settings.Configure(catalog, InputPromptStyle.Glyph);
      AssetDatabase.CreateAsset(settings, folder + "/MenuPromptSettings.asset");
      AssetDatabase.SaveAssetIfDirty(catalog); AssetDatabase.SaveAssetIfDirty(keyboard); AssetDatabase.SaveAssetIfDirty(settings);
      return settings;
    }
  }
}
