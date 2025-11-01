using UnityEngine.AddressableAssets;
using MToolKit.Runtime.Core.Abstractions;
using System;

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
  public class AssetReferenceScene : AssetReference {
    public AssetReferenceScene(string guid) : base(guid) { }
  }
}