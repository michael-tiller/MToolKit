using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MToolKit.ContextMenus.Configuration;
using UnityEditor;
using UnityEngine;
using ThemeSwatch = MToolKit.Theme.Swatch.Swatch;
using ThemeTypeset = MToolKit.Theme.Typeset.Typeset;

namespace MToolKit.Editor.ContextMenus
{
  public static partial class MenuStyleAuthoring
  {
    /// <summary>Read-only conversion plan. Raw colors and ambiguous legacy entries remain advanced overrides.</summary>
    public static MenuStyleSetConversionPlan PreviewStyleSetConversion(MenuSkin source, string outputFolder)
    {
      var entries = new List<MenuStyleSetConversionEntry>();
      var diagnostics = new List<string>();
      var plan = new MenuStyleSetConversionPlan { Source = source, OutputFolder = outputFolder,
        Entries = entries, Diagnostics = diagnostics };
      if (source == null || !SavedAsset(source) || !AssetDatabase.IsMainAsset(source))
        diagnostics.Add("Select a saved main MenuSkin asset under Assets.");
      if (!ValidFolder(outputFolder) || IsEditorPath(outputFolder) || !AssetDatabase.IsValidFolder(outputFolder))
        diagnostics.Add("Choose an existing runtime Assets folder outside Editor folders for the state style assets.");
      if (diagnostics.Count == 0)
      {
        string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));
        foreach (MenuThemeState state in Enum.GetValues(typeof(MenuThemeState)))
        {
          if (!HasConvertibleTokens(source, state)) continue;
          if (source.GetStyle(state) != null)
          { diagnostics.Add(state + ": a style is already assigned. Existing style assets are never overwritten."); continue; }
          string path = outputFolder + "/" + name + "_" + state + ".asset";
          entries.Add(new MenuStyleSetConversionEntry { State = state, OutputPath = path });
          if (Exists(path)) diagnostics.Add("Refusing to adopt an existing asset, directory or metadata at " + path + ".");
        }
      }
      plan.Fingerprint = ConversionFingerprint(plan);
      return plan;
    }

    /// <summary>Creates and verifies all state assets before retiring any converted source values.</summary>
    public static MenuStyleSet[] ConvertStyleSets(MenuStyleSetConversionPlan plan)
    {
      if (EditorApplication.isPlayingOrWillChangePlaymode)
        throw new InvalidOperationException("Stop Play Mode before converting menu styles.");
      if (plan?.Source == null) throw new InvalidOperationException("Preview a saved menu skin before converting it.");
      var current = PreviewStyleSetConversion(plan.Source, plan.OutputFolder);
      if (current.Fingerprint != plan.Fingerprint)
        throw new InvalidOperationException("The source or conversion destinations changed after preview. Refresh the conversion preview.");
      if (!current.CanConvert) throw new InvalidOperationException(string.Join("\n", current.Diagnostics));
      var source = plan.Source;
      string sourceFingerprint = SourceFingerprint(source);
      var created = new List<MenuStyleSet>();
      foreach (var entry in current.Entries)
      {
        var style = CreateMenuStyleSetAsset(entry.OutputPath);
        PopulateStyle(source, entry.State, style);
        string expected = EditorJsonUtility.ToJson(style);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        if (!EditorUtility.IsPersistent(style) || !File.Exists(entry.OutputPath) ||
            AssetDatabase.LoadAssetAtPath<MenuStyleSet>(entry.OutputPath) != style ||
            expected != EditorJsonUtility.ToJson(style))
          throw new InvalidOperationException("The created style could not be verified at " + entry.OutputPath + ". The legacy source is unchanged.");
        Undo.RegisterCreatedObjectUndo(style, "Create menu state style");
        created.Add(style);
      }
      if (SourceFingerprint(source) != sourceFingerprint)
        throw new InvalidOperationException("The source changed while creating style assets. The legacy source was not converted; inspect the new assets before retrying.");
      if (created.Count == 0) return Array.Empty<MenuStyleSet>();
      Undo.RecordObject(source, "Convert legacy menu tokens to state styles");
      var states = new HashSet<MenuThemeState>(current.Entries.Select(entry => entry.State));
      bool label = LegacyTypeset(source, MenuThemeState.Any, false) != null;
      bool description = LegacyTypeset(source, MenuThemeState.Any, true) != null;
      var colors = source.Colors ?? Array.Empty<MenuSkinColor>();
      source.Colors = colors.Where(entry => entry == null || !states.Contains(entry.State) ||
        LegacySwatch(source, entry.State, entry.Role) == null).ToArray();
      var typography = source.Typography ?? Array.Empty<MenuSkinTypography>();
      source.Typography = typography.Where(entry => entry == null || !states.Contains(entry.State) ||
        LegacyTypeset(source, entry.State, entry.Description) == null).ToArray();
      if (states.Contains(MenuThemeState.Any))
      {
        if (label) source.LabelTypeset = null;
        if (description) source.DescriptionTypeset = null;
        source.Spacing = null;
      }
      for (int i = 0; i < current.Entries.Count; i++) source.SetStyle(current.Entries[i].State, created[i]);
      EditorUtility.SetDirty(source);
      AssetDatabase.SaveAssetIfDirty(source);
      return created.ToArray();
    }

    private static bool HasConvertibleTokens(MenuSkin source, MenuThemeState state)
    {
      foreach (MenuVisualRole role in Enum.GetValues(typeof(MenuVisualRole)))
        if (LegacySwatch(source, state, role) != null) return true;
      return LegacyTypeset(source, state, false) != null || LegacyTypeset(source, state, true) != null ||
        state == MenuThemeState.Any && source.Spacing != null;
    }

    private static ThemeSwatch LegacySwatch(MenuSkin source, MenuThemeState state, MenuVisualRole role)
    {
      if (source.Colors == null) return null;
      MenuSkinColor effective = null;
      ThemeSwatch distinct = null;
      foreach (var entry in source.Colors)
      {
        if (entry == null || entry.State != state || entry.Role != role) continue;
        // Keep every raw-color group intact rather than discarding dormant authored data.
        if (entry.OverrideColor) return null;
        if (entry.Swatch != null)
        {
          if (distinct != null && distinct != entry.Swatch) return null;
          distinct = entry.Swatch;
        }
        effective = entry;
      }
      return effective?.Swatch;
    }

    private static ThemeTypeset LegacyTypeset(MenuSkin source, MenuThemeState state, bool description)
    {
      ThemeTypeset exact = null;
      ThemeTypeset distinct = null;
      if (source.Typography != null)
        foreach (var entry in source.Typography)
          if (entry != null && entry.State == state && entry.Description == description)
          {
            if (entry.Typeset != null)
            {
              if (distinct != null && distinct != entry.Typeset) return null;
              distinct = entry.Typeset;
            }
            exact = entry.Typeset;
          }
      var fallback = state == MenuThemeState.Any ? description ? source.DescriptionTypeset : source.LabelTypeset : null;
      // Preserve distinct legacy Any anchors instead of losing an inactive fallback reference.
      if (distinct != null && fallback != null && distinct != fallback) return null;
      return exact != null ? exact : fallback;
    }

    private static void PopulateStyle(MenuSkin source, MenuThemeState state, MenuStyleSet target)
    {
      target.OverrideTheme = source.OverrideTheme;
      target.PanelBackground = LegacySwatch(source, state, MenuVisualRole.Background);
      target.Normal = LegacySwatch(source, state, MenuVisualRole.Normal);
      target.Hovered = LegacySwatch(source, state, MenuVisualRole.Hovered);
      target.Selected = LegacySwatch(source, state, MenuVisualRole.Selected);
      target.Disabled = LegacySwatch(source, state, MenuVisualRole.Disabled);
      target.InnerBorder = LegacySwatch(source, state, MenuVisualRole.InnerBorder);
      target.OuterBorder = LegacySwatch(source, state, MenuVisualRole.OuterBorder);
      target.Separator = LegacySwatch(source, state, MenuVisualRole.Separator);
      target.Foreground = LegacySwatch(source, state, MenuVisualRole.Label);
      target.Description = LegacySwatch(source, state, MenuVisualRole.Description);
      target.Icon = LegacySwatch(source, state, MenuVisualRole.Icon);
      target.CenterBackground = LegacySwatch(source, state, MenuVisualRole.Center);
      target.AuxiliaryBackground = LegacySwatch(source, state, MenuVisualRole.Auxiliary);
      target.Checked = LegacySwatch(source, state, MenuVisualRole.Checked);
      target.Backdrop = LegacySwatch(source, state, MenuVisualRole.Backdrop);
      target.LabelTypeset = LegacyTypeset(source, state, false);
      target.DescriptionTypeset = LegacyTypeset(source, state, true);
      if (state == MenuThemeState.Any) target.Spacing = source.Spacing;
    }

    private static string SourceFingerprint(MenuSkin source)
    {
      var text = new StringBuilder(); AppendLayer(text, source); return HashText(text);
    }
    private static string ConversionFingerprint(MenuStyleSetConversionPlan plan)
    {
      var text = new StringBuilder(); AppendLayer(text, plan.Source); text.Append('|').Append(plan.OutputFolder);
      foreach (var entry in plan.Entries)
      {
        text.Append('|').Append(entry.State).Append('|').Append(entry.OutputPath);
        AppendFile(text, entry.OutputPath); text.Append('|').Append(Directory.Exists(entry.OutputPath));
        text.Append('|').Append(Directory.Exists(entry.OutputPath + ".meta"));
      }
      return HashText(text);
    }
    private static string HashText(StringBuilder text)
    {
      using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }
  }
}
