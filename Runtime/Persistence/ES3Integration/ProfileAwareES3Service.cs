using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
  /// <summary>
  ///   Profile-aware ES3 service that can switch between different save file paths based on the current profile
  /// </summary>
  public class ProfileAwareES3Service : IES3Service, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ProfileAwareES3Service>().ForFeature("Persistence.ES3"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly ES3SaveConfig config;
    private readonly CancellationTokenSource disposalCts;
    public readonly IProfileManager ProfileManager;
    private volatile bool isDisposed;

    // Batched-save state: inside a batch, key writes go to ES3's in-memory cache and
    // EndSaveBatch() stores the file ONCE. Without this every ES3.Save rewrites the whole
    // save file (30 controllers × ~100ms = the 3.3s autosave main-thread stall, 2026-08-08).
    private bool batchActive;
    private readonly System.Collections.Generic.HashSet<string> cachedPaths = new();

    public ProfileAwareES3Service(ES3SaveConfig config, IProfileManager profileManager)
    {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
      ProfileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
      disposalCts = new CancellationTokenSource();

      // Subscribe to profile changes to update save file path
      profileManager.CurrentProfile.Subscribe(OnCurrentProfileChanged).AddTo(disposalCts.Token);

      // Initialize by checking for existing save data
      InitializeFromExistingSave();

      log.ForMethod().Verbose("ProfileAwareES3Service created with config: {0}", config.name);
    }

    #region IDisposable Members

    public void Dispose()
    {
      if (isDisposed)
        return;

      isDisposed = true;
      disposalCts?.Cancel();
      disposalCts?.Dispose();

      // Dispose reactive properties
      IsSaving?.Dispose();
      IsLoading?.Dispose();
      LastSaveTime?.Dispose();
      LastLoadTime?.Dispose();
      SaveCounter?.Dispose();

      log.ForMethod().Debug("ProfileAwareES3Service disposed");
    }

    #endregion

    #region IES3Service Members

    public ReactiveProperty<bool> IsSaving { get; } = new(false);
    public ReactiveProperty<bool> IsLoading { get; } = new(false);
    public ReactiveProperty<string> LastSaveTime { get; } = new(string.Empty);
    public ReactiveProperty<string> LastLoadTime { get; } = new(string.Empty);
    public ReactiveProperty<int> SaveCounter { get; } = new(0);

    public UniTask SaveAsync(CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      if (IsSaving.Value)
      {
        log.ForMethod().Warning("Save operation already in progress, skipping duplicate save");
        return UniTask.CompletedTask;
      }

      IsSaving.Value = true;

      try
      {
        ES3Settings es3Settings = GetWriteSettings();
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Update reactive properties
        LastSaveTime.Value = currentTime;
        SaveCounter.Value++;

        // Update profile metadata if we have an active profile
        // This will handle all metadata storage including LastSaveTime, SaveFormatVersion, SaveCounter
        string currentProfile = ProfileManager.CurrentProfile.Value;
        if (!string.IsNullOrEmpty(currentProfile))
        {
          ProfileManager.SaveProfile(currentProfile, ct);
        }
        else
        {
          // Only save individual metadata fields if no profile is active (fallback case)
          ES3.Save("LastSaveTime", currentTime, es3Settings);
          ES3.Save("SaveFormatVersion", config.SaveFormatVersion, es3Settings);
          ES3.Save("SaveCounter", SaveCounter.Value, es3Settings);
        }

        log.ForMethod().Information("Saved to file: {0}, SaveCounter: {1}", es3Settings.FullPath, SaveCounter.Value);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save: {Message}", ex.Message);
        throw;
      }
      finally
      {
        IsSaving.Value = false;
      }
      return UniTask.CompletedTask;
    }

    public UniTask LoadAsync(CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      if (IsLoading.Value)
      {
        log.ForMethod().Warning("Load operation already in progress, skipping duplicate load");
        return UniTask.CompletedTask;
      }

      IsLoading.Value = true;

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Load metadata
        if (ES3.KeyExists("LastSaveTime", es3Settings))
          LastSaveTime.Value = ES3.Load<string>("LastSaveTime", es3Settings);

        if (ES3.KeyExists("SaveCounter", es3Settings))
          SaveCounter.Value = ES3.Load<int>("SaveCounter", es3Settings);

        LastLoadTime.Value = currentTime;

        log.ForMethod().Verbose("Loaded from file: {0}, LastSaveTime: {1}, SaveCounter: {2}",
          es3Settings.FullPath, LastSaveTime.Value, SaveCounter.Value);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load: {Message}", ex.Message);
        throw;
      }
      finally
      {
        IsLoading.Value = false;
      }
      return UniTask.CompletedTask;
    }

    public UniTask SaveAsync(string key, object value, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetWriteSettings();
        ES3.Save(key, value, es3Settings);
        log.ForMethod().Verbose("Saved key '{0}' to file: {1}", key, es3Settings.FullPath);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save key '{0}': {Message}", key, ex.Message);
        throw;
      }
      return UniTask.CompletedTask;
    }

    public UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));
      JoinPendingFlush();

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        if (ES3.KeyExists(key, es3Settings))
        {
          T value = ES3.Load<T>(key, es3Settings);
          log.ForMethod().Verbose("Loaded key '{0}' from file: {1}", key, es3Settings.FullPath);
          return UniTask.FromResult(value);
        }
        log.ForMethod().Verbose("Key '{0}' not found in file: {1}, returning default value", key, es3Settings.FullPath);
        return UniTask.FromResult(defaultValue);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load key '{0}': {Message}", key, ex.Message);
        throw;
      }
    }

    public bool KeyExists(string key)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));
      JoinPendingFlush();

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        return ES3.KeyExists(key, es3Settings);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to check if key '{0}' exists: {Message}", key, ex.Message);
        return false;
      }
    }

    public void DeleteKey(string key)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        ES3.DeleteKey(key, es3Settings);
        // Keep the in-memory cache coherent, or the next EndSaveBatch resurrects the key.
        if (cachedPaths.Contains(es3Settings.FullPath))
        {
          ES3Settings cacheSettings = GetCurrentES3Settings();
          cacheSettings.location = ES3.Location.Cache;
          try { ES3.DeleteKey(key, cacheSettings); } catch (Exception) { /* not cached yet */ }
        }
        log.ForMethod().Information("Deleted key '{0}' from file: {1}", key, es3Settings.FullPath);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to delete key '{0}': {Message}", key, ex.Message);
        throw;
      }
    }

    public void DeleteFile()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        ES3.DeleteFile(es3Settings);
        // Drop the cached copy too, or the next EndSaveBatch resurrects the whole file.
        if (cachedPaths.Remove(es3Settings.FullPath))
        {
          ES3Settings cacheSettings = GetCurrentES3Settings();
          cacheSettings.location = ES3.Location.Cache;
          try { ES3.DeleteFile(cacheSettings); } catch (Exception) { /* not cached */ }
        }
        batchActive = false;
        log.ForMethod().Information("Deleted save file: {0}", es3Settings.FullPath);

        // Reset reactive properties
        LastSaveTime.Value = "Never";
        SaveCounter.Value = 0;
        LastLoadTime.Value = "Never";
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to delete save file: {Message}", ex.Message);
        throw;
      }
    }

    public string GetSaveFormatVersion()
    {
      return config.SaveFormatVersion;
    }

    public string GetSavedFormatVersion()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        if (ES3.KeyExists("SaveFormatVersion", es3Settings))
          return ES3.Load<string>("SaveFormatVersion", es3Settings);
        return "Unknown";
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to get saved format version: {Message}", ex.Message);
        return "Unknown";
      }
    }

    public bool CreateBackup()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        string backupPath = es3Settings.FullPath + ".backup";

        // Create backup by copying the file
        if (File.Exists(es3Settings.FullPath))
        {
          File.Copy(es3Settings.FullPath, backupPath, true);
          log.ForMethod().Information("Created backup: {0}", backupPath);
          return true;
        }
        log.ForMethod().Warning("No save file exists to backup: {0}", es3Settings.FullPath);
        return false;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to create backup: {Message}", ex.Message);
        return false;
      }
    }

    public bool RestoreFromBackup()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        string backupPath = es3Settings.FullPath + ".backup";

        if (File.Exists(backupPath))
        {
          File.Copy(backupPath, es3Settings.FullPath, true);
          log.ForMethod().Information("Restored from backup: {0}", backupPath);

          // Re-initialize from the restored file
          InitializeFromExistingSave();
          return true;
        }
        log.ForMethod().Warning("No backup file exists: {0}", backupPath);
        return false;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to restore from backup: {Message}", ex.Message);
        return false;
      }
    }

    public string[] GetAvailableBackups()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileAwareES3Service));

      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();
        string directory = Path.GetDirectoryName(es3Settings.FullPath);
        string fileName = Path.GetFileNameWithoutExtension(es3Settings.FullPath);
        string extension = Path.GetExtension(es3Settings.FullPath);

        string backupPattern = $"{fileName}*{extension}.backup";
        string[] backupFiles = Directory.GetFiles(directory, backupPattern);

        log.ForMethod().Verbose("Found {0} backup files for: {1}", backupFiles.Length, es3Settings.FullPath);
        return backupFiles;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to get available backups: {Message}", ex.Message);
        return new string[0];
      }
    }

    #endregion

    /// <summary>
    ///   Gets the current save file path based on the active profile
    /// </summary>
    private string GetCurrentSaveFilePath()
    {
      string currentProfile = ProfileManager.CurrentProfile.Value;
      if (!string.IsNullOrEmpty(currentProfile))
      {
        // Use profile-specific file path
        string profileFilePath = ProfileManager.GetProfileFilePath(currentProfile);
        log.ForMethod().Verbose("Using profile-specific save file: {0}", profileFilePath);
        return profileFilePath;
      }
      // Fall back to default save file
      string defaultFilePath = config.GetSaveFilePath();
      log.ForMethod().Verbose("No active profile, using default save file: {0}", defaultFilePath);
      return defaultFilePath;
    }

    /// <summary>
    ///   Begin a batched save: subsequent key writes land in ES3's in-memory cache instead of
    ///   rewriting the file per key. The existing file is loaded into the cache once per path
    ///   (so the eventual store writes the FULL key set, not just this batch's keys).
    /// </summary>
    public void BeginSaveBatch()
    {
      if (isDisposed || batchActive) return;
      JoinPendingFlush();
      try
      {
        ES3Settings fileSettings = GetCurrentES3Settings();
        if (!cachedPaths.Contains(fileSettings.FullPath))
        {
          if (ES3.FileExists(fileSettings))
            ES3.CacheFile(fileSettings);
          cachedPaths.Add(fileSettings.FullPath);
        }
        batchActive = true;
      }
      catch (Exception ex)
      {
        // Fail open: without a batch, writes fall back to the per-key file path (slow but correct).
        log.ForMethod().Error(ex, "BeginSaveBatch failed; falling back to per-key file writes");
        batchActive = false;
      }
    }

    // In-flight background flush from EndSaveBatch. File-located reads and the next batch join it
    // first (JoinPendingFlush) so nothing observes a half-written save file. Batches are ~120s
    // apart and the flush takes ~50ms, so contention is effectively never.
    private System.Threading.Tasks.Task flushTask;

    private void JoinPendingFlush()
    {
      var t = flushTask;
      if (t != null && !t.IsCompleted)
      {
        try { t.Wait(); }
        catch (Exception ex) { log.ForMethod().Error(ex, "Background save flush failed: {Message}", ex.Message); }
      }
    }

    /// <summary>
    ///   Flush the batch: store the cached file to disk once, with the configured
    ///   compression/encryption — on a background thread. The ~50ms disk write was the last
    ///   single-frame autosave cost above the 33ms budget; ES3.StoreCachedFile only reads the
    ///   in-memory cache and writes the file, both safe off the main thread.
    /// </summary>
    public void EndSaveBatch()
    {
      if (!batchActive) return;
      batchActive = false;
      JoinPendingFlush();
      ES3Settings fileSettings = GetCurrentES3Settings();
      flushTask = System.Threading.Tasks.Task.Run(() =>
      {
        try
        {
          ES3.StoreCachedFile(fileSettings);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "EndSaveBatch background store failed: {Message}", ex.Message);
        }
      });
    }

    /// <summary>File-located settings normally; cache-located inside a save batch.</summary>
    private ES3Settings GetWriteSettings()
    {
      ES3Settings settings = GetCurrentES3Settings();
      if (batchActive)
        settings.location = ES3.Location.Cache;
      return settings;
    }

    /// <summary>
    ///   Gets ES3Settings for the current save file path
    /// </summary>
    private ES3Settings GetCurrentES3Settings()
    {
      string filePath = GetCurrentSaveFilePath();
      return new ES3Settings(filePath)
      {
        compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
        encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
        encryptionPassword = config.EncryptionKey
      };
    }

    /// <summary>
    ///   Called when the current profile changes
    /// </summary>
    private void OnCurrentProfileChanged(string newProfile)
    {
      log.ForMethod().Verbose("Profile changed to: {0}, updating save file path", string.IsNullOrWhiteSpace(newProfile) ? "null" : newProfile);

      // Re-initialize from the new save file
      InitializeFromExistingSave();
    }

    /// <summary>
    ///   Initialize the service by checking for existing save data
    /// </summary>
    private void InitializeFromExistingSave()
    {
      try
      {
        ES3Settings es3Settings = GetCurrentES3Settings();

        if (ES3.KeyExists("LastSaveTime", es3Settings))
          LastSaveTime.Value = ES3.Load<string>("LastSaveTime", es3Settings);
        else
          LastSaveTime.Value = "Never";

        if (ES3.KeyExists("SaveCounter", es3Settings))
          SaveCounter.Value = ES3.Load<int>("SaveCounter", es3Settings);
        else
          SaveCounter.Value = 0;

        LastLoadTime.Value = "Never";

        log.ForMethod().Verbose("Initialized from save file: {0}, LastSaveTime: {1}, SaveCounter: {2}",
          es3Settings.FullPath, LastSaveTime.Value, SaveCounter.Value);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize from existing save: {Message}", ex.Message);
        LastSaveTime.Value = "Never";
        SaveCounter.Value = 0;
        LastLoadTime.Value = "Never";
      }
    }
  }
}