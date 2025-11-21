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
  ///   Easy Save 3 service implementation with reactive state management
  ///   Updated to use string versions instead of int
  /// </summary>
  public class ES3SaveService : IES3Service, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ES3SaveService>().ForFeature("Persistence.ES3"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private readonly ES3SaveConfig config;
    private readonly CancellationTokenSource disposalCts;
    private readonly ES3Settings es3Settings;
    private readonly SemaphoreSlim fileAccessSemaphore;

    private readonly string filePath;
    private readonly string saveFormatVersion;
    private volatile bool isDisposed;

    public ES3SaveService(string filePath = "SaveFile.es3")
    {
      this.filePath = filePath;
      saveFormatVersion = "1.0.0"; // Default version for backward compatibility
      disposalCts = new CancellationTokenSource();
      fileAccessSemaphore = new SemaphoreSlim(1, 1); // Allow only one concurrent file access

      // Create ES3Settings directly
      es3Settings = new ES3Settings(filePath);

      // Ensure the directory exists for the save file
      EnsureDirectoryExists();

      // Initialize by checking for existing save data
      InitializeFromExistingSave();

      // Get the full path for logging
      var fullPath = es3Settings.FullPath;
      log.ForMethod().Information("ES3SaveService created - File: {0}, Full Path: {1}, Version: {2}", filePath, fullPath, saveFormatVersion);
    }

    public ES3SaveService(string filePath, ES3SaveConfig config)
    {
      this.filePath = filePath;
      saveFormatVersion = config?.SaveFormatVersion ?? "1.0.0";
      this.config = config;
      disposalCts = new CancellationTokenSource();
      fileAccessSemaphore = new SemaphoreSlim(1, 1); // Allow only one concurrent file access

      // Create ES3Settings with config-based options
      es3Settings = new ES3Settings(filePath)
      {
        compressionType = config?.CompressSaveData == true ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
        encryptionType = config?.EncryptSaveData == true ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
        encryptionPassword = config?.EncryptionKey ?? "DefaultEncryptionKey"
      };

      // Ensure the directory exists for the save file
      EnsureDirectoryExists();

      // Initialize by checking for existing save data
      InitializeFromExistingSave();

      // Get the full path for logging
      var fullPath = es3Settings.FullPath;
      log.ForMethod().Verbose("ES3SaveService created with config - File: {0}, Full Path: {1}, Version: {2}, Compression: {3}, Encryption: {4}",
        filePath, fullPath, saveFormatVersion, es3Settings.compressionType, es3Settings.encryptionType);
    }

    #region IDisposable Members

    /// <summary>
    ///   Disposes of reactive properties and other resources
    /// </summary>
    public void Dispose()
    {
      if (isDisposed)
      {
        return;
      }

      isDisposed = true;

      try
      {
        // Cancel any ongoing operations
        disposalCts?.Cancel();

        // Wait for any ongoing operations to complete
        Thread.Sleep(100);

        IsSaving?.Dispose();
        IsLoading?.Dispose();
        LastSaveTime?.Dispose();
        LastLoadTime?.Dispose();
        SaveCounter?.Dispose();
        disposalCts?.Dispose();

        // Dispose semaphore safely
        try
        {
          fileAccessSemaphore?.Dispose();
        }
        catch (ObjectDisposedException)
        {
          // Already disposed, ignore
        }

        log.ForMethod().Debug("ES3SaveService disposed");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Error during disposal: {Message}", ex.Message);
      }
    }

    #endregion

    #region IES3Service Members

    public ReactiveProperty<bool> IsSaving { get; } = new(false);
    public ReactiveProperty<bool> IsLoading { get; } = new(false);
    public ReactiveProperty<string> LastSaveTime { get; } = new(string.Empty);
    public ReactiveProperty<string> LastLoadTime { get; } = new(string.Empty);
    public ReactiveProperty<int> SaveCounter { get; } = new(0);

    public async UniTask SaveAsync(CancellationToken ct = default)
    {
      if (isDisposed)
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      if (IsSaving.Value)
      {
        log.ForMethod().Warning("Save operation already in progress");
        return;
      }

      try
      {
        IsSaving.Value = true;

        // Add timeout for save operation and link with disposal token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, disposalCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout

        // Acquire semaphore to ensure exclusive file access
        await fileAccessSemaphore.WaitAsync(timeoutCts.Token);

        try
        {
          await UniTask.RunOnThreadPool(() =>
          {
            // Check if disposed before proceeding
            if (isDisposed)
            {
              return;
            }

            // Create backup before saving if enabled
            if (config?.CreateBackups == true && ES3.FileExists(filePath))
            {
              try
              {
                ES3.CreateBackup(filePath);
                log.ForMethod().Debug("Created backup before save operation");
              }
              catch (Exception ex)
              {
                log.ForMethod().Warning(ex, "Failed to create backup: {Message}", ex.Message);
              }
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var newSaveCounter = SaveCounter.Value + 1;

            ES3.Save("LastSaveTime", timestamp, es3Settings);
            ES3.Save("SaveFormatVersion", saveFormatVersion, es3Settings);
            ES3.Save("SaveCounter", newSaveCounter, es3Settings);

            // Check disposal again before setting reactive properties
            if (!isDisposed)
            {
              LastSaveTime.Value = timestamp;
              SaveCounter.Value = newSaveCounter;
            }
          }, cancellationToken: timeoutCts.Token);

          // Check if file exists
          if (!ES3.FileExists(filePath))
          {
            log.ForMethod().Warning("No save file found at {0}, game was not saved", filePath);
            return;
          }

          // Get the full path for logging
          var fullPath = es3Settings.FullPath;
          log.ForMethod().Information("Game saved successfully - File: {0}, Full Path: {1}", filePath, fullPath);
        }
        finally
        {
          // Only release semaphore if not disposed
          if (!isDisposed)
          {
            try
            {
              fileAccessSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
              // Semaphore was disposed, ignore
            }
          }
        }
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        log.ForMethod().Warning("Save operation was cancelled");
        throw;
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Error("Save operation timed out after 30 seconds");
        throw new TimeoutException("Save operation timed out");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Save operation failed: {Message}", ex.Message);
        throw;
      }
      finally
      {
        if (!isDisposed)
        {
          IsSaving.Value = false;
        }
      }
    }

    public async UniTask LoadAsync(CancellationToken ct = default)
    {
      if (isDisposed)
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      if (IsLoading.Value)
      {
        log.ForMethod().Warning("Load operation already in progress");
        return;
      }

      try
      {
        IsLoading.Value = true;

        // Check if file exists
        if (!ES3.FileExists(filePath))
        {
          log.ForMethod().Information("No save file found at {0}, skipping load", filePath);
          return;
        }

        // Add timeout for load operation and link with disposal token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, disposalCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout

        // Acquire semaphore to ensure exclusive file access
        await fileAccessSemaphore.WaitAsync(timeoutCts.Token);

        try
        {
          await UniTask.RunOnThreadPool(() =>
          {
            // Check if disposed before proceeding
            if (isDisposed)
            {
              return;
            }

            // Load timestamp if it exists
            if (ES3.KeyExists("LastSaveTime", es3Settings))
            {
              var timestamp = ES3.Load<string>("LastSaveTime", es3Settings);

              // Check disposal again before setting reactive property
              if (!isDisposed)
              {
                LastLoadTime.Value = timestamp;
              }
            }

            // Load save counter if it exists
            if (ES3.KeyExists("SaveCounter", es3Settings))
            {
              var saveCounter = ES3.Load<int>("SaveCounter", es3Settings);

              // Check disposal again before setting reactive property
              if (!isDisposed)
              {
                SaveCounter.Value = saveCounter;
              }
            }

            // Check for version mismatch
            if (ES3.KeyExists("SaveFormatVersion", es3Settings))
            {
              var savedVersion = ES3.Load<string>("SaveFormatVersion", es3Settings);
              if (savedVersion != saveFormatVersion)
              {
                log.ForMethod().Warning("Version mismatch detected - Save file version: {0}, Current version: {1}", savedVersion, saveFormatVersion);
              }
              else
              {
                log.ForMethod().Debug("Save file version matches current version: {0}", saveFormatVersion);
              }
            }
            else
            {
              log.ForMethod().Information("No version information found in save file (likely version 1.0.0 or legacy save)");
            }
          }, cancellationToken: timeoutCts.Token);

          // Get the full path for logging
          var fullPath = es3Settings.FullPath;
          log.ForMethod().Information("Game loaded successfully - File: {0}, Full Path: {1}", filePath, fullPath);
        }
        finally
        {
          // Only release semaphore if not disposed
          if (!isDisposed)
          {
            try
            {
              fileAccessSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
              // Semaphore was disposed, ignore
            }
          }
        }
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        log.ForMethod().Warning("Load operation was cancelled");
        throw;
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Error("Load operation timed out after 30 seconds");
        throw new TimeoutException("Load operation timed out");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Load operation failed: {Message}", ex.Message);
        throw;
      }
      finally
      {
        if (!isDisposed)
        {
          IsLoading.Value = false;
        }
      }
    }

    public async UniTask SaveAsync(string key, object value, CancellationToken ct = default)
    {
      if (isDisposed)
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      try
      {
        // Add timeout for save operation and link with disposal token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, disposalCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout for individual key saves

        // Acquire semaphore to ensure exclusive file access
        await fileAccessSemaphore.WaitAsync(timeoutCts.Token);

        try
        {
          await UniTask.RunOnThreadPool(() => { ES3.Save(key, value, es3Settings); }, cancellationToken: timeoutCts.Token);

          log.ForMethod().Verbose("Saved key '{0}' to {1}", key, filePath);
        }
        finally
        {
          // Only release semaphore if not disposed
          if (!isDisposed)
          {
            try
            {
              fileAccessSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
              // Semaphore was disposed, ignore
            }
          }
        }
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        log.ForMethod().Warning("Save operation for key '{0}' was cancelled", key);
        throw;
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Error("Save operation for key '{0}' timed out after 10 seconds", key);
        throw new TimeoutException($"Save operation for key '{key}' timed out");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save key '{0}': {Message}", key, ex.Message);
        throw;
      }
    }

    public async UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
    {
      if (isDisposed)
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      try
      {
        // Add timeout for load operation and link with disposal token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, disposalCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout for individual key loads

        // Acquire semaphore to ensure exclusive file access
        await fileAccessSemaphore.WaitAsync(timeoutCts.Token);

        try
        {
          return await UniTask.RunOnThreadPool(() =>
          {
            if (ES3.KeyExists(key, es3Settings))
            {
              return ES3.Load<T>(key, es3Settings);
            }
            return defaultValue;
          }, cancellationToken: timeoutCts.Token);
        }
        finally
        {
          // Only release semaphore if not disposed
          if (!isDisposed)
          {
            try
            {
              fileAccessSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
              // Semaphore was disposed, ignore
            }
          }
        }
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        log.ForMethod().Warning("Load operation for key '{0}' was cancelled", key);
        throw;
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Error("Load operation for key '{0}' timed out after 10 seconds", key);
        throw new TimeoutException($"Load operation for key '{key}' timed out");
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
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      return ES3.KeyExists(key, es3Settings);
    }

    public void DeleteKey(string key)
    {
      if (isDisposed)
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      try
      {
        ES3.DeleteKey(key, es3Settings);
        log.ForMethod().Verbose("Deleted key '{0}' from {1}", key, filePath);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to delete key '{0}': {Message}", key, ex.Message);
      }
    }

    public void DeleteFile()
    {
      if (isDisposed)
      {
        throw new ObjectDisposedException(nameof(ES3SaveService));
      }

      try
      {
        ES3.DeleteFile(filePath);
        log.ForMethod().Information("Deleted save file: {0}", filePath);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to delete file '{0}': {Message}", filePath, ex.Message);
      }
    }

    /// <summary>
    ///   Gets the current save format version
    /// </summary>
    public string GetSaveFormatVersion()
    {
      return saveFormatVersion;
    }

    /// <summary>
    ///   Gets the version from the save file if it exists
    /// </summary>
    public string GetSavedFormatVersion()
    {
      try
      {
        if (ES3.FileExists(filePath) && ES3.KeyExists("SaveFormatVersion", es3Settings))
        {
          return ES3.Load<string>("SaveFormatVersion", es3Settings);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to read save format version: {Message}", ex.Message);
      }

      return "1.0.0"; // Default version for legacy saves
    }

    /// <summary>
    ///   Creates a backup of the current save file using ES3's native backup functionality
    /// </summary>
    public bool CreateBackup()
    {
      try
      {
        if (ES3.FileExists(filePath))
        {
          ES3.CreateBackup(filePath);
          log.ForMethod().Information("Backup created successfully for {0}", filePath);
          return true;
        }
        else
        {
          log.ForMethod().Warning("Cannot create backup - no save file exists at {0}", filePath);
          return false;
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to create backup: {Message}", ex.Message);
        return false;
      }
    }

    /// <summary>
    ///   Restores from the most recent backup using ES3's native restore functionality
    /// </summary>
    public bool RestoreFromBackup()
    {
      try
      {
        var restored = ES3.RestoreBackup(filePath);
        if (!restored)
        {
          log.ForMethod().Warning("No backup found to restore for {0}", filePath);
          return false;
        }

        log.ForMethod().Information("Restored from backup successfully for {0}", filePath);

        // Re-initialize after restore to update LastSaveTime
        InitializeFromExistingSave();
        return true;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to restore from backup: {Message}", ex.Message);
        return false;
      }
    }

    /// <summary>
    ///   Gets all available backup files for the current save file
    ///   Note: ES3 doesn't expose backup listing directly, so this returns empty array
    /// </summary>
    public string[] GetAvailableBackups()
    {
      try
      {
        // ES3 doesn't have a direct method to list backups
        // Return empty array as ES3 manages backups internally
        return new string[0];
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to get available backups: {Message}", ex.Message);
        return new string[0];
      }
    }

    #endregion

    /// <summary>
    ///   Initialize the service by checking for existing save data
    /// </summary>
    private void InitializeFromExistingSave()
    {
      try
      {
        // Check if save file exists
        if (ES3.FileExists(filePath))
        {
          // Try to load the LastSaveTime from the existing save file
          if (ES3.KeyExists("LastSaveTime", es3Settings))
          {
            var existingSaveTime = ES3.Load<string>("LastSaveTime", es3Settings);
            LastSaveTime.Value = existingSaveTime;
            log.ForMethod().Debug("Initialized from existing save file - LastSaveTime: {0}", existingSaveTime);
          }
          else
          {
            log.ForMethod().Debug("Save file exists but no LastSaveTime found");
          }

          // Try to load the SaveCounter from the existing save file
          if (ES3.KeyExists("SaveCounter", es3Settings))
          {
            var existingSaveCounter = ES3.Load<int>("SaveCounter", es3Settings);
            SaveCounter.Value = existingSaveCounter;
            log.ForMethod().Debug("Initialized from existing save file - SaveCounter: {0}", existingSaveCounter);
          }
          else
          {
            log.ForMethod().Debug("Save file exists but no SaveCounter found, starting from 0");
          }
        }
        else
        {
          log.ForMethod().Debug("No existing save file found at {0}", filePath);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to initialize from existing save file: {Message}", ex.Message);
        // Continue with empty LastSaveTime
      }
    }

    /// <summary>
    ///   Ensures that the directory for the save file exists
    /// </summary>
    private void EnsureDirectoryExists()
    {
      try
      {
        var fullPath = es3Settings.FullPath;
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
          Directory.CreateDirectory(directory);
          log.ForMethod().Debug("Created directory for save file: {0}", directory);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to ensure directory exists: {Message}", ex.Message);
      }
    }
  }
}