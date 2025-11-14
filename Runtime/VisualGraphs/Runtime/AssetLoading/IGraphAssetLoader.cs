using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Runtime.AssetLoading
{
  /// <summary>
  ///   Loads assets referenced in graphs at runtime using Addressables.
  /// </summary>
  public interface IGraphAssetLoader
  {
    UniTask<T> LoadAssetAsync<T>(SerializableAssetReference assetRef, CancellationToken ct = default)
      where T : Object;

    void UnloadAsset(SerializableAssetReference assetRef);

    bool IsValid(SerializableAssetReference assetRef);
  }
}