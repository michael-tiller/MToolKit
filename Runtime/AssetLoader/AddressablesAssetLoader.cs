using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using Serilog;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace MToolKit.Runtime.AssetLoader
{
  /// <summary>
  ///   This is the concrete implementation of the IAssetLoader for the Addressables asset loader.
  /// </summary>
  public sealed class AddressablesAssetLoader : IAssetLoader, IDisposable
  {
    private sealed class Acquisition
    {
      public readonly AsyncOperationHandle Handle;
      public int RemainingShares;
      public Acquisition(AsyncOperationHandle handle, int shares)
      { Handle = handle; RemainingShares = shares; }
    }

    // Each successful load owns one acquired handle reference, even when Addressables returns
    // the same underlying operation/asset. A grouped load shares that one reference across its assets.
    private readonly Dictionary<Object, List<Acquisition>> assetOwners = new();
    private readonly HashSet<Acquisition> acquisitions = new();
    private readonly object @lock = new();
    private static readonly Lazy<ILogger> logger = new(() => Log.ForContext<AddressablesAssetLoader>()
      .ForContext("Feature", "AssetLoader"));

    private static async UniTask<TResult> AwaitAndRegisterAsync<TResult>(AsyncOperationHandle<TResult> handle,
      CancellationToken cancellationToken, Action<AsyncOperationHandle<TResult>> register)
    {
      bool transferred = false;
      try
      {
        // This loader owns the acquired reference until registration succeeds, including failures
        // and cancellation before UniTask installs a completion callback.
        var result = await handle.ToUniTask(cancellationToken: cancellationToken, autoReleaseWhenCanceled: false);
        if (handle.Status != AsyncOperationStatus.Succeeded)
          throw new InvalidOperationException("Addressable asset load did not succeed.");
        register(handle);
        transferred = true;
        return result;
      }
      finally
      {
        if (!transferred && handle.IsValid()) Addressables.Release(handle);
      }
    }

    #region IAssetLoader Members

    public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
      where T : Object
    {
      ct.ThrowIfCancellationRequested();
      AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
      return await AwaitAndRegisterAsync(handle, ct, RegisterLoadedAsset<T>);
    }

    private void RegisterLoadedAsset<T>(AsyncOperationHandle<T> operation) where T : Object
    {
      var asset = operation.Result;
      if (asset == null) throw new InvalidOperationException("A successful asset operation returned no live asset.");
      lock (@lock)
      {
        var acquisition = new Acquisition(operation, 1);
        acquisitions.Add(acquisition);
        AddShare(asset, acquisition);
      }
    }

    public async UniTask<IList<T>> LoadAllAsync<T>(string label, CancellationToken ct = default)
      where T : Object
    {
      ct.ThrowIfCancellationRequested();
      AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label);
      return await AwaitAndRegisterAsync(handle, ct, RegisterLoadedAssets<T>);
    }

    private void RegisterLoadedAssets<T>(AsyncOperationHandle<IList<T>> operation) where T : Object
    {
      var result = operation.Result;
      if (result == null) throw new InvalidOperationException("A successful asset group operation returned no collection.");
      // Validate the complete group before mutating ownership, so a bad item cannot leave partial entries.
      var assets = new Object[result.Count];
      for (int i = 0; i < assets.Length; i++)
      {
        if (result[i] == null) throw new InvalidOperationException("A successful asset group contains a missing or destroyed asset.");
        assets[i] = result[i];
      }
      if (assets.Length == 0)
      {
        // There is no asset through which a caller could return this acquired reference.
        if (operation.IsValid()) Addressables.Release(operation);
        return;
      }
      lock (@lock)
      {
        var acquisition = new Acquisition(operation, assets.Length);
        acquisitions.Add(acquisition);
        for (int i = 0; i < assets.Length; i++) AddShare(assets[i], acquisition);
      }
    }

    private void AddShare(Object asset, Acquisition acquisition)
    {
      if (!assetOwners.TryGetValue(asset, out var owners))
      { owners = new List<Acquisition>(); assetOwners.Add(asset, owners); }
      owners.Add(acquisition);
    }

    public void Release(Object asset)
    {
      // A destroyed Unity object can still have an owned managed identity that needs releasing.
      if (ReferenceEquals(asset, null)) return;

      Acquisition released = null;
      try
      {
        lock (@lock)
        {
          if (!assetOwners.TryGetValue(asset, out var owners) || owners.Count == 0) return;
          // The asset-only public API returns one acquisition share per call. Preserve every other owner.
          int last = owners.Count - 1;
          var acquisition = owners[last];
          owners.RemoveAt(last);
          if (owners.Count == 0) assetOwners.Remove(asset);
          if (--acquisition.RemainingShares == 0 && acquisitions.Remove(acquisition)) released = acquisition;
        }
        if (released != null) ReleaseHandle(released);
      }
      catch (Exception exception)
      {
        logger.Value.Warning(exception, "Failed to return an asset ownership share");
      }
    }

    public void ReleaseAll()
    {
      List<Acquisition> owned;
      lock (@lock)
      {
        owned = new List<Acquisition>(acquisitions);
        assetOwners.Clear(); acquisitions.Clear();
      }
      // Release outside the lock after detaching ownership: completion/destruction callbacks can reenter.
      for (int i = 0; i < owned.Count; i++) ReleaseHandle(owned[i]);
    }

    private static void ReleaseHandle(Acquisition acquisition)
    {
      try { if (acquisition.Handle.IsValid()) Addressables.Release(acquisition.Handle); }
      catch (Exception exception)
      {
        logger.Value.Warning(exception, "Failed to release asset acquisition {OperationId}", acquisition.Handle.GetHashCode());
      }
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      ReleaseAll();
    }

    #endregion
  }
}
