using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using UnityEngine;

namespace MToolKit.Runtime.AssetLoader {
  /// <summary>
  /// This is the concrete implementation of the IAssetLoader for the Resources asset loader.
  /// </summary>
public sealed class ResourcesAssetLoader : IAssetLoader
    {
        public UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
            where T : Object
            => UniTask.FromResult(Resources.Load<T>(key));

        public UniTask<IList<T>> LoadAllAsync<T>(string label, CancellationToken ct = default)
            where T : Object
        {
            // Resources.LoadAll<T> returns T[] which implements IList<T>
            T[] arr = Resources.LoadAll<T>(label);
            return UniTask.FromResult((IList<T>)arr);
        }

        public void Release(Object asset) { Resources.UnloadAsset(asset); }

        public void ReleaseAll() { }
    }
  
}
