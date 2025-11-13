using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
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
    private readonly Dictionary<AsyncOperationHandle, int> handleRefCounts = new();
    private readonly Dictionary<Object, AsyncOperationHandle> handles = new();
    private readonly object @lock = new();

    #region IAssetLoader Members

    public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
      where T : Object
    {
      AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
      await handle.ToUniTask(cancellationToken: ct);
      if (handle.Status != AsyncOperationStatus.Succeeded)
        throw new InvalidOperationException($"Failed to load key: {key}");

      lock (@lock)
      {
        handles[handle.Result] = handle;
        handleRefCounts[handle] = 1;
      }

      return handle.Result;
    }

    public async UniTask<IList<T>> LoadAllAsync<T>(string label, CancellationToken ct = default)
      where T : Object
    {
      AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label);
      await handle.ToUniTask(cancellationToken: ct);
      if (handle.Status != AsyncOperationStatus.Succeeded)
        throw new InvalidOperationException($"Failed to load label: {label}");

      lock (@lock)
      {
        int assetCount = handle.Result.Count;
        handleRefCounts[handle] = assetCount;

        foreach (T asset in handle.Result)
          handles[asset] = handle;
      }

      return handle.Result;
    }

    public void Release(Object asset)
    {
      if (asset == null) return;

      lock (@lock)
      {
        if (handles.TryGetValue(asset, out AsyncOperationHandle handle))
        {
          handles.Remove(asset);

          if (handleRefCounts.TryGetValue(handle, out int refCount))
          {
            refCount--;
            if (refCount <= 0)
            {
              Addressables.Release(handle);
              handleRefCounts.Remove(handle);
            }
            else
            {
              handleRefCounts[handle] = refCount;
            }
          }
        }
      }
    }

    public void ReleaseAll()
    {
      lock (@lock)
      {
        // Collect unique handles from the dictionary to avoid releasing the same handle multiple times
        HashSet<AsyncOperationHandle> uniqueHandles = new(handles.Values);
        foreach (AsyncOperationHandle handle in uniqueHandles)
          Addressables.Release(handle);

        handles.Clear();
        handleRefCounts.Clear();
      }
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      lock (@lock)
      {
        // Collect unique handles from the dictionary to avoid releasing the same handle multiple times
        HashSet<AsyncOperationHandle> uniqueHandles = new(handles.Values);
        foreach (AsyncOperationHandle handle in uniqueHandles)
          Addressables.Release(handle);

        handles.Clear();
        handleRefCounts.Clear();
      }
    }

    #endregion
  }
}