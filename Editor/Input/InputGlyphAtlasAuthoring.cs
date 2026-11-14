using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Editor.Input
{
  public static class InputGlyphAtlasAuthoring
  {
    public static string[] Validate(InputGlyphAtlasSource source)
    {
      var errors = new List<string>();
      if (source == null) return new[] { "Choose an atlas source profile." };
      if (source.Catalog == null) errors.Add("Choose the runtime glyph catalog.");
      if (string.IsNullOrWhiteSpace(source.SpriteAssetPath) || !source.SpriteAssetPath.StartsWith("Assets/", StringComparison.Ordinal)
        || !source.SpriteAssetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
        || source.SpriteAssetPath.Split('/').Any(p => p == ".." || p == ".") || source.SpriteAssetPath.Contains("\\")
        || !Regex.IsMatch(Path.GetFileNameWithoutExtension(source.SpriteAssetPath), "^[A-Za-z0-9_-]+$"))
        errors.Add("Choose an Assets/.../Semantic_Atlas_Name.asset destination without spaces or path traversal.");
      if (!Enum.IsDefined(typeof(InputGlyphFamily), source.Family)) errors.Add("Select a known device family.");
      if (source.Glyphs == null || source.Glyphs.Length == 0) errors.Add("Add at least one glyph.");
      var controls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var glyph in source.Glyphs ?? Array.Empty<InputGlyphAtlasSource.Glyph>())
      {
        if (glyph == null) { errors.Add("Remove empty glyph rows."); continue; }
        if (!Enum.IsDefined(typeof(InputGlyphEntryKind), glyph.Kind)) errors.Add("Select a known glyph entry kind.");
        if (glyph.Kind == InputGlyphEntryKind.Device)
        {
          if (!IsLogicalName(glyph.DeviceLayout)) errors.Add("Every device glyph needs a native Input System device layout.");
          if (!string.IsNullOrEmpty(glyph.ControlPath)) errors.Add("Device glyphs use a device layout rather than a binding control path.");
        }
        else if (!IsLogicalName(glyph.ControlPath))
          errors.Add("Every binding glyph needs a logical Input System control path.");
        if (!Enum.IsDefined(typeof(InputKeycapContentMode), glyph.KeycapContent)) errors.Add("Select a known keycap content mode.");
        if (!Enum.IsDefined(typeof(InputGlyphPlatform), glyph.Platform)) errors.Add("Select a known glyph platform.");
        if (!controls.Add(Identity(glyph.Kind, glyph.DeviceLayout, glyph.ControlPath, glyph.LocaleCode, glyph.BakedLabel, glyph.KeycapContent, glyph.Platform)))
          errors.Add("Duplicate binding or device/locale/content/platform: " + LogicalName(glyph) + " [" + glyph.Platform + "]");
        if (string.IsNullOrEmpty(glyph.SpriteName) || !Regex.IsMatch(glyph.SpriteName, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || !names.Add(glyph.SpriteName))
          errors.Add("Sprite names must be distinct semantic lowercase names: " + glyph.SpriteName);
        if (glyph.SourceSprite == null) errors.Add("Choose source artwork for " + LogicalName(glyph));
      }
      var target = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(source.SpriteAssetPath ?? "");
      if (target != null && source.Catalog != null && source.Catalog.Entries.Any(entry => entry != null && entry.SpriteAsset == target && entry.Family != source.Family))
        errors.Add("This atlas is shared by another family. Choose a separate destination to preserve its mappings.");
      return errors.ToArray();
    }

    public static TMP_SpriteAsset Rebuild(InputGlyphAtlasSource source)
    {
      var errors = Validate(source);
      if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
      var old = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(source.SpriteAssetPath);
      var generatedSources = source.Glyphs.Select(glyph => AssetDatabase.GetAssetPath(glyph.SourceSprite) == source.SpriteAssetPath).ToArray();
      var asset = InputGlyphSpriteAssetBuilder.Bake(source.SpriteAssetPath,
        source.Glyphs.Select(glyph => new InputGlyphSpriteAssetBuilder.NamedSprite(glyph.SpriteName, glyph.SourceSprite, glyph.Tint)).ToArray());
      var identities = new HashSet<string>(source.Glyphs.Select(glyph => Identity(glyph.Kind, glyph.DeviceLayout, glyph.ControlPath, glyph.LocaleCode, glyph.BakedLabel, glyph.KeycapContent, glyph.Platform)), StringComparer.OrdinalIgnoreCase);
      var entries = source.Catalog.Entries.Where(entry => entry != null
        && !(entry.Family == source.Family && old != null && entry.SpriteAsset == old)
        && !((entry.Kind == InputGlyphEntryKind.Device || entry.Family == source.Family)
          && identities.Contains(Identity(entry.Kind, entry.DeviceLayout, entry.ControlPath, entry.LocaleCode, entry.BakedLabel, entry.KeycapContent, entry.Platform)))).ToList();
      for (int i = 0; i < source.Glyphs.Length; i++)
      {
        var glyph = source.Glyphs[i];
        var sprite = InputGlyphSpriteAssetBuilder.GetSprite(asset, glyph.SpriteName);
        entries.Add(new InputGlyphCatalog.Entry {
          Family = source.Family, Kind = glyph.Kind, DeviceLayout = glyph.DeviceLayout,
          ControlPath = glyph.ControlPath, LocaleCode = glyph.LocaleCode,
          BakedLabel = glyph.BakedLabel, KeycapContent = glyph.KeycapContent, Platform = glyph.Platform, SpriteAsset = asset, SpriteName = glyph.SpriteName,
          ImageSprite = sprite, Symbol = glyph.Symbol, LocalizationKey = glyph.LocalizationKey, DisplayName = glyph.DisplayName
        });
        if (generatedSources[i])
        {
          glyph.SourceSprite = sprite;
          // Destination-backed artwork already contains its tint; do not multiply it again on the next rebuild.
          glyph.Tint = Color.white;
        }
      }
      Undo.RecordObject(source.Catalog, "Rebuild input glyph atlas");
      source.Catalog.SetEntries(entries.ToArray());
      EditorUtility.SetDirty(source.Catalog);
      EditorUtility.SetDirty(source);
      AssetDatabase.SaveAssetIfDirty(source.Catalog);
      if (AssetDatabase.Contains(source)) AssetDatabase.SaveAssetIfDirty(source);
      return asset;
    }

    public static void ImportFromCatalog(InputGlyphAtlasSource source, TMP_SpriteAsset atlas = null)
    {
      if (source == null || source.Catalog == null) throw new ArgumentException("Choose a source profile and catalog.");
      atlas = atlas ?? source.Catalog.Entries.FirstOrDefault(entry => entry != null && entry.Family == source.Family && entry.SpriteAsset != null)?.SpriteAsset;
      if (atlas == null) throw new InvalidOperationException("This family has no authored atlas. Choose a destination and add sprites.");
      source.SpriteAssetPath = AssetDatabase.GetAssetPath(atlas);
      source.Glyphs = source.Catalog.Entries.Where(entry => entry != null && entry.Family == source.Family && entry.SpriteAsset == atlas)
        .Select(entry => new InputGlyphAtlasSource.Glyph { Kind = entry.Kind, DeviceLayout = entry.DeviceLayout,
          ControlPath = entry.ControlPath, SpriteName = entry.SpriteName,
          SourceSprite = entry.ImageSprite != null ? entry.ImageSprite : InputGlyphSpriteAssetBuilder.GetSprite(atlas, entry.SpriteName),
          Tint = Color.white,
          LocaleCode = entry.LocaleCode, BakedLabel = entry.BakedLabel, KeycapContent = entry.KeycapContent, Platform = entry.Platform, Symbol = entry.Symbol,
          LocalizationKey = entry.LocalizationKey, DisplayName = entry.DisplayName }).ToArray();
      EditorUtility.SetDirty(source);
    }

    private static bool IsLogicalName(string value)
      => !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(new[] { '<', '>', '"', '\r', '\n', '\t' }) < 0;

    private static string LogicalName(InputGlyphAtlasSource.Glyph glyph)
      => glyph.Kind == InputGlyphEntryKind.Device ? glyph.DeviceLayout : glyph.ControlPath;

    private static string Identity(InputGlyphEntryKind kind, string layout, string control, string locale, string bakedLabel,
      InputKeycapContentMode content, InputGlyphPlatform platform)
      => kind == InputGlyphEntryKind.Device
        // Runtime device selection uses native layout, locale and platform, independently of binding-family/keycap metadata.
        ? (int)kind + "|" + (layout ?? "") + "|" + (locale ?? "").Replace('_', '-') + "|" + (int)platform
        : (int)kind + "|" + (control ?? "")
        + "|" + (locale ?? "").Replace('_', '-') + "|"
        + (content == InputKeycapContentMode.Sprite ? "symbol" : content == InputKeycapContentMode.SpriteAndLabel
          ? "mixed" : string.IsNullOrEmpty(bakedLabel) ? "authored" : "generated") + "|" + (int)platform;
  }
}
