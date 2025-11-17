using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Bootstrapper.Interfaces;
using MToolKit.Runtime.Core.Singletons;
using Serilog;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Bootstrapper
{
  /// <summary>
  ///   This is the concrete implementation of the IGameLoader for the GameLoader.
  ///   Loads a data-driven manifest that defines catalogs, labels, and scenes to preload.
  /// </summary>
  public sealed class GameLoader : IGameLoader
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameLoader>().ForFeature("Bootstrap"));

    private readonly IContentLoaderService contentLoader;

    public GameLoader(IContentLoaderService contentLoader)
    {
      this.contentLoader = contentLoader;
    }

    private static ILogger log => logLazy.Value ?? Logger.None;

    #region IGameLoader Members

    public async UniTask LoadGameAsync(CancellationToken ct = default)
    {
      // Initialize Addressables first (required before loading any Addressable assets)
      await contentLoader.InitializeAsync(ct);

      // Load manifest from Addressables (with "manifest" tag) or fallback to StreamingAssets
      // The manifest has its own tag to avoid circular dependency with the labels it configures
      (RuntimeContentManifest manifest, string path) = await LoadManifestAsync(ct);

      // Load remote catalogs
      foreach (string catalog in manifest.Catalogs)
      {
        log.ForMethod().Information("Loading catalog: {Catalog}", catalog);
        await contentLoader.LoadCatalogAsync(catalog, ct);
      }

      // Preload labels
      foreach (string label in manifest.Labels)
      {
        log.ForMethod().Debug("Preloading label: {Label}", label);
        await contentLoader.PreloadLabelAsync(label, ct);
      }

      // Load Addressable scenes
      // Check if GlobalConstantsConfig has a MenuSceneReference configured
      if (GlobalConstants.Instance?.GlobalConstantsConfig?.MenuSceneReference != null)
      {
        AssetReferenceScene menuSceneRef = GlobalConstants.Instance.GlobalConstantsConfig.MenuSceneReference;
        if (menuSceneRef.RuntimeKeyIsValid())
        {
          log.ForMethod().Information("Loading menu scene from GlobalConstantsConfig: {SceneGuid}", menuSceneRef.AssetGUID);
          await LoadSceneFromAssetReference(menuSceneRef, ct);
        }
        else
        {
          log.ForMethod().Warning("MenuSceneReference from GlobalConstantsConfig is invalid, falling back to manifest scenes");
          await LoadScenesFromManifest(manifest, ct);
        }
      }
      else
      {
        log.ForMethod().Debug("No MenuSceneReference in GlobalConstantsConfig, using manifest scenes");
        await LoadScenesFromManifest(manifest, ct);
      }

      await UniTask.WaitForEndOfFrame();

      log.ForMethod().Information("Game content loaded successfully. Manifest: {ManifestPath}, Version: {Version}", path, manifest.Version);
    }

    #endregion

    private async UniTask LoadScenesFromManifest(RuntimeContentManifest manifest, CancellationToken ct)
    {
      // All scenes load as Single (replacing Bootstrapper) - first scene becomes active
      foreach (string sceneKey in manifest.Scenes)
      {
        log.ForMethod().Debug("Loading Addressable scene from manifest: {Scene}", sceneKey);
        await LoadSceneAsync(sceneKey, LoadSceneMode.Single, ct);
      }
    }

    private async UniTask LoadSceneFromAssetReference(AssetReference assetRef, CancellationToken ct)
    {
      // Load scene from AssetReference
      try
      {
        AsyncOperationHandle<SceneInstance> handle = assetRef.LoadSceneAsync();
        await handle.ToUniTask(cancellationToken: ct);

        if (handle.Status == AsyncOperationStatus.Succeeded)
          log.ForMethod().Debug("Successfully loaded scene from AssetReference: {Guid}", assetRef.AssetGUID);
        else
          log.ForMethod().Warning("Scene load failed from AssetReference: {Guid}, Status: {Status}", assetRef.AssetGUID, handle.Status);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load scene from AssetReference: {Guid}", assetRef.AssetGUID);
        throw;
      }
    }

    private async UniTask LoadSceneAsync(string sceneKey, LoadSceneMode loadMode, CancellationToken ct)
    {
      // Load scene via Addressables - the manifest is designed for Addressable scenes only.
      // This enables remote updates, DLC, and patchable content without code rebuilds.
      try
      {
        AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(sceneKey, loadMode);
        await handle.ToUniTask(cancellationToken: ct);

        if (handle.Status == AsyncOperationStatus.Succeeded)
          log.ForMethod().Debug("Successfully loaded Addressable scene: {Scene}", sceneKey);
        else
          log.ForMethod().Warning("Addressable scene load failed for: {Scene}, Status: {Status}", sceneKey, handle.Status);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load Addressable scene: {Scene}. Ensure the scene is configured as Addressable.", sceneKey);
        throw;
      }
    }

    private static async UniTask<(RuntimeContentManifest, string)> LoadManifestAsync(CancellationToken ct)
    {
      const string manifestAddress = "manifest"; // Addressable address for the manifest
      const string manifestTag = "manifest"; // Tag for the manifest (separate from content labels)

      // Try to load from Addressables first (allows remote updates)
      try
      {
        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(manifestAddress);
        await handle.ToUniTask(cancellationToken: ct);

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
          RuntimeContentManifest manifest = JsonUtility.FromJson<RuntimeContentManifest>(handle.Result.text);
          log.ForMethod().Information("Loaded manifest from Addressables ({Address}) with {Catalogs} catalogs, {Labels} labels, {Scenes} scenes",
            manifestAddress,
            manifest.Catalogs?.Length ?? 0,
            manifest.Labels?.Length ?? 0,
            manifest.Scenes?.Length ?? 0);
          
          // Release the handle after we've extracted the text
          Addressables.Release(handle);
          return (manifest, $"Addressables://{manifestAddress}");
        }
        else
        {
          log.ForMethod().Debug("Manifest not found in Addressables at {Address}, trying StreamingAssets fallback", manifestAddress);
          if (handle.IsValid())
            Addressables.Release(handle);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to load manifest from Addressables, trying StreamingAssets fallback: {Message}", ex.Message);
      }

      // Fallback to StreamingAssets (for development/local builds)
      // Use .txt extension since Unity can import text files properly for Addressables
      string manifestPath = Path.Combine(Application.streamingAssetsPath, "manifest.txt");

      if (!File.Exists(manifestPath))
      {
        log.ForMethod().Warning("Manifest not found at {Path}, using defaults", manifestPath);
        return (new RuntimeContentManifest(
          catalogs: Array.Empty<string>(),
          labels: new[] { "core", "ui", "localization" },
          scenes: Array.Empty<string>(),
          version: "1.0"),
          manifestPath);
      }

      try
      {
        string text = await File.ReadAllTextAsync(manifestPath, ct);
        RuntimeContentManifest manifest = JsonUtility.FromJson<RuntimeContentManifest>(text);
        log.ForMethod().Information("Loaded manifest from StreamingAssets ({Path}) with {Catalogs} catalogs, {Labels} labels, {Scenes} scenes",
          manifestPath,
          manifest.Catalogs?.Length ?? 0,
          manifest.Labels?.Length ?? 0,
          manifest.Scenes?.Length ?? 0);
        return (manifest, manifestPath);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load manifest from {Path}", manifestPath);
        throw;
      }
    }
  }
}