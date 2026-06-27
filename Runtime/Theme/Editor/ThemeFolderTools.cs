#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Theme.Editor
{
  /// <summary>
  /// Deep-clones a theme FOLDER. A theme is really the folder — the <see cref="Theme"/> ScriptableObject
  /// is just the container inside it. Cloning copies the folder, then relinks every internal reference
  /// (Theme → registries → swatches / typesets / spacings) to the copies, so the clone is fully
  /// self-contained and the source is untouched. References to assets OUTSIDE the source folder
  /// (fonts, shared icons) are preserved.
  ///
  /// This exists because Unity's folder Duplicate / <see cref="AssetDatabase.CopyAsset(string,string)"/>
  /// does NOT relink: every copied asset keeps pointing at the originals, so retheming the clone would
  /// edit the source. Use this to spin up light / dark / high-contrast variants from a base theme.
  /// </summary>
  public static class ThemeFolderTools
  {
    /// <summary>
    /// Clone <paramref name="srcFolder"/> to a unique sibling named "{folder} Clone" and relink internal
    /// references. Returns the new folder path, or null on failure. Rename the result afterward freely —
    /// the relinked references are by GUID and survive folder/asset renames.
    /// </summary>
    public static string CloneThemeFolder(string srcFolder)
    {
      if (!AssetDatabase.IsValidFolder(srcFolder))
      {
        Debug.LogError($"[ThemeFolderTools] Not a folder: {srcFolder}");
        return null;
      }
      string parent = Path.GetDirectoryName(srcFolder).Replace("\\", "/");
      string name = Path.GetFileName(srcFolder);
      string dstFolder = AssetDatabase.GenerateUniqueAssetPath($"{parent}/{name} Clone");
      return CloneThemeFolderTo(srcFolder, dstFolder);
    }

    /// <summary>
    /// Clone <paramref name="srcFolder"/> to <paramref name="dstFolder"/> (must not already exist) and
    /// relink internal references. Returns <paramref name="dstFolder"/>, or null on failure.
    /// </summary>
    public static string CloneThemeFolderTo(string srcFolder, string dstFolder)
    {
      if (!AssetDatabase.IsValidFolder(srcFolder))
      {
        Debug.LogError($"[ThemeFolderTools] Not a folder: {srcFolder}");
        return null;
      }
      if (!AssetDatabase.CopyAsset(srcFolder, dstFolder))
      {
        Debug.LogError($"[ThemeFolderTools] CopyAsset failed: {srcFolder} -> {dstFolder}");
        return null;
      }
      AssetDatabase.Refresh();

      RelinkInternalReferences(srcFolder, dstFolder);
      ClearThemeIds(dstFolder);
      RegisterClonedThemes(srcFolder, dstFolder);

      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      return dstFolder;
    }

    /// <summary>
    /// Repoint every reference inside <paramref name="dstFolder"/> that targets an asset under
    /// <paramref name="srcFolder"/> to the mirrored copy under <paramref name="dstFolder"/> (matched by
    /// relative path). References pointing outside the source folder are left untouched. Safe to run
    /// standalone on an already-copied folder.
    /// </summary>
    public static void RelinkInternalReferences(string srcFolder, string dstFolder)
    {
      foreach (string guid in AssetDatabase.FindAssets("", new[] { dstFolder }))
      {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (AssetDatabase.IsValidFolder(path)) continue;
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        if (asset == null) continue;

        var so = new SerializedObject(asset);
        var p = so.GetIterator();
        bool changed = false;
        while (p.Next(true))  // Next(true) walks every property incl. array elements & hidden backing fields
        {
          if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
          var target = p.objectReferenceValue;
          if (target == null) continue;

          string targetPath = AssetDatabase.GetAssetPath(target).Replace("\\", "/");
          if (!targetPath.StartsWith(srcFolder + "/", StringComparison.Ordinal)) continue;

          string mirrored = dstFolder + targetPath.Substring(srcFolder.Length);
          var replacement = AssetDatabase.LoadAssetAtPath(mirrored, target.GetType());
          if (replacement != null) { p.objectReferenceValue = replacement; changed = true; }
        }
        if (changed) so.ApplyModifiedPropertiesWithoutUndo();
      }
      AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Blank the SemId of each <see cref="Theme"/> asset in <paramref name="dstFolder"/> so the clone
    /// does not share the source theme's identity (its Id falls back to the asset name until the user
    /// sets a new one). Element SemIds (swatch / typeset / spacing) are deliberately left intact — they
    /// are the shared semantic slots that MUST match across theme variants for a theme swap to work.
    /// </summary>
    private static void ClearThemeIds(string dstFolder)
    {
      foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(Theme)}", new[] { dstFolder }))
      {
        var theme = AssetDatabase.LoadAssetAtPath<Theme>(AssetDatabase.GUIDToAssetPath(guid));
        if (theme == null) continue;
        var so = new SerializedObject(theme);
        var semId = so.FindProperty("semId");
        if (semId != null) { semId.stringValue = ""; so.ApplyModifiedPropertiesWithoutUndo(); }
      }
    }

    /// <summary>
    /// Add each cloned Theme to the same <see cref="ThemeRegistry"/> that lists its source theme (matched
    /// by relative path), so the variant shows up in the global theme list without manual wiring.
    /// </summary>
    private static void RegisterClonedThemes(string srcFolder, string dstFolder)
    {
      foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(Theme)}", new[] { dstFolder }))
      {
        string clonePath = AssetDatabase.GUIDToAssetPath(guid);
        var clone = AssetDatabase.LoadAssetAtPath<Theme>(clonePath);
        var source = AssetDatabase.LoadAssetAtPath<Theme>(srcFolder + clonePath.Substring(dstFolder.Length));
        ThemeRegistryTools.Register(clone, source);
      }
    }

    // ── Project-window entry point ────────────────────────────────────────────

    [MenuItem("Assets/MToolKit/Clone Theme Folder (relink)", false, 20)]
    private static void CloneSelectedThemeFolder()
    {
      string folder = AssetDatabase.GetAssetPath(Selection.activeObject);
      int themeCount = AssetDatabase.FindAssets($"t:{nameof(Theme)}", new[] { folder }).Length;
      if (themeCount != 1 &&
          !EditorUtility.DisplayDialog("Clone theme folder",
            $"'{Path.GetFileName(folder)}' contains {themeCount} Theme assets. The whole folder will be " +
            "deep-copied and relinked. Continue?",
            "Clone", "Cancel"))
        return;

      string dst = CloneThemeFolder(folder);
      if (string.IsNullOrEmpty(dst)) return;

      var folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst);
      Selection.activeObject = folderObj;
      EditorGUIUtility.PingObject(folderObj);
    }

    [MenuItem("Assets/MToolKit/Clone Theme Folder (relink)", true)]
    private static bool CloneSelectedThemeFolderValidate()
    {
      return Selection.activeObject != null
        && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(Selection.activeObject));
    }
  }
}
#endif
