using System;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace MToolKit.Runtime.VisualGraphs.Runtime.DTOs
{
  /// <summary>
  ///   Serializable representation of an AssetReference for runtime use.
  ///   Stores the GUID which can be used to load assets via Addressables.
  /// </summary>
  [Serializable]
  public sealed class SerializableAssetReference
  {
    public string AssetGuid { get; private set; }
    public string AssetType { get; private set; }
    public string RuntimeKey { get; private set; }

    public SerializableAssetReference() { }

    public SerializableAssetReference(string assetGuid, string assetType, string runtimeKey = null)
    {
      AssetGuid = assetGuid;
      AssetType = assetType;
      RuntimeKey = runtimeKey;
    }

#if UNITY_ADDRESSABLES
    public static SerializableAssetReference FromAssetReference(AssetReference assetReference)
    {
      if (assetReference == null || string.IsNullOrEmpty(assetReference.AssetGUID))
        return null;

      return new SerializableAssetReference
      {
        AssetGuid = assetReference.AssetGUID,
        AssetType = assetReference.GetType().Name,
        RuntimeKey = assetReference.RuntimeKey?.ToString()
      };
    }
#endif

    public bool IsValid => !string.IsNullOrEmpty(AssetGuid);

    public override string ToString()
    {
      return $"AssetRef[{AssetType}]: {AssetGuid ?? "null"}";
    }
  }
}
