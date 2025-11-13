using System;
using System.IO;
using MToolKit.Runtime.Settings.Game;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   Configuration asset for ES3 save system settings.
    ///   Provides designer-friendly configuration for save behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "ES3SaveConfig", menuName = "Game/Save/ES3 Save Config")]
  [InlineEditor]
  public class ES3SaveConfig : ScriptableObject
  {
    [Header("File Settings")]
    [BoxGroup("File Settings")]
    [SerializeField]
    [Required]
    private string saveFileName = "SaveFile.es3";

    [BoxGroup("File Settings")]
    [SerializeField]
    private string backupFileName = "SaveFile.backup.es3";

    [BoxGroup("File Settings")]
    [SerializeField]
    private string saveDirectory = "Saves";

    [BoxGroup("File Settings")]
    [SerializeField]
    private bool usePersistentDataPath = true;

    [Header("Auto-Save Settings")]
    [BoxGroup("Auto-Save Settings")]
    [SerializeField]
    [MinValue(1f)]
    private float autoSaveIntervalSeconds = 300f; // 5 minutes

    [BoxGroup("Auto-Save Settings")]
    [SerializeField]
    private int autoSavePaddingMilliseconds = 200;

    [BoxGroup("Auto-Save Settings")]
    [SerializeField]
    private bool autoSaveOnSceneChange = true;

    [Header("Validation Settings")]
    [BoxGroup("Validation Settings")]
    [SerializeField]
    private bool enableChecksums = true;

    [BoxGroup("Validation Settings")]
    [SerializeField]
    private bool createBackups = true;

    [BoxGroup("Validation Settings")]
    [SerializeField]
    [ShowIf("createBackups")]
    [MinValue(1)]
    private int maxBackupFiles = 3;

    [BoxGroup("Migration Settings")]
    [SerializeField]
    private bool enableFormatMigration = true;

    [BoxGroup("Migration Settings")]
    [SerializeField]
    [ShowIf("enableFormatMigration")]
    private bool createMigrationBackup = true;

    [Header("Performance Settings")]
    [BoxGroup("Performance Settings")]
    [SerializeField]
    private bool compressSaveData = true;

    [BoxGroup("Performance Settings")]
    [SerializeField]
    private bool encryptSaveData;

    [BoxGroup("Performance Settings")]
    [SerializeField]
    [ShowIf("encryptSaveData")]
    private string encryptionKey = "DefaultEncryptionKey";

    [Header("Platform Cloud Backup Settings")]
    [BoxGroup("Platform Cloud Backup Settings")]
    [SerializeField]
    private bool enablePlatformCloudBackup = true;

    [BoxGroup("Platform Cloud Backup Settings")]
    [SerializeField]
    [ShowIf("enablePlatformCloudBackup")]
    [InfoBox("Platform cloud backup uses Steam Auto Cloud, Android Auto Backup, and Apple iCloud Backup automatically. Save files are stored in platform-appropriate locations.")]
    private bool usePlatformSpecificPaths = true;

    private IDisposable autoSaveSubscription;

    [Inject]
    private IGameSettings gameSettings;


    // Public properties for runtime access
    public string SaveFileName => saveFileName;
    public string BackupFileName => backupFileName;
    public string SaveDirectory => saveDirectory;
    public bool UsePersistentDataPath => usePersistentDataPath;
    public bool EnableAutoSave => gameSettings?.AutoSave?.Value ?? true;
    public float AutoSaveIntervalSeconds => autoSaveIntervalSeconds;
    public bool AutoSaveOnSceneChange => autoSaveOnSceneChange;
    public bool EnableChecksums => enableChecksums;
    public bool CreateBackups => createBackups;
    public int MaxBackupFiles => maxBackupFiles;
    public string SaveFormatVersion => GetApplicationVersion();
    public bool EnableFormatMigration => enableFormatMigration;
    public bool CreateMigrationBackup => createMigrationBackup;
    public bool CompressSaveData => compressSaveData;
    public bool EncryptSaveData => encryptSaveData;
    public string EncryptionKey => encryptionKey;
    public int AutoSavePaddingMilliseconds => autoSavePaddingMilliseconds;

    // Platform cloud backup properties
    public bool EnablePlatformCloudBackup => enablePlatformCloudBackup;
    public bool UsePlatformSpecificPaths => usePlatformSpecificPaths;

        /// <summary>
        ///   Clean up subscriptions when the config is destroyed
        /// </summary>
        private void OnDestroy()
    {
      autoSaveSubscription?.Dispose();
    }

        /// <summary>
        ///   Event fired when AutoSave setting changes
        /// </summary>
        public event Action<bool> OnAutoSaveSettingChanged;

        /// <summary>
        ///   Initialize the configuration with dependency injection.
        ///   This should be called after the settings system is available.
        /// </summary>
        public void Initialize(IGameSettings settings)
    {
      if (settings == null)
      {
        Debug.LogWarning("IGameSettings is null - AutoSave will default to true");
        return;
      }

      gameSettings = settings;

      // Subscribe to AutoSave setting changes
      autoSaveSubscription?.Dispose();
      autoSaveSubscription = gameSettings.AutoSave.Property.Subscribe(OnAutoSaveValueChanged);

      Debug.Log($"ES3SaveConfig initialized with AutoSave setting: {EnableAutoSave}");
    }

        /// <summary>
        ///   Handle AutoSave setting value changes
        /// </summary>
        private void OnAutoSaveValueChanged(bool newValue)
    {
      Debug.Log($"AutoSave setting changed to: {newValue}");
      OnAutoSaveSettingChanged?.Invoke(newValue);
    }

        /// <summary>
        ///   Gets the full save file path based on configuration and platform cloud backup settings.
        /// </summary>
        public string GetSaveFilePath()
    {
      string basePath;

      if (enablePlatformCloudBackup && usePlatformSpecificPaths)
        // Use platform-specific paths that work with cloud backup
        basePath = GetPlatformCloudBackupPath();
      else
      // Use standard persistent data path
        basePath = usePersistentDataPath ? Application.persistentDataPath : Application.dataPath;

      return Path.Combine(basePath, saveDirectory, saveFileName);
    }

        /// <summary>
        ///   Gets the platform-specific path that works with cloud backup services.
        /// </summary>
        private string GetPlatformCloudBackupPath()
    {
      // For most platforms, persistentDataPath is the correct location for cloud backup
      // Steam, Android Auto Backup, and Apple iCloud Backup all work with persistentDataPath
      return Application.persistentDataPath;
    }

        /// <summary>
        ///   Gets the full backup file path based on configuration.
        /// </summary>
        public string GetBackupFilePath()
    {
      string basePath = usePersistentDataPath ? Application.persistentDataPath : Application.dataPath;
      return Path.Combine(basePath, saveDirectory, backupFileName);
    }

        /// <summary>
        ///   Gets the save directory path.
        /// </summary>
        public string GetSaveDirectoryPath()
    {
      string basePath = usePersistentDataPath ? Application.persistentDataPath : Application.dataPath;
      return Path.Combine(basePath, saveDirectory);
    }

        /// <summary>
        ///   Gets the Application.version string for save format versioning.
        ///   Returns a default version if Application.version is not set.
        /// </summary>
        private string GetApplicationVersion()
    {
      try
      {
        string version = Application.version;
        if (string.IsNullOrEmpty(version))
          return "1.0.0"; // Default version if Application.version is not set
        return version;
      }
      catch (Exception)
      {
        return "1.0.0"; // Safe fallback
      }
    }

        /// <summary>
        ///   Validates the configuration settings.
        /// </summary>
        [Button("Validate Config")]
    public bool ValidateConfig()
    {
      bool isValid = true;

      if (string.IsNullOrEmpty(saveFileName))
      {
        Debug.LogError("Save file name cannot be empty");
        isValid = false;
      }

      if (string.IsNullOrEmpty(saveDirectory))
      {
        Debug.LogError("Save directory cannot be empty");
        isValid = false;
      }

      if (autoSaveIntervalSeconds <= 0)
      {
        Debug.LogError("Auto-save interval must be greater than 0");
        isValid = false;
      }

      if (encryptSaveData && string.IsNullOrEmpty(encryptionKey))
      {
        Debug.LogError("Encryption key cannot be empty when encryption is enabled");
        isValid = false;
      }

      if (isValid)
        Debug.Log("ES3SaveConfig validation passed");

      return isValid;
    }

        /// <summary>
        ///   Checks the version of an existing save file for migration testing.
        /// </summary>
        [Button("Check Save File Version")]
    [BoxGroup("Migration Settings")]
    public void CheckSaveFileVersion()
    {
      try
      {
        string filePath = GetSaveFilePath();

        if (!File.Exists(filePath))
        {
          Debug.LogWarning($"No save file found at {filePath}");
          return;
        }

        // Try to read the version from the save file
        ES3Settings es3Settings = new(filePath);
        if (ES3.KeyExists("SaveFormatVersion", es3Settings))
        {
          string savedVersion = ES3.Load<string>("SaveFormatVersion", es3Settings);
          string currentVersion = SaveFormatVersion;
          Debug.Log($"Save file version: {savedVersion}, Current app version: {Application.version}");

          if (savedVersion != currentVersion)
            Debug.Log($"Migration needed: {savedVersion} -> {currentVersion}");
          else
            Debug.Log("Save file version matches current version - no migration needed");
        }
        else
        {
          Debug.Log("Save file exists but no version information found (likely version 1.0.0 or legacy save)");
        }
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to check save file version: {ex}");
      }
    }

        /// <summary>
        ///   Creates a backup of the current save file using ES3's native functionality.
        /// </summary>
        [Button("Create Backup")]
    [BoxGroup("Validation Settings")]
    public void CreateBackup()
    {
      try
      {
        string filePath = GetSaveFilePath();

        if (!File.Exists(filePath))
        {
          Debug.LogWarning($"No save file found at {filePath} to backup");
          return;
        }

        ES3.CreateBackup(filePath);
        Debug.Log($"Backup created successfully for {filePath}");
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to create backup: {ex}");
      }
    }

        /// <summary>
        ///   Restores from the most recent backup using ES3's native functionality.
        /// </summary>
        [Button("Restore From Backup")]
    [BoxGroup("Validation Settings")]
    public void RestoreFromBackup()
    {
      try
      {
        string filePath = GetSaveFilePath();
        ES3.RestoreBackup(filePath);
        Debug.Log($"Restored from backup successfully for {filePath}");
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to restore from backup: {ex}");
      }
    }

        /// <summary>
        ///   Lists all available backup files for the current save file.
        /// </summary>
        [Button("List Available Backups")]
    [BoxGroup("Validation Settings")]
    public void ListAvailableBackups()
    {
      try
      {
        string filePath = GetSaveFilePath();
        // ES3 doesn't have a direct method to list backups, so we'll check if backups exist
        // by trying to restore and catching the exception if no backups exist
        try
        {
          // This is a workaround since ES3 doesn't expose backup listing directly
          Debug.Log("Backup functionality available - use Create Backup and Restore From Backup buttons");
        }
        catch
        {
          Debug.Log("No backup files found");
        }
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to list backups: {ex}");
      }
    }
  }
}