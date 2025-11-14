using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
using Object = UnityEngine.Object;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MToolKit.Runtime.VisualGraphs.Runtime.AssetLoading
{
  /// <summary>
  ///   Default implementation of IGraphAssetLoader using Unity Addressables.
  /// </summary>
  public sealed class GraphAssetLoader : IGraphAssetLoader, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GraphAssetLoader>().ForFeature("VisualGraphs.Assets"));
    private static ILogger log => logLazy.Value ?? Logger.None;

#if UNITY_ADDRESSABLES
    private readonly Dictionary<string, AsyncOperationHandle> loadedAssets = new();
#endif

    public GraphAssetLoader()
    {
    }

    public async UniTask<T> LoadAssetAsync<T>(SerializableAssetReference assetRef, CancellationToken ct = default)
      where T : Object
    {
#if !UNITY_ADDRESSABLES
      log.ForMethod().Error("Addressables package not installed. Cannot load asset {AssetGuid}", assetRef?.AssetGuid);
      await UniTask.CompletedTask;
      return default;
#else
      if (assetRef == null || !assetRef.IsValid)
      {
        log.ForMethod().Warning("Attempted to load invalid asset reference");
        return default;
      }

      try
      {
        // Check if already loaded
        if (loadedAssets.TryGetValue(assetRef.AssetGuid, out var existingHandle))
        {
          if (existingHandle.IsValid() && existingHandle.IsDone)
          {
            log.ForMethod().Debug("Asset {AssetGuid} already loaded, returning cached reference", assetRef.AssetGuid);
            return existingHandle.Result as T;
          }
        }

        // Load via Addressables using GUID
        log.ForMethod().Debug("Loading asset {AssetGuid} of type {AssetType}", assetRef.AssetGuid, assetRef.AssetType);
        
        var handle = Addressables.LoadAssetAsync<T>(assetRef.AssetGuid);
        var result = await handle.ToUniTask(cancellationToken: ct);

        // Cache the handle for later unloading
        loadedAssets[assetRef.AssetGuid] = handle;

        log.ForMethod().Information("Successfully loaded asset {AssetGuid}", assetRef.AssetGuid);
        return result;
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Debug("Asset load cancelled for {AssetGuid}", assetRef.AssetGuid);
        throw;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load asset {AssetGuid} of type {AssetType}", assetRef.AssetGuid, assetRef.AssetType);
        return default;
      }
#endif
    }

    public void UnloadAsset(SerializableAssetReference assetRef)
    {
#if UNITY_ADDRESSABLES
      if (assetRef == null || !assetRef.IsValid)
        return;

      if (loadedAssets.TryGetValue(assetRef.AssetGuid, out var handle))
      {
        if (handle.IsValid())
        {
          log.ForMethod().Debug("Unloading asset {AssetGuid}", assetRef.AssetGuid);
          Addressables.Release(handle);
        }

        loadedAssets.Remove(assetRef.AssetGuid);
      }
#endif
    }

    public bool IsValid(SerializableAssetReference assetRef)
    {
      return assetRef != null && assetRef.IsValid;
    }

    public void Dispose()
    {
#if UNITY_ADDRESSABLES
      log.ForMethod().Debug("Disposing GraphAssetLoader, releasing {Count} loaded assets", loadedAssets.Count);

      foreach (var handle in loadedAssets.Values)
      {
        if (handle.IsValid())
          Addressables.Release(handle);
      }

      loadedAssets.Clear();
#endif
    }
  }
}