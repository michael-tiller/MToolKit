using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader;
using Serilog;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Runtime.Bootstrapper.Interfaces;
using MToolKit.Runtime.AssetLoader.Interfaces;

namespace MToolKit.Runtime.Bootstrapper {
  /// <summary>
  /// This is the concrete implementation of the IGameLoader for the GameLoader.
  /// Loads a data-driven manifest that defines catalogs, labels, and scenes to preload.
  /// </summary>
  public sealed class GameLoader : IGameLoader
{
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameLoader>().ForFeature("Bootstrap"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private readonly IContentLoaderService _contentLoader;

    public GameLoader(IContentLoaderService contentLoader)
    {
        _contentLoader = contentLoader;
    }

    public async UniTask LoadGameAsync(CancellationToken ct = default)
    {
        // Load manifest from StreamingAssets
        var manifest = await LoadManifestAsync(ct);
        
        // Initialize Addressables
        await _contentLoader.InitializeAsync(ct);

        // Load remote catalogs
        foreach (var catalog in manifest.catalogs)
        {
            log.ForMethod(nameof(LoadGameAsync)).Information("Loading catalog: {Catalog}", catalog);
            await _contentLoader.LoadCatalogAsync(catalog, ct);
        }

        // Preload labels
        foreach (var label in manifest.labels)
        {
            log.ForMethod(nameof(LoadGameAsync)).Debug("Preloading label: {Label}", label);
            await _contentLoader.PreloadLabelAsync(label, ct);
        }

        // Load Addressable scenes
        // Check if GlobalConstantsConfig has a MenuSceneReference configured
        if (GlobalConstants.Instance?.GlobalConstantsConfig?.MenuSceneReference != null)
        {
            var menuSceneRef = GlobalConstants.Instance.GlobalConstantsConfig.MenuSceneReference;
            if (menuSceneRef.RuntimeKeyIsValid())
            {
                log.ForMethod(nameof(LoadGameAsync)).Information("Loading menu scene from GlobalConstantsConfig: {SceneGuid}", menuSceneRef.AssetGUID);
                await LoadSceneFromAssetReference(menuSceneRef, ct);
            }
            else
            {
                log.ForMethod(nameof(LoadGameAsync)).Warning("MenuSceneReference from GlobalConstantsConfig is invalid, falling back to manifest scenes");
                await LoadScenesFromManifest(manifest, ct);
            }
        }
        else
        {
            log.ForMethod(nameof(LoadGameAsync)).Debug("No MenuSceneReference in GlobalConstantsConfig, using manifest scenes");
            await LoadScenesFromManifest(manifest, ct);
        }

        log.ForMethod(nameof(LoadGameAsync)).Information("Game content loaded successfully. Version: {Version}", manifest.version);
    }
    
    private async UniTask LoadScenesFromManifest(RuntimeContentManifest manifest, CancellationToken ct)
    {
        // All scenes load as Single (replacing Bootstrapper) - first scene becomes active
        foreach (var sceneKey in manifest.scenes)
        {
            log.ForMethod(nameof(LoadScenesFromManifest)).Debug("Loading Addressable scene from manifest: {Scene}", sceneKey);
            await LoadSceneAsync(sceneKey, LoadSceneMode.Single, ct);
        }
    }

    private async UniTask LoadSceneFromAssetReference(AssetReference assetRef, CancellationToken ct)
    {
        // Load scene from AssetReference
        try
        {
            var handle = assetRef.LoadSceneAsync(LoadSceneMode.Single);
            await handle.ToUniTask(cancellationToken: ct);
            
            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                log.ForMethod(nameof(LoadSceneFromAssetReference)).Debug("Successfully loaded scene from AssetReference: {Guid}", assetRef.AssetGUID);
            }
            else
            {
                log.ForMethod(nameof(LoadSceneFromAssetReference)).Warning("Scene load failed from AssetReference: {Guid}, Status: {Status}", assetRef.AssetGUID, handle.Status);
            }
        }
        catch (Exception ex)
        {
            log.ForMethod(nameof(LoadSceneFromAssetReference)).Error(ex, "Failed to load scene from AssetReference: {Guid}", assetRef.AssetGUID);
            throw;
        }
    }
    
    private async UniTask LoadSceneAsync(string sceneKey, LoadSceneMode loadMode, CancellationToken ct)
    {
        // Load scene via Addressables - the manifest is designed for Addressable scenes only.
        // This enables remote updates, DLC, and patchable content without code rebuilds.
        try
        {
            var handle = Addressables.LoadSceneAsync(sceneKey, loadMode);
            await handle.ToUniTask(cancellationToken: ct);
            
            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                log.ForMethod(nameof(LoadSceneAsync)).Debug("Successfully loaded Addressable scene: {Scene}", sceneKey);
            }
            else
            {
                log.ForMethod(nameof(LoadSceneAsync)).Warning("Addressable scene load failed for: {Scene}, Status: {Status}", sceneKey, handle.Status);
            }
        }
        catch (Exception ex)
        {
            log.ForMethod(nameof(LoadSceneAsync)).Error(ex, "Failed to load Addressable scene: {Scene}. Ensure the scene is configured as Addressable.", sceneKey);
            throw;
        }
    }

    private static async UniTask<RuntimeContentManifest> LoadManifestAsync(CancellationToken ct)
    {
        var manifestPath = Path.Combine(Application.streamingAssetsPath, "manifest.json");
        
        if (!File.Exists(manifestPath))
        {
            log.ForMethod(nameof(LoadManifestAsync)).Warning("Manifest not found at {Path}, using defaults", manifestPath);
            return new RuntimeContentManifest
            {
                catalogs = Array.Empty<string>(),
                labels = new[] { "core", "ui", "localization" },
                scenes = Array.Empty<string>(),
                version = "1.0"
            };
        }

        try
        {
            var text = await File.ReadAllTextAsync(manifestPath, ct);
            var manifest = JsonUtility.FromJson<RuntimeContentManifest>(text);
            log.ForMethod(nameof(LoadManifestAsync)).Information("Loaded manifest with {Catalogs} catalogs, {Labels} labels, {Scenes} scenes",
                manifest.catalogs?.Length ?? 0, 
                manifest.labels?.Length ?? 0,
                manifest.scenes?.Length ?? 0);
            return manifest;
        }
        catch (Exception ex)
        {
            log.ForMethod(nameof(LoadManifestAsync)).Error(ex, "Failed to load manifest from {Path}", manifestPath);
            throw;
        }
    }
}
}