using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MToolKit.Runtime.AssetLoader.Interfaces
{
  /// <summary>
  ///   A higher-level service that wraps IAssetLoader for gameplay code. It provides a simplified interface for loading assets.
  /// </summary>
  public interface IRuntimeAssetService
  {
    UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : Object;
    void Release(Object asset);
  }
}