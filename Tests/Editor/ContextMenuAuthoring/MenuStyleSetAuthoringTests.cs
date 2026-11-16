using System;
using System.Collections.Generic;
using System.IO;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ThemeSpacing = MToolKit.Theme.Spacing.Spacing;
using ThemeSwatch = MToolKit.Theme.Swatch.Swatch;
using ThemeTypeset = MToolKit.Theme.Typeset.Typeset;

namespace MToolKit.Editor.ContextMenus.Tests
{
  public sealed class MenuStyleSetAuthoringTests
  {
    private string root;
    private MenuCommonSettings common;
    private MenuStyleConfig config;

    [SetUp]
    public void SetUp()
    {
      root = "Assets/Temp/automation-scratch/MenuStyleSetAuthoring_" + Guid.NewGuid().ToString("N");
      EnsureFolder(root);
      common = Save<MenuCommonSettings>("Common");
      config = Save<MenuStyleConfig>("Styles");
      config.Common = common;
      config.OutputFolder = root + "/Generated";
      AssetDatabase.SaveAssetIfDirty(config);
    }

    [TearDown]
    public void TearDown()
    {
      if (root != null && root.StartsWith("Assets/Temp/automation-scratch/MenuStyleSetAuthoring_", StringComparison.Ordinal))
        AssetDatabase.DeleteAsset(root);
    }

    [Test]
    public void CreatesConcreteReusableRuntimeStyleAndRefusesEditorDestinations()
    {
      string path = root + "/PositiveInactive.asset";

      var created = MenuStyleAuthoring.CreateMenuStyleSetAsset(path);

      Assert.That(created, Is.TypeOf<MenuStyleSet>());
      Assert.That(created, Is.InstanceOf<ThemeStyleSet>());
      Assert.That(EditorUtility.IsPersistent(created), Is.True);
      Assert.That(AssetDatabase.GetAssetPath(created), Is.EqualTo(path));
      EnsureFolder(root + "/Editor");
      string editorPath = root + "/Editor/NegativeSelected.asset";
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreateMenuStyleSetAsset(editorPath));
      Assert.That(File.Exists(editorPath), Is.False);
      Assert.That(File.Exists(editorPath + ".meta"), Is.False);
    }

    [TestCase("asset")]
    [TestCase("metadata")]
    [TestCase("directory")]
    public void CreatingTypedStylesNeverAdoptsExistingDataOrMetadata(string occupied)
    {
      string path = root + "/Occupied.asset";
      if (occupied == "asset") Save<MenuStyleSet>("Occupied");
      else if (occupied == "metadata")
        File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n");
      else Directory.CreateDirectory(path);
      byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : null;
      byte[] metadata = File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null;

      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreateMenuStyleSetAsset(path));

      Assert.That(File.Exists(path), Is.EqualTo(before != null));
      if (before != null) Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
      Assert.That(File.Exists(path + ".meta"), Is.EqualTo(metadata != null));
      if (metadata != null) Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metadata));
      if (occupied == "directory") Assert.That(Directory.Exists(path), Is.True);
    }

    [Test]
    public void SharedAndPerStyleStateSlotsBakeInPlaceAndReuseOneTypedAsset()
    {
      var first = Save<MenuProfile>("First");
      var second = Save<MenuProfile>("Second");
      var shared = Save<MenuSkin>("Shared");
      var local = Save<MenuSkin>("Local");
      var fallback = Save<MenuSkin>("Fallback");
      var reusable = Save<MenuStyleSet>("NegativeSelected");
      shared.SetStyle(MenuThemeState.Selected, reusable);
      local.SetStyle(MenuThemeState.Disabled, reusable);
      common.ThemeOverrides = shared;
      first.Skin = second.Skin = fallback;
      config.Styles = new MenuStyleRecipe[] {
        new SingleRingMenuStyleRecipe { Output = first, ThemeOverrides = local },
        new ListMenuStyleRecipe { Output = second, ThemeOverrides = local }
      };
      string firstGuid = GuidOf(first), secondGuid = GuidOf(second), sharedGuid = GuidOf(shared);
      string localGuid = GuidOf(local), reusableGuid = GuidOf(reusable);

      MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      foreach (var output in new[] { first, second })
      {
        Assert.That(output.SharedThemeOverrides, Is.SameAs(shared));
        Assert.That(output.ThemeOverrides, Is.SameAs(local));
        Assert.That(output.Skin, Is.SameAs(fallback));
        Assert.That(output.SharedThemeOverrides.GetStyle(MenuThemeState.Selected), Is.SameAs(reusable));
        Assert.That(output.ThemeOverrides.GetStyle(MenuThemeState.Disabled), Is.SameAs(reusable));
      }
      Assert.That(GuidOf(first), Is.EqualTo(firstGuid));
      Assert.That(GuidOf(second), Is.EqualTo(secondGuid));
      Assert.That(GuidOf(shared), Is.EqualTo(sharedGuid));
      Assert.That(GuidOf(local), Is.EqualTo(localGuid));
      Assert.That(GuidOf(reusable), Is.EqualTo(reusableGuid));
    }

    [TestCase("shared")]
    [TestCase("style")]
    public void ReferencedStyleContentChangesInvalidatePreviewBeforeAnyProfileWrite(string layer)
    {
      var output = Save<MenuProfile>("Existing");
      var skin = Save<MenuSkin>("Layer");
      var set = Save<MenuStyleSet>("NegativeSelected");
      // Direct serialization isolates the Editor fingerprint from the GetStyle/SetStyle behavior tests.
      skin.StateStyles = new[] { new MenuSkinStyle { State = MenuThemeState.Selected, Style = set } };
      common.ThemeOverrides = layer == "shared" ? skin : null;
      config.Styles = new MenuStyleRecipe[] {
        new SingleRingMenuStyleRecipe { Output = output, ThemeOverrides = layer == "style" ? skin : null }
      };
      var plan = MenuStyleAuthoring.Preview(config);
      byte[] before = File.ReadAllBytes(AssetDatabase.GetAssetPath(output));
      set.Foreground = Save<ThemeSwatch>("ChangedForeground");

      var error = Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));

      Assert.That(error.Message, Does.Contain("changed").IgnoreCase);
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(output)), Is.EqualTo(before));
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [Test]
    public void ConversionPreservesEveryTokenChannelAndGuidWhileRetainingRawColorOverrides()
    {
      var source = Save<MenuSkin>("LegacyLayer");
      var profile = Save<MenuProfile>("Profile"); profile.SharedThemeOverrides = source;
      var colors = new Dictionary<MenuVisualRole, ThemeSwatch>();
      var entries = new List<MenuSkinColor>();
      foreach (MenuVisualRole role in Enum.GetValues(typeof(MenuVisualRole)))
      {
        var token = Save<ThemeSwatch>(role.ToString()); colors.Add(role, token);
        entries.Add(new MenuSkinColor { Role = role, State = MenuThemeState.Any, Swatch = token });
      }
      var raw = new Color(.15f, .25f, .35f, .45f);
      entries.Add(new MenuSkinColor { Role = MenuVisualRole.Label, State = MenuThemeState.Disabled,
        OverrideColor = true, Color = raw });
      var dormantIcon = Save<ThemeSwatch>("DormantHoveredIcon");
      var activeIcon = Save<ThemeSwatch>("ActiveHoveredIcon");
      entries.Add(new MenuSkinColor { Role = MenuVisualRole.Icon, State = MenuThemeState.Hovered, Swatch = dormantIcon });
      entries.Add(new MenuSkinColor { Role = MenuVisualRole.Icon, State = MenuThemeState.Hovered, Swatch = activeIcon });
      source.Colors = entries.ToArray();
      var label = Save<ThemeTypeset>("LabelTypeset"); var description = Save<ThemeTypeset>("DescriptionTypeset");
      var selected = Save<ThemeTypeset>("SelectedLabel"); var spacing = Save<ThemeSpacing>("Spacing");
      var dormantType = Save<ThemeTypeset>("DormantHoveredDescription");
      var activeType = Save<ThemeTypeset>("ActiveHoveredDescription");
      source.LabelTypeset = label; source.DescriptionTypeset = description; source.Spacing = spacing;
      source.Typography = new[] {
        new MenuSkinTypography { State = MenuThemeState.Selected, Typeset = selected },
        new MenuSkinTypography { State = MenuThemeState.Hovered, Description = true, Typeset = dormantType },
        new MenuSkinTypography { State = MenuThemeState.Hovered, Description = true, Typeset = activeType }
      };
      source.OverrideTheme = true;
      string sourceGuid = GuidOf(source), profileGuid = GuidOf(profile);
      string folder = root + "/Converted"; EnsureFolder(folder);

      var plan = MenuStyleAuthoring.PreviewStyleSetConversion(source, folder);
      Assert.That(plan.CanConvert, Is.True, string.Join("\n", plan.Diagnostics));
      var created = MenuStyleAuthoring.ConvertStyleSets(plan);

      Assert.That(Array.Exists(source.Colors, entry => entry != null && entry.Swatch == dormantIcon), Is.True,
        "Distinct inactive Swatch references must remain advanced rather than being silently discarded.");
      Assert.That(Array.Exists(source.Colors, entry => entry != null && entry.Swatch == activeIcon), Is.True);
      Assert.That(Array.Exists(source.Typography, entry => entry != null && entry.Typeset == dormantType), Is.True,
        "Distinct inactive Typeset references must remain advanced rather than being silently discarded.");
      Assert.That(Array.Exists(source.Typography, entry => entry != null && entry.Typeset == activeType), Is.True);
      Assert.That(created.Length, Is.EqualTo(2), "Only Any and Selected contain representable legacy data.");
      var any = source.GetStyle(); var selectedStyle = source.GetStyle(MenuThemeState.Selected);
      Assert.That(any, Is.Not.Null); Assert.That(selectedStyle, Is.Not.Null);
      Assert.That(any.PanelBackground, Is.SameAs(colors[MenuVisualRole.Background]));
      Assert.That(any.Normal, Is.SameAs(colors[MenuVisualRole.Normal]));
      Assert.That(any.Hovered, Is.SameAs(colors[MenuVisualRole.Hovered]));
      Assert.That(any.Selected, Is.SameAs(colors[MenuVisualRole.Selected]));
      Assert.That(any.Disabled, Is.SameAs(colors[MenuVisualRole.Disabled]));
      Assert.That(any.InnerBorder, Is.SameAs(colors[MenuVisualRole.InnerBorder]));
      Assert.That(any.OuterBorder, Is.SameAs(colors[MenuVisualRole.OuterBorder]));
      Assert.That(any.Separator, Is.SameAs(colors[MenuVisualRole.Separator]));
      Assert.That(any.Foreground, Is.SameAs(colors[MenuVisualRole.Label]));
      Assert.That(any.Description, Is.SameAs(colors[MenuVisualRole.Description]));
      Assert.That(any.Icon, Is.SameAs(colors[MenuVisualRole.Icon]));
      Assert.That(any.CenterBackground, Is.SameAs(colors[MenuVisualRole.Center]));
      Assert.That(any.AuxiliaryBackground, Is.SameAs(colors[MenuVisualRole.Auxiliary]));
      Assert.That(any.Checked, Is.SameAs(colors[MenuVisualRole.Checked]));
      Assert.That(any.Backdrop, Is.SameAs(colors[MenuVisualRole.Backdrop]));
      Assert.That(any.LabelTypeset, Is.SameAs(label)); Assert.That(any.DescriptionTypeset, Is.SameAs(description));
      Assert.That(any.Spacing, Is.SameAs(spacing)); Assert.That(any.OverrideTheme, Is.True);
      Assert.That(selectedStyle.LabelTypeset, Is.SameAs(selected));
      Assert.That(selectedStyle.OverrideTheme, Is.True);
      Assert.That(source.Colors.Length, Is.EqualTo(3), "Raw colors and distinct duplicate token groups remain explicit advanced overrides.");
      Assert.That(source.Colors[0].OverrideColor, Is.True);
      Assert.That(source.Colors[0].State, Is.EqualTo(MenuThemeState.Disabled));
      Assert.That(source.Colors[0].Color, Is.EqualTo(raw));
      Assert.That(source.Typography.Length, Is.EqualTo(2));
      Assert.That(source.GetStyle(MenuThemeState.Hovered), Is.Null, "An ambiguous duplicate state is not converted into a new style.");
      Assert.That(source.LabelTypeset, Is.Null); Assert.That(source.DescriptionTypeset, Is.Null); Assert.That(source.Spacing, Is.Null);
      Assert.That(profile.SharedThemeOverrides, Is.SameAs(source));
      Assert.That(GuidOf(source), Is.EqualTo(sourceGuid)); Assert.That(GuidOf(profile), Is.EqualTo(profileGuid));
      foreach (var token in colors.Values) Assert.That(AssetDatabase.LoadAssetAtPath<ThemeSwatch>(AssetDatabase.GetAssetPath(token)), Is.SameAs(token));
    }

    [Test]
    public void ChangedConversionSourceIsRejectedBeforeCreatingAssetsOrClearingLegacyData()
    {
      var source = Save<MenuSkin>("LegacyLayer"); source.Spacing = Save<ThemeSpacing>("OriginalSpacing");
      string folder = root + "/Converted"; EnsureFolder(folder);
      var plan = MenuStyleAuthoring.PreviewStyleSetConversion(source, folder);
      source.Spacing = Save<ThemeSpacing>("ChangedSpacing");
      string state = EditorJsonUtility.ToJson(source);

      var error = Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.ConvertStyleSets(plan));

      Assert.That(error.Message, Does.Contain("changed").IgnoreCase);
      Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(state));
      Assert.That(Directory.GetFileSystemEntries(folder), Is.Empty);
    }

    private T Save<T>(string name) where T : ScriptableObject
    {
      var asset = ScriptableObject.CreateInstance<T>();
      AssetDatabase.CreateAsset(asset, root + "/" + name + ".asset");
      return asset;
    }
    private static string GuidOf(UnityEngine.Object asset)
      => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    private static void EnsureFolder(string path)
    {
      if (AssetDatabase.IsValidFolder(path)) return;
      string parent = path.Substring(0, path.LastIndexOf('/'));
      EnsureFolder(parent);
      AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
  }
}
