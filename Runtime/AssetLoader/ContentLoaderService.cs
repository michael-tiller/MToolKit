using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using R3;
using Serilog;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.AssetLoader { 
  /// <summary>
  /// This is the concrete implementation of the IContentLoaderService for the ContentLoaderService.
  /// </summary>

  public sealed class ContentLoaderService : IContentLoaderService, IDisposable
{
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ContentLoaderService>().ForFeature("Assets"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private readonly IAssetLoader _assetLoader;
    private readonly Dictionary<string, IResourceLocator> _catalogLocators = new();
    private readonly Dictionary<string, List<UnityEngine.Object>> _preloadedAssets = new();
    private readonly Dictionary<string, UnityEngine.Object> _cachedAssets = new();
    private readonly Dictionary<string, AsyncOperationHandle> _cachedHandles = new();

    public ReactiveProperty<float> Progress { get; } = new(0f);

    public ContentLoaderService(IAssetLoader assetLoader)
    {
        _assetLoader = assetLoader;
    }

    public async UniTask InitializeAsync(CancellationToken ct = default)
    {
        log.ForMethod(nameof(InitializeAsync)).Verbose("Initializing ContentLoaderService with Addressables");
        
        try
        {
            var initHandle = Addressables.InitializeAsync();
            log.ForMethod(nameof(InitializeAsync)).Debug("Addressables.InitializeAsync called, waiting for completion...");
            
            await initHandle.ToUniTask(cancellationToken: ct);
            
            log.ForMethod(nameof(InitializeAsync)).Verbose("Addressables initialized successfully. Status: {Status}", initHandle.Status);
            
            // Log all available resource locators
            var locators = Addressables.ResourceLocators;
            log.ForMethod(nameof(InitializeAsync)).Debug("Available resource locators: {Count}", locators.Count());
            foreach (var locator in locators)
            {
                log.ForMethod(nameof(InitializeAsync)).Debug("Locator: {Locator}, Keys: {Keys}", locator, locator.Keys.Count());
            }
        }
        catch (Exception ex)
        {
            log.ForMethod(nameof(InitializeAsync)).Error(ex, "Failed to initialize Addressables");
            throw;
        }
    }

public async UniTask LoadCatalogAsync(string url, CancellationToken ct = default)
{
    if (_catalogLocators.ContainsKey(url))
        return;

    var handle = Addressables.LoadContentCatalogAsync(url);
    await handle.ToUniTask(cancellationToken: ct);
    
    if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        _catalogLocators[url] = handle.Result;
    else
        throw new InvalidOperationException($"Failed to load catalog: {url}");
}

    public UniTask UnloadCatalogAsync(string url)
{
    if (_catalogLocators.TryGetValue(url, out var locator))
    {
        Addressables.RemoveResourceLocator(locator);
        _catalogLocators.Remove(url);
    }
    return UniTask.CompletedTask;
}

    public async UniTask LoadRemoteCatalogAsync(string baseUrl, string catalogName, CancellationToken ct = default)
    {
        var fullUrl = $"{baseUrl}/{catalogName}.json";
        await LoadCatalogAsync(fullUrl, ct);

        // Optional: pre-download dependencies
        await Addressables.DownloadDependenciesAsync(catalogName)
            .ToUniTask(cancellationToken: ct);
    }

    public async UniTask<bool> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var handle = Addressables.CheckForCatalogUpdates(false);
        await handle.ToUniTask(cancellationToken: ct);
        if (handle.Status != AsyncOperationStatus.Succeeded) return false;

        var updates = handle.Result;
        if (updates.Count == 0) return false;

        var updateHandle = Addressables.UpdateCatalogs(updates);
        await updateHandle.ToUniTask(cancellationToken: ct);
        return true;
    }

    public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object
    {
        return await _assetLoader.LoadAsync<T>(key, ct);
    }

    public async UniTask<T> LoadCachedAsync<T>(string key, CancellationToken ct = default)
        where T : UnityEngine.Object
    {
        if (_cachedAssets.TryGetValue(key, out var existing))
            return existing as T;

        var asset = await _assetLoader.LoadAsync<T>(key, ct);
        _cachedAssets[key] = asset;
        return asset;
    }

    public void ReleaseCached(string key)
    {
        if (_cachedAssets.TryGetValue(key, out var asset))
        {
            _assetLoader.Release(asset);
            _cachedAssets.Remove(key);
        }
    }

    public async UniTask PreloadLabelAsync(string label, CancellationToken ct = default)
    {
        log.ForMethod(nameof(PreloadLabelAsync)).Debug("PreloadLabelAsync called for label: {Label}", label);
        
        if (_preloadedAssets.ContainsKey(label))
        {
            log.ForMethod(nameof(PreloadLabelAsync)).Verbose("Label {Label} already preloaded, skipping", label);
            Progress.Value = 1f;
            return;
        }

        Progress.Value = 0f;

        // Load assets with progress tracking (this will download dependencies automatically)
        log.ForMethod(nameof(PreloadLabelAsync)).Verbose("Loading assets for label: {Label}", label);
        var assets = await LoadAssetsWithProgressAsync(label, ct);
        log.ForMethod(nameof(PreloadLabelAsync)).Debug("Loaded {Count} assets for label: {Label}", assets.Count, label);
        Progress.Value = 1f;
        
        _preloadedAssets[label] = new List<UnityEngine.Object>(assets);
    }

    private async UniTask<T> LoadAssetAsync<T>(string key, CancellationToken ct) where T : UnityEngine.Object
    {
        try
        {
            return await _assetLoader.LoadAsync<T>(key, ct);
        }
        catch (Exception ex)
        {
            log.ForMethod(nameof(LoadAssetAsync)).Warning(ex, "Failed to load asset with key: {Key}", key);
            return null;
        }
    }

    private async UniTask<List<UnityEngine.Object>> LoadAssetsWithProgressAsync(string label, CancellationToken ct)
    {
        if (_assetLoader is AddressablesAssetLoader)
        {
            // Get all locations for this label using the old Locate API which works in Editor
            log.ForMethod(nameof(LoadAssetsWithProgressAsync)).Debug("Loading resource locations for label: {Label}", label);
            
            var locationsToLoad = new List<IResourceLocation>();
            
            // Use the old Locate API which works properly in Editor
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(label, null, out var locations))
                {
                    foreach (var location in locations)
                    {
                        // Filter out scenes and other non-loadable asset types
                        // Only load assets that can be loaded as UnityEngine.Object
                        bool isScene = location.ResourceType == typeof(UnityEngine.ResourceManagement.ResourceProviders.SceneInstance);
                        bool isConfigAsset = location.PrimaryKey.Contains(".asset") && 
                                             !location.PrimaryKey.Contains("Prefab") && 
                                             !location.PrimaryKey.Contains("ScriptableObject");
                        
                        if (!isScene && !isConfigAsset)
                        {
                            locationsToLoad.Add(location);
                        }
                        else
                        {
                            log.ForMethod(nameof(LoadAssetsWithProgressAsync)).Debug("Skipping {Type} asset: {Key}", 
                                isScene ? "scene" : "config", 
                                location.PrimaryKey);
                        }
                    }
                }
            }

            if (locationsToLoad.Count == 0)
            {
                log.ForMethod(nameof(LoadAssetsWithProgressAsync)).Warning("No loadable assets found for label: {Label}", label);
                return new List<UnityEngine.Object>();
            }
            
            log.ForMethod(nameof(LoadAssetsWithProgressAsync)).Information("Found {Count} loadable assets for label: {Label}", locationsToLoad.Count, label);

            // Load all assets in parallel using UniTask.WhenAll
            var loadTasks = new List<UniTask<UnityEngine.Object>>();
            
            foreach (var location in locationsToLoad)
            {
                var key = location.PrimaryKey;
                
                loadTasks.Add(LoadAssetAsync<UnityEngine.Object>(key, ct));
            }

            // Wait for all assets to load in parallel
            var results = await UniTask.WhenAll(loadTasks);
            
            // Filter out null results (failed loads)
            var loadedAssets = results.OfType<UnityEngine.Object>().Where(a => a != null).ToList();
            Progress.Value = 1f;
            
            log.ForMethod(nameof(LoadAssetsWithProgressAsync)).Information("Successfully loaded {Count} assets for label: {Label}", loadedAssets.Count, label);
            return loadedAssets;
        }
        else
        {
            // Fallback for Resources
            var assets = await _assetLoader.LoadAllAsync<UnityEngine.Object>(label, ct);
            Progress.Value = 1f;
            return new List<UnityEngine.Object>(assets);
        }
    }

    public async UniTask CacheDependenciesAsync(string label, CancellationToken ct = default)
    {
        // For labels, we need to get the list of assets first, then download their dependencies
        var locationsHandle = Addressables.LoadResourceLocationsAsync(label);
        await locationsHandle.ToUniTask(cancellationToken: ct);
        
        if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
            throw new InvalidOperationException($"Failed to load resource locations for label: {label}");

        var keys = new List<object>();
        foreach (var location in locationsHandle.Result)
        {
            keys.Add(location.PrimaryKey);
        }

        if (keys.Count > 0)
        {
            var handle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);
            while (!handle.IsDone && !ct.IsCancellationRequested)
                await UniTask.Yield(ct);

            if (handle.Status == AsyncOperationStatus.Succeeded)
                _cachedHandles[label] = handle;
            else
                throw new InvalidOperationException($"Failed to cache dependencies for {label}");
        }
    }

    public void ReleaseDependencies(string label)
    {
        if (_cachedHandles.TryGetValue(label, out var handle))
        {
            Addressables.Release(handle);
            _cachedHandles.Remove(label);
        }
    }

    public void ClearAllCaches()
    {
        // Release all cached dependencies
        foreach (var label in _cachedHandles.Keys.ToList())
            ReleaseDependencies(label);

        // Release all cached assets
        foreach (var key in _cachedAssets.Keys.ToList())
            ReleaseCached(key);

        // Clear preloaded assets
        _preloadedAssets.Clear();
    }

    public void Release(UnityEngine.Object asset) => _assetLoader.Release(asset);

    public void ReleasePreloadedLabel(string label)
    {
        if (!_preloadedAssets.TryGetValue(label, out var assets))
            return;

        foreach (var asset in assets)
            _assetLoader.Release(asset);

        _preloadedAssets.Remove(label);
        
        // Also release cached dependencies for this label
        ReleaseDependencies(label);
    }

    public void ReleaseAll()
    {
        // Release all tracked handles from the asset loader
        _assetLoader.ReleaseAll();
    }

    public async UniTask LoadSceneAsync(AssetReference sceneReference, CancellationToken ct = default)
    {
        if (sceneReference == null || !sceneReference.RuntimeKeyIsValid())
        {
            log.ForMethod(nameof(LoadSceneAsync)).Error("Invalid scene reference");
            throw new ArgumentException("Invalid AssetReference provided");
        }

        log.ForMethod(nameof(LoadSceneAsync)).Debug("Loading scene via Addressables: {Scene}", sceneReference.RuntimeKey);
        
        var handle = Addressables.LoadSceneAsync(sceneReference, LoadSceneMode.Single);
        
        while (!handle.IsDone && !ct.IsCancellationRequested)
        {
            await UniTask.Yield(ct);
        }
        
        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            log.ForMethod(nameof(LoadSceneAsync)).Information("Successfully loaded scene: {Scene}", sceneReference.RuntimeKey);
        }
        else
        {
            log.ForMethod(nameof(LoadSceneAsync)).Error("Failed to load scene: {Scene}, Status: {Status}", sceneReference.RuntimeKey, handle.Status);
            throw new InvalidOperationException($"Failed to load scene: {sceneReference.RuntimeKey}, Status: {handle.Status}");
        }
    }

public void Dispose()
{
    foreach (var locator in _catalogLocators.Values)
        Addressables.RemoveResourceLocator(locator);

    _catalogLocators.Clear();

    // Clear all caches (includes preloaded assets, cached assets, and dependency handles)
    ClearAllCaches();

    // Release all active handles
    ReleaseAll();
}
}

}