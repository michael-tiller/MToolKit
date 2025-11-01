using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Namespace for asset loader interfaces.
/// </summary>
namespace MToolKit.Runtime.AssetLoader.Interfaces {

  /// <summary>
  /// This interface provides a low-level API for loading assets from various sources.
  /// </summary>
public interface IAssetLoader
    {
        UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
            where T : Object;

        UniTask<IList<T>> LoadAllAsync<T>(string label, CancellationToken ct = default)
            where T : Object;

        void Release(Object asset);
        
        void ReleaseAll();
    }
}