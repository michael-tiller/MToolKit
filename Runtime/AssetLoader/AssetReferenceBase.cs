using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MToolKit.Runtime.AssetLoader
{
  /// <summary>
  ///   Generic base class for typed AssetReference
  /// </summary>
  public class AssetReferenceBase<T> : AssetReference where T : Object
  {
    protected AssetReferenceBase(string guid) : base(guid) { }
  }
}