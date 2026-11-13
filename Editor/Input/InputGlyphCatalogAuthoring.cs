using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MToolKit.Runtime.Input.Config;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.Input
{
  /// <summary>Optional migration from the serialized legacy Image icon sets into native TMP resources.</summary>
  public static class InputGlyphCatalogAuthoring
  {
    private static readonly string[] Controls =
    {
      "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "start", "select", "leftShoulder",
      "rightShoulder", "leftTrigger", "rightTrigger", "leftStick", "rightStick", "leftStickPress",
      "rightStickPress", "dpad", "dpad/up", "dpad/down", "dpad/left", "dpad/right"
    };
    private static readonly string[] Faces = { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth" };

    /// <summary>
    /// Creates five independently replaceable sprite assets and one catalog. Original icon sets and
    /// import settings remain unchanged. Nintendo retains logical bindings and remaps printed letters;
    /// generic controllers use neutral position diagrams rather than Xbox markings.
    /// </summary>
    public static InputGlyphCatalog Generate(GamepadIconsData legacy, string destinationFolder,
      string catalogName = "InputGlyphCatalog")
    {
      if (legacy == null) throw new ArgumentNullException(nameof(legacy));
      if (string.IsNullOrEmpty(catalogName) || !Regex.IsMatch(catalogName, "^[A-Za-z0-9_-]+$"))
        throw new ArgumentException("Choose an asset filename without an extension or directory.", nameof(catalogName));
      InputGlyphSpriteAssetBuilder.EnsureFolder(destinationFolder);
      var entries = new List<InputGlyphCatalog.Entry>();
      AddGamepad(InputGlyphFamily.Xbox, "Xbox", Controls, control => legacy.XboxIcons.GetSprite(control), destinationFolder, entries);
      AddGamepad(InputGlyphFamily.PlayStation, "PlayStation", Controls, control => legacy.Ps4Icons.GetSprite(control), destinationFolder, entries);
      var nintendoSources = new Dictionary<string, string>
      {
        ["buttonSouth"] = "buttonEast", ["buttonEast"] = "buttonSouth",
        ["buttonWest"] = "buttonNorth", ["buttonNorth"] = "buttonWest"
      };
      AddGamepad(InputGlyphFamily.Nintendo, "Nintendo", Faces,
        control => legacy.XboxIcons.GetSprite(nintendoSources[control]), destinationFolder, entries);

      var temporary = new List<Sprite>();
      try
      {
        var generic = Faces.ToDictionary(control => control, control => InputGlyphStarterArt.FacePosition(control));
        temporary.AddRange(generic.Values);
        AddGamepad(InputGlyphFamily.Generic, "Generic", Faces, control => generic[control], destinationFolder, entries);
        var keys = new Dictionary<string, Sprite>();
        foreach (string key in new[] { "enter", "space", "escape", "tab", "e" })
        {
          keys.Add(key, InputGlyphStarterArt.Keycap(key));
          temporary.Add(keys[key]);
        }
        foreach (string mouse in new[] { "leftButton", "rightButton", "middleButton" })
        {
          keys.Add(mouse, InputGlyphStarterArt.MouseButton(mouse));
          temporary.Add(keys[mouse]);
        }
        var keyboardAsset = InputGlyphSpriteAssetBuilder.Bake(destinationFolder + "/MTK_InputGlyphs_KeyboardAndMouse.asset",
          keys.Select(pair => new InputGlyphSpriteAssetBuilder.NamedSprite(
            pair.Key.EndsWith("Button", StringComparison.Ordinal) ? Semantic("mouse", pair.Key) : "keyboard-" + pair.Key, pair.Value)).ToArray());
        foreach (string control in keys.Keys)
        {
          bool mouse = control.EndsWith("Button", StringComparison.Ordinal);
          string semantic = mouse ? Semantic("mouse", control) : "keyboard-" + control;
          entries.Add(Entry(InputGlyphFamily.KeyboardAndMouse, control, keyboardAsset, semantic,
            "input." + (mouse ? "mouse." : "key.") + control, KeyboardName(control)));
        }
      }
      finally
      {
        foreach (var sprite in temporary)
        {
          var texture = sprite.texture;
          Object.DestroyImmediate(sprite);
          Object.DestroyImmediate(texture);
        }
      }

      string catalogPath = destinationFolder + "/" + catalogName + ".asset";
      var existing = AssetDatabase.LoadMainAssetAtPath(catalogPath);
      if (existing != null && !(existing is InputGlyphCatalog))
        throw new InvalidOperationException("Catalog destination contains a different asset: " + catalogPath);
      var catalog = existing as InputGlyphCatalog;
      if (catalog == null)
      {
        catalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
        AssetDatabase.CreateAsset(catalog, catalogPath);
      }
      catalog.SetEntries(entries.ToArray());
      catalog.SetLayoutRules(
        new InputGlyphCatalog.LayoutRule { Layout = "DualShockGamepad", Family = InputGlyphFamily.PlayStation },
        new InputGlyphCatalog.LayoutRule { Layout = "DualSenseGamepadHID", Family = InputGlyphFamily.PlayStation },
        new InputGlyphCatalog.LayoutRule { Layout = "XInputController", Family = InputGlyphFamily.Xbox },
        new InputGlyphCatalog.LayoutRule { Layout = "SwitchProControllerHID", Family = InputGlyphFamily.Nintendo });
      EditorUtility.SetDirty(catalog);
      AssetDatabase.SaveAssets();
      return catalog;
    }

    private static void AddGamepad(InputGlyphFamily family, string familyName, IEnumerable<string> controls,
      Func<string, Sprite> spriteFor, string folder, ICollection<InputGlyphCatalog.Entry> entries)
    {
      string prefix = familyName.ToLowerInvariant();
      var sources = controls.Select(control => new InputGlyphSpriteAssetBuilder.NamedSprite(Semantic(prefix, control), spriteFor(control))).ToArray();
      if (sources.Any(source => source.Sprite == null))
        throw new InvalidOperationException("The legacy " + familyName + " icon set has missing sprites. Complete its mappings before migration.");
      var asset = InputGlyphSpriteAssetBuilder.Bake(folder + "/MTK_InputGlyphs_" + familyName + ".asset", sources);
      foreach (string control in controls)
        entries.Add(Entry(family, control, asset, Semantic(prefix, control),
          "input.gamepad." + prefix + "." + control, GamepadName(family, control)));
    }

    private static InputGlyphCatalog.Entry Entry(InputGlyphFamily family, string control, TMP_SpriteAsset asset,
      string semantic, string localizationKey, string fallback) => new()
    {
      Family = family, ControlPath = control, SpriteAsset = asset, SpriteName = semantic,
      ImageSprite = InputGlyphSpriteAssetBuilder.GetSprite(asset, semantic), LocalizationKey = localizationKey,
      DisplayName = fallback
    };

    private static string Semantic(string prefix, string control) => prefix + "-"
      + Regex.Replace(control.Replace('/', '-'), "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();

    /// <summary>Updates authored family names without changing native sprite assets or glyph identities.</summary>
    public static int RefreshFamilyNames(InputGlyphCatalog catalog)
    {
      if (catalog == null) throw new ArgumentNullException(nameof(catalog));
      int changed = 0;
      foreach (var entry in catalog.Entries)
      {
        if (entry.Family == InputGlyphFamily.KeyboardAndMouse
          || (entry.ControlPath != "start" && entry.ControlPath != "select")) continue;
        string name = GamepadName(entry.Family, entry.ControlPath);
        if (entry.DisplayName == name) continue;
        entry.DisplayName = name;
        changed++;
      }
      if (changed > 0) { catalog.SetEntries(catalog.Entries.ToArray()); EditorUtility.SetDirty(catalog); }
      return changed;
    }

    private static string GamepadName(InputGlyphFamily family, string control)
    {
      if (control == "start")
        switch (family)
        {
          case InputGlyphFamily.Xbox: return "Menu";
          case InputGlyphFamily.PlayStation: return "Options";
          case InputGlyphFamily.Nintendo: return "Plus";
          default: return "Start";
        }
      if (control == "select")
        switch (family)
        {
          case InputGlyphFamily.Xbox: return "View";
          case InputGlyphFamily.PlayStation: return "Share";
          case InputGlyphFamily.Nintendo: return "Minus";
          default: return "Select";
        }
      int index = Array.IndexOf(Faces, control);
      if (index >= 0)
      {
        switch (family)
        {
          case InputGlyphFamily.Xbox: return new[] { "A", "B", "X", "Y" }[index];
          case InputGlyphFamily.PlayStation: return new[] { "Cross", "Circle", "Square", "Triangle" }[index];
          case InputGlyphFamily.Nintendo: return new[] { "B", "A", "Y", "X" }[index];
          default: return new[] { "South Button", "East Button", "West Button", "North Button" }[index];
        }
      }
      return Regex.Replace(control.Replace("dpad", "D-Pad").Replace('/', ' '), "([a-z])([A-Z])", "$1 $2");
    }

    private static string KeyboardName(string control)
    {
      switch (control)
      {
        case "leftButton": return "Left Click";
        case "rightButton": return "Right Click";
        case "middleButton": return "Middle Click";
        default: return char.ToUpperInvariant(control[0]) + control.Substring(1);
      }
    }
  }
}
