using UnityEngine.AddressableAssets;
using MToolKit.Runtime.Core.Abstractions;
using System;
using UnityEngine;

namespace MToolKit.Runtime.AssetLoader {

  /// <summary>
  /// Concrete implementation of the AssetReference for AbstractGamePlugin
  /// </summary>
  [Serializable]
  public class AssetReferenceGamePlugin : AssetReferenceBase<AbstractGamePlugin> {
    public AssetReferenceGamePlugin(string guid) : base(guid) { }
  }

  /// <summary>
  /// Type-safe AssetReference specifically for Scene assets
  /// </summary>
  [Serializable]
  public class AssetReferenceScene : AssetReference, ISerializationCallbackReceiver {
    // A scene GUID cannot be resolved in a player: AssetDatabase is editor-only and
    // built-in scenes are not Addressables catalog entries. The name/path are baked
    // here whenever the containing asset serializes in the editor (including player
    // build serialization) so runtime scene matching can compare them directly.
    [SerializeField] private string sceneName;
    [SerializeField] private string scenePath;

    public string SceneName => sceneName;
    public string ScenePath => scenePath;

    public AssetReferenceScene(string guid) : base(guid) { }

    public void OnBeforeSerialize() {
#if UNITY_EDITOR
      string path = UnityEditor.AssetDatabase.GUIDToAssetPath(AssetGUID);
      if (!string.IsNullOrEmpty(path)) {
        scenePath = path;
        sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
      }
#endif
    }

    public void OnAfterDeserialize() { }
  }
}
