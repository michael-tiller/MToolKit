#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MToolKit.Theme.Editor
{
  /// <summary>
  /// Keeps theme registries in sync when the configurator/tool creates or duplicates assets. A new
  /// <see cref="Theme"/> is appended to the <see cref="ThemeRegistry"/> that lists its source (or the
  /// nearest one above its folder); a new swatch / typeset / spacing is appended to the matching element
  /// registry in its theme folder. Best-effort: if no registry is found it does nothing. Registry arrays
  /// have private setters, so entries are appended via SerializedProperty.
  /// </summary>
  public static class ThemeRegistryTools
  {
    // element type -> (its registry type, the registry's array backing field)
    private static readonly Dictionary<Type, (Type registry, string arrayField)> Map = new()
    {
      [typeof(Theme)]                          = (typeof(ThemeRegistry),                            "<Themes>k__BackingField"),
      [typeof(MToolKit.Theme.Swatch.Swatch)]   = (typeof(MToolKit.Theme.Swatch.SwatchRegistry),    "<Swatches>k__BackingField"),
      [typeof(MToolKit.Theme.Typeset.Typeset)] = (typeof(MToolKit.Theme.Typeset.TypesetStyleRegistry), "<Typesets>k__BackingField"),
      [typeof(MToolKit.Theme.Spacing.Spacing)] = (typeof(MToolKit.Theme.Spacing.SpacingRegistry),   "<Spacings>k__BackingField"),
    };

    /// <summary>
    /// Append <paramref name="asset"/> to the appropriate registry. When <paramref name="source"/> is
    /// supplied (duplicate / clone), prefers the registry that already lists the source so the new entry
    /// lands next to it; otherwise walks up from the asset's folder to the nearest registry of the right
    /// type. Returns the registry it updated, or null if the type isn't registry-backed, no registry was
    /// found, or the asset was already present.
    /// </summary>
    public static ScriptableObject Register(ScriptableObject asset, ScriptableObject source = null)
    {
      if (asset == null || !Map.TryGetValue(asset.GetType(), out var info))
        return null;

      ScriptableObject registry =
        (source != null ? FindRegistryContaining(info.registry, info.arrayField, source) : null)
        ?? FindNearestRegistry(info.registry, AssetDatabase.GetAssetPath(asset));

      return registry != null && AppendUnique(registry, info.arrayField, asset) ? registry : null;
    }

    private static ScriptableObject FindRegistryContaining(Type registryType, string arrayField, ScriptableObject element)
    {
      foreach (string guid in AssetDatabase.FindAssets($"t:{registryType.Name}"))
      {
        var reg = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), registryType) as ScriptableObject;
        if (reg == null) continue;
        if (Contains(reg, arrayField, element)) return reg;
      }
      return null;
    }

    private static ScriptableObject FindNearestRegistry(Type registryType, string assetPath)
    {
      string dir = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
      while (!string.IsNullOrEmpty(dir) && (dir == "Assets" || dir.StartsWith("Assets/", StringComparison.Ordinal)))
      {
        foreach (string guid in AssetDatabase.FindAssets($"t:{registryType.Name}", new[] { dir }))
        {
          string path = AssetDatabase.GUIDToAssetPath(guid);
          if (Path.GetDirectoryName(path)?.Replace("\\", "/") == dir)  // directly in this folder, not a subfolder
            return AssetDatabase.LoadAssetAtPath(path, registryType) as ScriptableObject;
        }
        dir = Path.GetDirectoryName(dir)?.Replace("\\", "/");
      }
      return null;
    }

    private static bool Contains(ScriptableObject registry, string arrayField, ScriptableObject element)
    {
      var arr = new SerializedObject(registry).FindProperty(arrayField);
      if (arr == null || !arr.isArray) return false;
      for (int i = 0; i < arr.arraySize; i++)
        if (arr.GetArrayElementAtIndex(i).objectReferenceValue == element)
          return true;
      return false;
    }

    private static bool AppendUnique(ScriptableObject registry, string arrayField, ScriptableObject element)
    {
      var so = new SerializedObject(registry);
      var arr = so.FindProperty(arrayField);
      if (arr == null || !arr.isArray) return false;
      for (int i = 0; i < arr.arraySize; i++)
        if (arr.GetArrayElementAtIndex(i).objectReferenceValue == element)
          return false; // already present

      arr.arraySize++;  // new last slot copies the previous tail value; overwrite it below
      arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = element;
      so.ApplyModifiedPropertiesWithoutUndo();
      EditorUtility.SetDirty(registry);
      return true;
    }
  }
}
#endif
