using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;
using MToolKit.Runtime.AssetLoader.Interfaces;

namespace MToolKit.Runtime.AssetLoader { 
  /// <summary>
  /// This is the concrete implementation of the IAssetLoader for the Addressables asset loader.
  /// </summary>
public sealed class AddressablesAssetLoader : IAssetLoader, IDisposable
{ 
    private readonly Dictionary<UnityEngine.Object, AsyncOperationHandle> _handles = new();
    private readonly Dictionary<AsyncOperationHandle, int> _handleRefCounts = new();
        private readonly object _lock = new();

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(key);
            await handle.ToUniTask(cancellationToken: ct);
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Failed to load key: {key}");

            lock (_lock)
            {
                _handles[handle.Result] = handle;
                _handleRefCounts[handle] = 1;
            }

            return handle.Result;
        }

        public async UniTask<IList<T>> LoadAllAsync<T>(string label, CancellationToken ct = default)
            where T :  UnityEngine.Object
        {
            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            await handle.ToUniTask(cancellationToken: ct);
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Failed to load label: {label}");

            lock (_lock)
            {
                int assetCount = handle.Result.Count;
                _handleRefCounts[handle] = assetCount;
                
                foreach (var asset in handle.Result)
                    _handles[asset] = handle;
            }

            return handle.Result;
        }

        public void Release( UnityEngine.Object asset)
        {
            if (asset == null) return;

            lock (_lock)
            {
                if (_handles.TryGetValue(asset, out var handle))
                {
                    _handles.Remove(asset);
                    
                    if (_handleRefCounts.TryGetValue(handle, out var refCount))
                    {
                        refCount--;
                        if (refCount <= 0)
                        {
                            Addressables.Release(handle);
                            _handleRefCounts.Remove(handle);
                        }
                        else
                        {
                            _handleRefCounts[handle] = refCount;
                        }
                    }
                }
            }
        }

        public void ReleaseAll()
        {
            lock (_lock)
            {
                // Collect unique handles from the dictionary to avoid releasing the same handle multiple times
                var uniqueHandles = new HashSet<AsyncOperationHandle>(_handles.Values);
                foreach (var handle in uniqueHandles)
                    Addressables.Release(handle);
                
                _handles.Clear();
                _handleRefCounts.Clear();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                // Collect unique handles from the dictionary to avoid releasing the same handle multiple times
                var uniqueHandles = new HashSet<AsyncOperationHandle>(_handles.Values);
                foreach (var handle in uniqueHandles)
                    Addressables.Release(handle);
                
                _handles.Clear();
                _handleRefCounts.Clear();
            }
        }
    }
}