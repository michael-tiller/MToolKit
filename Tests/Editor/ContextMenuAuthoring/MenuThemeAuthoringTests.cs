using System;
using System.IO;
using MToolKit.ContextMenus.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ThemeSwatch = MToolKit.Theme.Swatch.Swatch;
using ThemeTypeset = MToolKit.Theme.Typeset.Typeset;

namespace MToolKit.Editor.ContextMenus.Tests
{
  public sealed class MenuThemeAuthoringTests
  {
    private string root;
    private MenuCommonSettings common;
    private MenuStyleConfig config;

    [SetUp]
    public void SetUp()
    {
      root = "Assets/Temp/automation-scratch/MenuThemeAuthoring_" + Guid.NewGuid().ToString("N");
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
      if (root != null && root.StartsWith("Assets/Temp/automation-scratch/MenuThemeAuthoring_", StringComparison.Ordinal))
        AssetDatabase.DeleteAsset(root);
    }

    [Test]
    public void SharedAndPerStyleOverridesBakeAsDistinctReferencesWithoutReplacingFallbackOrTokens()
    {
      var output = Save<MenuProfile>("Existing");
      var fallback = Save<MenuSkin>("Fallback");
      var shared = Save<MenuSkin>("SharedOverrides");
      var style = Save<MenuSkin>("StyleOverrides");
      var token = Save<ThemeSwatch>("SelectedLabel");
      shared.Colors = new[] { new MenuSkinColor { Role = MenuVisualRole.Label,
        State = MenuThemeState.Selected, Swatch = token } };
      output.Skin = fallback;
      common.ThemeOverrides = shared;
      var recipe = new SingleRingMenuStyleRecipe { Output = output, ThemeOverrides = style };
      config.Styles = new MenuStyleRecipe[] { recipe };
      string outputGuid = GuidOf(output), sharedGuid = GuidOf(shared), styleGuid = GuidOf(style);

      var rebuilt = MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config));

      Assert.That(rebuilt, Is.EqualTo(new[] { output }));
      Assert.That(output.SharedThemeOverrides, Is.SameAs(shared));
      Assert.That(output.ThemeOverrides, Is.SameAs(style));
      Assert.That(output.Skin, Is.SameAs(fallback));
      Assert.That(output.SharedThemeOverrides.Colors[0].Swatch, Is.SameAs(token));
      Assert.That(common.ThemeOverrides, Is.SameAs(shared));
      Assert.That(recipe.ThemeOverrides, Is.SameAs(style));
      Assert.That(GuidOf(output), Is.EqualTo(outputGuid));
      Assert.That(GuidOf(shared), Is.EqualTo(sharedGuid));
      Assert.That(GuidOf(style), Is.EqualTo(styleGuid));
    }

    [Test]
    public void SelectedRebuildCanClearBothOverrideLayersWithoutChangingOtherOutputsOrFallback()
    {
      var first = Save<MenuProfile>("First");
      var second = Save<MenuProfile>("Second");
      var fallback = Save<MenuSkin>("Fallback");
      var shared = Save<MenuSkin>("SharedOverrides");
      var style = Save<MenuSkin>("StyleOverrides");
      first.Skin = second.Skin = fallback;
      first.SharedThemeOverrides = second.SharedThemeOverrides = shared;
      first.ThemeOverrides = second.ThemeOverrides = style;
      AssetDatabase.SaveAssetIfDirty(first);
      AssetDatabase.SaveAssetIfDirty(second);
      var selected = new SingleRingMenuStyleRecipe { Output = first };
      config.Styles = new MenuStyleRecipe[] { selected,
        new SingleRingMenuStyleRecipe { Output = second, ThemeOverrides = style } };
      common.ThemeOverrides = null;
      byte[] otherBefore = File.ReadAllBytes(AssetDatabase.GetAssetPath(second));

      var rebuilt = MenuStyleAuthoring.Rebuild(MenuStyleAuthoring.Preview(config, selected.Id));

      Assert.That(rebuilt, Is.EqualTo(new[] { first }));
      Assert.That(first.SharedThemeOverrides, Is.Null);
      Assert.That(first.ThemeOverrides, Is.Null);
      Assert.That(first.Skin, Is.SameAs(fallback));
      Assert.That(second.SharedThemeOverrides, Is.SameAs(shared));
      Assert.That(second.ThemeOverrides, Is.SameAs(style));
      Assert.That(second.Skin, Is.SameAs(fallback));
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(second)), Is.EqualTo(otherBefore));
    }

    [TestCase("shared")]
    [TestCase("style")]
    public void EditingAnInlineOverrideAssetInvalidatesPreviewBeforeAnyProfileWrite(string layer)
    {
      var output = Save<MenuProfile>("Existing");
      var shared = Save<MenuSkin>("SharedOverrides");
      var style = Save<MenuSkin>("StyleOverrides");
      common.ThemeOverrides = shared;
      config.Styles = new MenuStyleRecipe[] {
        new SingleRingMenuStyleRecipe { Output = output, ThemeOverrides = style }
      };
      byte[] outputBefore = File.ReadAllBytes(AssetDatabase.GetAssetPath(output));
      var plan = MenuStyleAuthoring.Preview(config);
      if (layer == "shared")
        shared.Colors = new[] { new MenuSkinColor { Role = MenuVisualRole.Label,
          State = MenuThemeState.Disabled, Swatch = Save<ThemeSwatch>("ChangedSwatch") } };
      else
        style.Typography = new[] { new MenuSkinTypography { State = MenuThemeState.Selected,
          Description = true, Typeset = Save<ThemeTypeset>("ChangedDescription") } };

      var error = Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.Rebuild(plan));

      Assert.That(error.Message, Does.Contain("changed").IgnoreCase);
      Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(output)), Is.EqualTo(outputBefore));
      Assert.That(AssetDatabase.IsValidFolder(config.OutputFolder), Is.False);
    }

    [Test]
    public void OverridesAreCreatedAsRuntimeAssetsAndEditorFolderDestinationsAreRefused()
    {
      string runtimePath = root + "/ThemeOverrides.asset";

      var created = MenuStyleAuthoring.CreateThemeOverrideAsset(runtimePath);

      Assert.That(created, Is.Not.Null);
      Assert.That(EditorUtility.IsPersistent(created), Is.True);
      Assert.That(AssetDatabase.GetAssetPath(created), Is.EqualTo(runtimePath));
      Assert.That(created.Colors, Is.Empty);
      Assert.That(created.Typography, Is.Empty);
      EnsureFolder(root + "/Editor");
      string editorPath = root + "/Editor/ThemeOverrides.asset";
      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreateThemeOverrideAsset(editorPath));
      Assert.That(File.Exists(editorPath), Is.False);
      Assert.That(File.Exists(editorPath + ".meta"), Is.False);
    }

    [Test]
    public void CreatingOverridesNeverAdoptsOrOverwritesOrphanMetadata()
    {
      string path = root + "/OccupiedOverrides.asset";
      string metadata = "fileFormatVersion: 2\nguid: " + Guid.NewGuid().ToString("N") + "\n";
      File.WriteAllText(path + ".meta", metadata);
      byte[] before = File.ReadAllBytes(path + ".meta");

      Assert.Throws<InvalidOperationException>(() => MenuStyleAuthoring.CreateThemeOverrideAsset(path));

      Assert.That(File.Exists(path), Is.False);
      Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(before));
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