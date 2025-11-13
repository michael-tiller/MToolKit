using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using ILogger = Serilog.ILogger;
using Object = UnityEngine.Object;

namespace MToolKit.Runtime.AssetLoader
{
  /// <summary>
  ///   This is the concrete implementation of the IContentLoaderService for the ContentLoaderService.
  /// </summary>
  public sealed class ContentLoaderService : IContentLoaderService, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ContentLoaderService>().ForFeature("Assets"));

    private readonly IAssetLoader assetLoader;
    private readonly Dictionary<string, Object> cachedAssets = new();
    private readonly Dictionary<string, AsyncOperationHandle> cachedHandles = new();
    private readonly Dictionary<string, IResourceLocator> catalogLocators = new();
    private readonly Dictionary<string, List<Object>> preloadedAssets = new();

    public ContentLoaderService(IAssetLoader assetLoader)
    {
      this.assetLoader = assetLoader;
    }

    private static ILogger log => logLazy.Value ?? Logger.None;

    #region IContentLoaderService Members

    public ReactiveProperty<float> Progress { get; } = new(0f);

    public async UniTask InitializeAsync(CancellationToken ct = default)
    {
      log.ForMethod().Verbose("Initializing ContentLoaderService with Addressables");

      try
      {
        AsyncOperationHandle<IResourceLocator> initHandle = Addressables.InitializeAsync();
        log.ForMethod().Debug("Addressables.InitializeAsync called, waiting for completion...");

        await initHandle.ToUniTask(cancellationToken: ct);

        log.ForMethod().Verbose("Addressables initialized successfully. Status: {Status}", initHandle.Status);

        // Log all available resource locators
        IEnumerable<IResourceLocator> locators = Addressables.ResourceLocators;
        List<IResourceLocator> resourceLocators = locators.ToList();
        log.ForMethod().Debug("Available resource locators: {Count}", resourceLocators.Count());
        foreach (IResourceLocator locator in resourceLocators)
          log.ForMethod().Debug("Locator: {Locator}, Keys: {Keys}", locator, locator.Keys.Count());
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize Addressables");
        throw;
      }
    }

    public async UniTask LoadCatalogAsync(string url, CancellationToken ct = default)
    {
      if (catalogLocators.ContainsKey(url))
        return;

      AsyncOperationHandle<IResourceLocator> handle = Addressables.LoadContentCatalogAsync(url);
      await handle.ToUniTask(cancellationToken: ct);

      if (handle is {Status:AsyncOperationStatus.Succeeded,Result: not null})
        catalogLocators[url] = handle.Result;
      else
        throw new InvalidOperationException($"Failed to load catalog: {url}");
    }

    public UniTask UnloadCatalogAsync(string url)
    {
      if (catalogLocators.TryGetValue(url, out IResourceLocator locator))
      {
        Addressables.RemoveResourceLocator(locator);
        catalogLocators.Remove(url);
      }
      return UniTask.CompletedTask;
    }

    public async UniTask LoadRemoteCatalogAsync(string baseUrl, string catalogName, CancellationToken ct = default)
    {
      string fullUrl = $"{baseUrl}/{catalogName}.json";
      await LoadCatalogAsync(fullUrl, ct);

      // Optional: pre-download dependencies
      await Addressables.DownloadDependenciesAsync(catalogName)
        .ToUniTask(cancellationToken: ct);
    }

    public async UniTask<bool> CheckForUpdateAsync(CancellationToken ct = default)
    {
      AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates(false);
      await handle.ToUniTask(cancellationToken: ct);
      if (handle.Status != AsyncOperationStatus.Succeeded) return false;

      List<string> updates = handle.Result;
      if (updates.Count == 0) return false;

      AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs(updates);
      await updateHandle.ToUniTask(cancellationToken: ct);
      return true;
    }

    public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : Object
    {
      return await assetLoader.LoadAsync<T>(key, ct);
    }

    public async UniTask<T> LoadCachedAsync<T>(string key, CancellationToken ct = default)
      where T : Object
    {
      if (cachedAssets.TryGetValue(key, out Object existing))
        return existing as T;

      T asset = await assetLoader.LoadAsync<T>(key, ct);
      cachedAssets[key] = asset;
      return asset;
    }

    public void ReleaseCached(string key)
    {
      if (cachedAssets.TryGetValue(key, out Object asset))
      {
        assetLoader.Release(asset);
        cachedAssets.Remove(key);
      }
    }

    public async UniTask PreloadLabelAsync(string label, CancellationToken ct = default)
    {
      log.ForMethod().Debug("PreloadLabelAsync called for label: {Label}", label);

      if (preloadedAssets.ContainsKey(label))
      {
        log.ForMethod().Verbose("Label {Label} already preloaded, skipping", label);
        Progress.Value = 1f;
        return;
      }

      Progress.Value = 0f;

      // Load assets with progress tracking (this will download dependencies automatically)
      log.ForMethod().Verbose("Loading assets for label: {Label}", label);
      List<Object> assets = await LoadAssetsWithProgressAsync(label, ct);
      log.ForMethod().Debug("Loaded {Count} assets for label: {Label}", assets.Count, label);
      Progress.Value = 1f;

      preloadedAssets[label] = new List<Object>(assets);
    }

    public async UniTask CacheDependenciesAsync(string label, CancellationToken ct = default)
    {
      // For labels, we need to get the list of assets first, then download their dependencies
      AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = Addressables.LoadResourceLocationsAsync(label);
      await locationsHandle.ToUniTask(cancellationToken: ct);

      if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
        throw new InvalidOperationException($"Failed to load resource locations for label: {label}");

      List<object> keys = new();
      foreach (IResourceLocation location in locationsHandle.Result)
        keys.Add(location.PrimaryKey);

      if (keys.Count > 0)
      {
        AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union);
        while (!handle.IsDone && !ct.IsCancellationRequested)
          await UniTask.Yield(ct);

        if (handle.Status == AsyncOperationStatus.Succeeded)
          cachedHandles[label] = handle;
        else
          throw new InvalidOperationException($"Failed to cache dependencies for {label}");
      }
    }

    public void ReleaseDependencies(string label)
    {
      if (cachedHandles.TryGetValue(label, out AsyncOperationHandle handle))
      {
        Addressables.Release(handle);
        cachedHandles.Remove(label);
      }
    }

    public void ClearAllCaches()
    {
      // Release all cached dependencies
      foreach (string label in cachedHandles.Keys.ToList())
        ReleaseDependencies(label);

      // Release all cached assets
      foreach (string key in cachedAssets.Keys.ToList())
        ReleaseCached(key);

      // Clear preloaded assets
      preloadedAssets.Clear();
    }

    public void Release(Object asset)
    {
      assetLoader.Release(asset);
    }

    public void ReleasePreloadedLabel(string label)
    {
      if (!preloadedAssets.TryGetValue(label, out List<Object> assets))
        return;

      foreach (Object asset in assets)
        assetLoader.Release(asset);

      preloadedAssets.Remove(label);

      // Also release cached dependencies for this label
      ReleaseDependencies(label);
    }

    public void ReleaseAll()
    {
      // Release all tracked handles from the asset loader
      assetLoader.ReleaseAll();
    }

    public async UniTask LoadSceneAsync(AssetReference sceneReference, CancellationToken ct = default)
    {
      if (sceneReference == null || !sceneReference.RuntimeKeyIsValid())
      {
        log.ForMethod().Error("Invalid scene reference");
        throw new ArgumentException("Invalid AssetReference provided");
      }

      log.ForMethod().Debug("Loading scene via Addressables: {Scene}", sceneReference.RuntimeKey);

      AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(sceneReference);

      while (!handle.IsDone && !ct.IsCancellationRequested)
        await UniTask.Yield(ct);

      if (handle.Status == AsyncOperationStatus.Succeeded)
      {
        log.ForMethod().Information("Successfully loaded scene: {Scene}", sceneReference.RuntimeKey);
      }
      else
      {
        log.ForMethod().Error("Failed to load scene: {Scene}, Status: {Status}", sceneReference.RuntimeKey, handle.Status);
        throw new InvalidOperationException($"Failed to load scene: {sceneReference.RuntimeKey}, Status: {handle.Status}");
      }
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      foreach (IResourceLocator locator in catalogLocators.Values)
        Addressables.RemoveResourceLocator(locator);

      catalogLocators.Clear();

      // Clear all caches (includes preloaded assets, cached assets, and dependency handles)
      ClearAllCaches();

      // Release all active handles
      ReleaseAll();
    }

    #endregion

    private async UniTask<T> LoadAssetAsync<T>(string key, CancellationToken ct) where T : Object
    {
      try
      {
        return await assetLoader.LoadAsync<T>(key, ct);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to load asset with key: {Key}", key);
        return null;
      }
    }

    private async UniTask<List<Object>> LoadAssetsWithProgressAsync(string label, CancellationToken ct)
    {
      if (assetLoader is AddressablesAssetLoader)
      {
        // Get all locations for this label using the old Locate API which works in Editor
        log.ForMethod().Debug("Loading resource locations for label: {Label}", label);

        List<IResourceLocation> locationsToLoad = new();

        // Use the old Locate API which works properly in Editor
        foreach (IResourceLocator locator in Addressables.ResourceLocators)
          if (locator.Locate(label, null, out IList<IResourceLocation> locations))
            foreach (IResourceLocation location in locations)
            {
              // Filter out scenes and other non-loadable asset types
              // Only load assets that can be loaded as UnityEngine.Object
              bool isScene = location.ResourceType == typeof(SceneInstance);
              bool isConfigAsset = location.PrimaryKey.Contains(".asset") &&
                                   !location.PrimaryKey.Contains("Prefab") &&
                                   !location.PrimaryKey.Contains("ScriptableObject");

              if (!isScene && !isConfigAsset)
                locationsToLoad.Add(location);
              else
                log.ForMethod().Debug("Skipping {Type} asset: {Key}",
                  isScene ? "scene" : "config",
                  location.PrimaryKey);
            }

        if (locationsToLoad.Count == 0)
        {
          log.ForMethod().Warning("No loadable assets found for label: {Label}", label);
          return new List<Object>();
        }

        log.ForMethod().Information("Found {Count} loadable assets for label: {Label}", locationsToLoad.Count, label);

        // Load all assets in parallel using UniTask.WhenAll
        List<UniTask<Object>> loadTasks = new();

        foreach (IResourceLocation location in locationsToLoad)
        {
          string key = location.PrimaryKey;

          loadTasks.Add(LoadAssetAsync<Object>(key, ct));
        }

        // Wait for all assets to load in parallel
        Object[] results = await UniTask.WhenAll(loadTasks);

        // Filter out null results (failed loads)
        List<Object> loadedAssets = results.OfType<Object>().Where(a => a != null).ToList();
        Progress.Value = 1f;

        log.ForMethod().Information("Successfully loaded {Count} assets for label: {Label}", loadedAssets.Count, label);
        return loadedAssets;
      }
      // Fallback for Resources
      IList<Object> assets = await assetLoader.LoadAllAsync<Object>(label, ct);
      Progress.Value = 1f;
      return new List<Object>(assets);
    }
  }
}