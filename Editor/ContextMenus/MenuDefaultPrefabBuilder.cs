using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.ContextMenus
{
  internal static class MenuDefaultPrefabBuilder
  {
    // Only new default outputs are generated. Existing/custom prefab contents are never rebuilt.
    internal static GameObject Create(Type presenterType, string path)
    {
      if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path) || System.IO.File.Exists(path + ".meta"))
        throw new InvalidOperationException("Refusing to overwrite a presenter prefab at " + path + ".");
      var scene = EditorSceneManager.NewPreviewScene();
      GameObject root = null;
      try
      {
        root = new GameObject("MTK " + presenterType.Name, typeof(RectTransform));
        root.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent(presenterType);
        root.GetComponent<RectTransform>().sizeDelta = Vector2.one;
        root.hideFlags = HideFlags.None;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        if (prefab == null) throw new InvalidOperationException("Unity could not save the native presenter prefab: " + path);
        return prefab;
      }
      finally
      {
        if (root != null) Object.DestroyImmediate(root);
        EditorSceneManager.ClosePreviewScene(scene);
      }
    }
  }
}
