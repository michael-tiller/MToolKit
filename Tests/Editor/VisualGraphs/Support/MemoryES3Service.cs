using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
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
  public sealed class MemoryES3Service : IDurableES3Service
  {
    private readonly Dictionary<string, object> store = new();

    public ReactiveProperty<bool> IsSaving { get; } = new(false);
    public ReactiveProperty<bool> IsLoading { get; } = new(false);
    public ReactiveProperty<string> LastSaveTime { get; } = new(string.Empty);
    public ReactiveProperty<string> LastLoadTime { get; } = new(string.Empty);
    public ReactiveProperty<int> SaveCounter { get; } = new(0);

    private readonly ES3SaveContext saveContext = new(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), "memory-save.es3"));

    public ES3SaveContext GetSaveContext() => saveContext;
    public Exception FlushFailure { get; set; }
    public UniTaskCompletionSource<ES3FlushReceipt> PendingFlush { get; set; }
    public int FlushCount { get; private set; }
    public UniTask<ES3FlushReceipt> FlushSaveBatchAsync(CancellationToken ct = default)
    {
      FlushCount++;
      if (FlushFailure != null) return UniTask.FromException<ES3FlushReceipt>(FlushFailure);
      if (PendingFlush != null) return PendingFlush.Task;
      return UniTask.FromResult(new ES3FlushReceipt(saveContext, new string('0', 64), DateTime.UtcNow));
    }

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
