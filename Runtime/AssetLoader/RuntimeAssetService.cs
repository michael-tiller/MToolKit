using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using UnityEngine;

namespace MToolKit.Runtime.AssetLoader {

  /// <summary>
  /// This is the concrete implementation of the IRuntimeAssetService for gameplay code.
  /// </summary>
  public sealed class RuntimeAssetService : IRuntimeAssetService
{
  private readonly IAssetLoader _loader;

        public RuntimeAssetService(IAssetLoader loader)
        {
            _loader = loader;
        }

        public UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
            where T : Object
            => _loader.LoadAsync<T>(key, ct);

        public void Release(Object asset)
            => _loader.Release(asset);
}
}