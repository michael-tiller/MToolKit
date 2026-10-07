using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>
  ///   Service interface for Easy Save 3 integration with reactive state management
  ///   Updated to use string versions instead of int
  /// </summary>
  public interface IES3Service
  {
    ReactiveProperty<bool> IsSaving { get; }
    ReactiveProperty<bool> IsLoading { get; }
    ReactiveProperty<string> LastSaveTime { get; }
    ReactiveProperty<string> LastLoadTime { get; }
    ReactiveProperty<int> SaveCounter { get; }

    UniTask SaveAsync(CancellationToken ct = default);
    UniTask LoadAsync(CancellationToken ct = default);
    UniTask SaveAsync(string key, object value, CancellationToken ct = default);

    /// <summary>Begins the optional in-memory save batch.</summary>
    void BeginSaveBatch() { }

    /// <summary>Legacy fire-and-forget batch flush; durable callers use IDurableES3Service.</summary>
    void EndSaveBatch() { }

    /// <summary>Closes an unsuccessful batch. Backends with a cache discard pending writes.</summary>
    /// <remarks>The default preserves legacy backends' batch-closing behavior without a receipt.</remarks>
    void AbortSaveBatch() => EndSaveBatch();

    UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default);
    bool KeyExists(string key);
    void DeleteKey(string key);
    void DeleteFile();

    /// <summary>
    ///   Gets the current save format version
    /// </summary>
    string GetSaveFormatVersion();

    /// <summary>
    ///   Gets the version from the save file if it exists
    /// </summary>
    string GetSavedFormatVersion();

    /// <summary>
    ///   Creates a backup of the current save file using ES3's native backup functionality
    /// </summary>
    bool CreateBackup();

    /// <summary>
    ///   Restores from the most recent backup using ES3's native restore functionality
    /// </summary>
    bool RestoreFromBackup();

    /// <summary>
    ///   Gets all available backup files for the current save file
    /// </summary>
    string[] GetAvailableBackups();
  }
}
