using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using UnityEngine;

namespace MToolKit.Runtime.AssetLoader
{
  /// <summary>
  ///   This is the concrete implementation of the IRuntimeAssetService for gameplay code.
  /// </summary>
  public sealed class RuntimeAssetService : IRuntimeAssetService
  {
    private readonly IAssetLoader loader;

    public RuntimeAssetService(IAssetLoader loader)
    {
      this.loader = loader;
    }

    #region IRuntimeAssetService Members

    public UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
      where T : Object
    {
      return loader.LoadAsync<T>(key, ct);
    }

    public void Release(Object asset)
    {
      loader.Release(asset);
    }

    #endregion
  }
}