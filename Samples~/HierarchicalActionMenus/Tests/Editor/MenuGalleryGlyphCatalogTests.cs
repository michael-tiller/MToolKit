using System;
using System.Collections.Generic;
using System.IO;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Samples.Editor;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MToolKit.ContextMenus.Samples.Tests.Editor
{
  public sealed class MenuGalleryGlyphCatalogTests
  {
    private string ownedRoot;

    [TearDown]
    public void RemoveOnlyOwnedCatalogs()
    {
      if (!string.IsNullOrEmpty(ownedRoot) && AssetDatabase.IsValidFolder(ownedRoot)) AssetDatabase.DeleteAsset(ownedRoot);
      ownedRoot = null;
    }

    [Test]
    public void Two_generated_catalogs_have_distinct_stable_TMP_names_and_resolve_together()
    {
      ownedRoot = "Assets/Temp/automation-scratch/MenuGlyphCatalogs-" + Guid.NewGuid().ToString("N");
      Folder(ownedRoot);
      var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
      Assert.That(font, Is.Not.Null, "Import the project's required TMP Essentials before native glyph authoring.");
      var caller = SceneManager.GetActiveScene();
      bool callerDirty = caller.isDirty;
      var marker = ScriptableObject.CreateInstance<MenuProfile>();
      string markerPath = ownedRoot + "/OwnedDirtyMarker.asset";
      AssetDatabase.CreateAsset(marker, markerPath); AssetDatabase.SaveAssetIfDirty(marker);
      string physicalMarker = Path.Combine(Path.GetDirectoryName(Application.dataPath), markerPath);
      string diskBefore = File.ReadAllText(physicalMarker);
      marker.ListWidth = 501; EditorUtility.SetDirty(marker);

      var first = MenuGalleryGlyphAuthoring.Create(ownedRoot + "/First", font);
      var second = MenuGalleryGlyphAuthoring.Create(ownedRoot + "/Second", font);
      var firstAssets = Atlases(first.Catalog);
      var secondAssets = Atlases(second.Catalog);
      Assert.That(firstAssets.Count, Is.GreaterThanOrEqualTo(2));
      Assert.That(secondAssets.Count, Is.GreaterThanOrEqualTo(2));
      var firstNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var atlas in firstAssets) firstNames.Add(atlas.name);
      foreach (var atlas in secondAssets)
        Assert.That(firstNames.Contains(atlas.name), Is.False,
          "Independent galleries must not collide in TMP's global case-insensitive sprite-name registry: " + atlas.name);
      foreach (var atlas in firstAssets) AssertStableAfterImport(atlas);
      foreach (var atlas in secondAssets) AssertStableAfterImport(atlas);

      using (var firstPrompts = new InputPromptService(first, localizer: new EnglishNames()))
      using (var secondPrompts = new InputPromptService(second, localizer: new EnglishNames()))
      {
        foreach (string path in new[] { "<Keyboard>/enter", "<Mouse>/rightButton", "<Gamepad>/buttonSouth", "<Gamepad>/select" })
        {
          string a = firstPrompts.ResolvePath(path, InputPromptStyle.Glyph);
          string b = secondPrompts.ResolvePath(path, InputPromptStyle.Glyph);
          Assert.That(a, Does.StartWith("<sprite=\""), "The first generated catalog must retain native TMP resolution for " + path);
          Assert.That(b, Does.StartWith("<sprite=\""), "The second generated catalog must resolve alongside the first for " + path);
          Assert.That(a, Is.Not.EqualTo(b), "Each prompt must name its own gallery's registered atlas.");
        }
      }
      Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(caller));
      Assert.That(caller.isDirty, Is.EqualTo(callerDirty));
      Assert.That(EditorUtility.IsDirty(marker), Is.True, "Glyph authoring must preserve an existing unsaved asset edit.");
      Assert.That(marker.ListWidth, Is.EqualTo(501));
      Assert.That(File.ReadAllText(physicalMarker), Is.EqualTo(diskBefore));
    }

    private static List<TMP_SpriteAsset> Atlases(InputGlyphCatalog catalog)
    {
      var result = new List<TMP_SpriteAsset>();
      foreach (var entry in catalog.Entries)
        if (entry?.SpriteAsset != null && !result.Contains(entry.SpriteAsset)) result.Add(entry.SpriteAsset);
      return result;
    }

    private static void AssertStableAfterImport(TMP_SpriteAsset atlas)
    {
      string name = atlas.name;
      string path = AssetDatabase.GetAssetPath(atlas);
      string guid = AssetDatabase.AssetPathToGUID(path);
      AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
      var imported = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);
      Assert.That(imported, Is.Not.Null);
      Assert.That(imported.name, Is.EqualTo(name), "The destination's serialized native TMP name must survive reimport.");
      Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
    }

    private static void Folder(string path)
    {
      string current = "Assets";
      foreach (string part in path.Substring("Assets/".Length).Split('/'))
      {
        string next = current + "/" + part;
        if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
        current = next;
      }
    }

    private sealed class EnglishNames : IInputPromptLocalizer, IInputPromptLocaleProvider
    {
      public string LocaleCode => "en";
      public string GetName(string key, string fallback) => fallback;
    }
  }
}
