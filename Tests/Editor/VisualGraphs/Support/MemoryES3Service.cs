using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;
using R3;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   In-memory <see cref="IES3Service" /> fake: key/value round-trips by object identity (no ES3 wire
  ///   format). Lets GraphStateSaveController save/load characterization run synchronously in EditMode. Real
  ///   ES3 wire behavior is pinned separately by ES3SnapshotFileRoundTripTests.
  /// </summary>
  public sealed class MemoryES3Service : IES3Service
  {
    private readonly Dictionary<string, object> store = new();

    public ReactiveProperty<bool> IsSaving { get; } = new(false);
    public ReactiveProperty<bool> IsLoading { get; } = new(false);
    public ReactiveProperty<string> LastSaveTime { get; } = new(string.Empty);
    public ReactiveProperty<string> LastLoadTime { get; } = new(string.Empty);
    public ReactiveProperty<int> SaveCounter { get; } = new(0);

    public UniTask SaveAsync(CancellationToken ct = default)
    {
      return UniTask.CompletedTask;
    }

    public UniTask LoadAsync(CancellationToken ct = default)
    {
      return UniTask.CompletedTask;
    }

    public UniTask SaveAsync(string key, object value, CancellationToken ct = default)
    {
      store[key] = value;
      return UniTask.CompletedTask;
    }

    public UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
    {
      if (store.TryGetValue(key, out var value) && value is T typed)
        return UniTask.FromResult(typed);
      return UniTask.FromResult(defaultValue);
    }

    public bool KeyExists(string key)
    {
      return store.ContainsKey(key);
    }

    public void DeleteKey(string key)
    {
      store.Remove(key);
    }

    public void DeleteFile()
    {
      store.Clear();
    }

    public string GetSaveFormatVersion()
    {
      return "test";
    }

    public string GetSavedFormatVersion()
    {
      return "test";
    }

    public bool CreateBackup()
    {
      return true;
    }

    public bool RestoreFromBackup()
    {
      return true;
    }

    public string[] GetAvailableBackups()
    {
      return Array.Empty<string>();
    }
  }
}
