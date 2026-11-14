using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MToolKit.Editor.SpriteAuthoring
{
  public readonly struct SpriteRename
  {
    public Sprite SourceSprite { get; }
    public string NewName { get; }
    public SpriteRename(Sprite sourceSprite, string newName)
    { SourceSprite = sourceSprite; NewName = newName; }
  }

  /// <summary>Optional native 2D Sprite Editor authoring; players do not depend on this assembly.</summary>
  public static class SpriteRenameUtility
  {
    private sealed class SheetPlan
    {
      public string Path;
      public TextureImporter Importer;
      public ISpriteEditorDataProvider Provider;
      public ISpriteNameFileIdDataProvider Names;
      public SpriteRect[] Rects;
      public readonly Dictionary<GUID, string> Renames = new Dictionary<GUID, string>();
      public int Changed;
    }

    /// <summary>
    /// Renames native multiple-sprite slices without changing their sprite IDs, geometry or source image.
    /// Every requested sheet and final name is checked before writing any importer. Returns the changed slice count.
    /// </summary>
    public static int Rename(IReadOnlyList<SpriteRename> renames)
    {
      if (renames == null) throw new ArgumentNullException(nameof(renames));
      var factories = new SpriteDataProviderFactories();
      factories.Init();
      var sheets = new Dictionary<string, SheetPlan>(StringComparer.Ordinal);
      foreach (var rename in renames)
      {
        if (rename.SourceSprite == null || string.IsNullOrWhiteSpace(rename.NewName) || rename.NewName.Any(char.IsControl))
          throw new InvalidOperationException("Each rename needs a saved source sprite and a nonempty name without control characters.");
        string path = AssetDatabase.GetAssetPath(rename.SourceSprite);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
          throw new InvalidOperationException("Source sprites must belong to editable project texture assets: " + rename.SourceSprite.name);
        if (!sheets.TryGetValue(path, out var sheet))
        {
          var importer = AssetImporter.GetAtPath(path) as TextureImporter;
          if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple)
            throw new InvalidOperationException("Native sprite renaming requires a multiple-sprite texture importer: " + path);
          var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
          if (provider == null) throw new InvalidOperationException("No native Sprite Editor data provider exists for " + path);
          provider.InitSpriteEditorDataProvider();
          var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
          if (names == null) throw new InvalidOperationException("The native sprite provider cannot preserve name/file-ID pairs for " + path);
          sheet = new SheetPlan { Path = path, Importer = importer, Provider = provider, Names = names,
            Rects = provider.GetSpriteRects() };
          sheets.Add(path, sheet);
        }
        var id = rename.SourceSprite.GetSpriteID();
        if (id.Empty() || !sheet.Rects.Any(rect => rect.spriteID == id))
          throw new InvalidOperationException("The source sprite does not match a native slice in " + path + ": " + rename.SourceSprite.name);
        if (sheet.Renames.ContainsKey(id))
          throw new InvalidOperationException("The same source sprite was requested more than once: " + rename.SourceSprite.name);
        sheet.Renames.Add(id, rename.NewName);
      }

      // Include unmodified slices so a collision on the final sheet cannot partially rename an earlier sheet.
      foreach (var sheet in sheets.Values)
      {
        var finalNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rect in sheet.Rects)
        {
          string name = sheet.Renames.TryGetValue(rect.spriteID, out var renamed) ? renamed : rect.name;
          if (!finalNames.Add(name)) throw new InvalidOperationException("Duplicate final sprite name in " + sheet.Path + ": " + name);
          if (!string.Equals(name, rect.name, StringComparison.Ordinal)) sheet.Changed++;
        }
      }

      int changed = 0;
      foreach (var sheet in sheets.Values.OrderBy(plan => plan.Path, StringComparer.Ordinal))
      {
        if (sheet.Changed == 0) continue;
        foreach (var rect in sheet.Rects)
          if (sheet.Renames.TryGetValue(rect.spriteID, out var name)) rect.name = name;
        sheet.Provider.SetSpriteRects(sheet.Rects);
        // Reuse native GUIDs, allowing the provider to retain each imported Sprite's existing local file ID.
        sheet.Names.SetNameFileIdPairs(sheet.Rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        sheet.Provider.Apply();
        sheet.Importer.SaveAndReimport();
        changed += sheet.Changed;
      }
      return changed;
    }
  }
}
