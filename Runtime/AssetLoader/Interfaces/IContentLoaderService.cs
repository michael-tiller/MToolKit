using System.Threading;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
using R3;

namespace MToolKit.Runtime.AssetLoader.Interfaces {
  /// <summary>
  /// This service governs:
  /// - Addressables initialization (one entry point).
  /// - Remote catalog lifecycle (load/unload).
  /// - Preload orchestration by label or manifest.
  /// - Fallback to IAssetLoader for direct asset access.
  /// </summary>
public interface IContentLoaderService
{
    ReactiveProperty<float> Progress { get; }
    UniTask InitializeAsync(CancellationToken ct = default);
    UniTask PreloadLabelAsync(string label, CancellationToken ct = default);
    UniTask LoadCatalogAsync(string url, CancellationToken ct = default);
    UniTask UnloadCatalogAsync(string url);
    UniTask LoadRemoteCatalogAsync(string baseUrl, string catalogName, CancellationToken ct = default);
    UniTask<bool> CheckForUpdateAsync(CancellationToken ct = default);
    UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object;
    UniTask<T> LoadCachedAsync<T>(string key, CancellationToken ct = default) where T : UnityEngine.Object;
    void Release(UnityEngine.Object asset);
    void ReleaseCached(string key);
    UniTask CacheDependenciesAsync(string label, CancellationToken ct = default);
    void ReleaseDependencies(string label);
    void ReleasePreloadedLabel(string label);
    void ClearAllCaches();
    void ReleaseAll();
    UniTask LoadSceneAsync(AssetReference sceneReference, CancellationToken ct = default);
}
}