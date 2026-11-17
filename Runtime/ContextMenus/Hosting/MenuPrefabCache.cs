using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader.Interfaces;
using UnityEngine;

namespace MToolKit.ContextMenus.Hosting
{
  /// <summary>Serializes same-key loads and retains one service-owned reference per host/key.</summary>
  public sealed class MenuPrefabCache : IDisposable
  {
    private sealed class Entry
    {
      // Unlike an async UniTask's single continuation, this source supports multiple pending awaiters.
      public readonly UniTaskCompletionSource<GameObject> Completion = new();
      public GameObject Asset;
      public bool Released;
    }
    private readonly IRuntimeAssetService assets;
    private readonly bool unityOwner;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    public MenuPrefabCache(IRuntimeAssetService assets)
    {
      this.assets = assets;
      unityOwner = PlayerLoopHelper.IsMainThread && PlayerLoopHelper.UnitySynchronizationContext != null;
    }

    public UniTask<GameObject> LoadAsync(string key, CancellationToken cancellationToken)
    {
      if (disposed) throw new ObjectDisposedException(nameof(MenuPrefabCache));
      if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A typed prefab reference requires an asset GUID.", nameof(key));
      if (assets == null) throw new InvalidOperationException("An IRuntimeAssetService is required for Addressable menu prefabs. Direct prefab references need no loader.");
      if (!entries.TryGetValue(key, out var entry))
      {
        entry = new Entry(); entries.Add(key, entry);
        LoadCoreAsync(key, entry).Forget();
      }
      var result = entry.Completion.Task.AttachExternalCancellation(lifetime.Token);
      return cancellationToken.CanBeCanceled ? result.AttachExternalCancellation(cancellationToken) : result;
    }

    private async UniTask LoadCoreAsync(string key, Entry entry)
    {
      try
      {
        // Caller cancellation fences presentation, not the shared loader operation. A late result
        // remains owned by this cache or is released here after disposal; it cannot reopen a menu.
        GameObject asset;
        try { asset = await assets.LoadAsync<GameObject>(key, CancellationToken.None); }
        finally
        {
          if (unityOwner && !PlayerLoopHelper.IsMainThread) await UniTask.SwitchToMainThread();
        }
        if (asset == null) throw new InvalidOperationException("Menu prefab load returned no asset: " + key);
        entry.Asset = asset;
        // Returned awaiters are already canceled by lifetime on disposal. Complete the internal
        // operation quietly after releasing its late asset, avoiding an unobserved teardown fault.
        if (disposed) { Release(entry); entry.Completion.TrySetCanceled(); return; }
        entry.Completion.TrySetResult(asset);
      }
      catch (Exception exception)
      {
        if (entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry)) entries.Remove(key);
        entry.Completion.TrySetException(exception);
      }
    }

    private void Release(Entry entry)
    {
      if (entry.Released || ReferenceEquals(entry.Asset, null)) return;
      entry.Released = true; assets.Release(entry.Asset);
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      lifetime.Cancel();
      foreach (var entry in entries.Values) Release(entry);
      entries.Clear();
      lifetime.Dispose();
    }
  }
}
