using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Interfaces;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
  /// <summary>
  ///   Manages multiple save profiles using ES3, where each profile is a separate save file
  ///   Integrates with the existing ES3GameSavePlugin architecture
  /// </summary>
  [Serializable]
  public class ProfileManager : IProfileManager, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ProfileManager>().ForFeature("Persistence.ES3"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private readonly ES3SaveConfig config;

    private readonly IES3Service es3Service;
    private readonly string profilesDirectory;
    private volatile bool isDisposed;
    private int pendingWorldSeed;
    private IDisposable currentProfileSubscription;

    public ProfileManager(IES3Service es3Service, ES3SaveConfig config)
    {
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));
      this.config = config ?? throw new ArgumentNullException(nameof(config));

      // Use the same directory as the main save config but with a profiles subdirectory
      profilesDirectory = Path.Combine(config.GetSaveDirectoryPath(), "Profiles");

      log.ForMethod().Verbose("ProfileManager created with profiles directory: {0}", profilesDirectory);

      // Ensure profiles directory exists
      if (!Directory.Exists(profilesDirectory))
      {
        Directory.CreateDirectory(profilesDirectory);
        log.ForMethod().Verbose("Created profiles directory: {0}", profilesDirectory);
      }

      // Initialize available profiles
      RefreshAvailableProfiles();

      // Subscribe to CurrentProfile changes to update CurrentProfileMetadata
      currentProfileSubscription = CurrentProfile.Subscribe(OnCurrentProfileChanged);
    }

    [ShowInInspector]
    [ReadOnly]
    public ProfileMetaData CurrentProfileMetadataDebug => CurrentProfileMetadata.Value;

    [ShowInInspector]
    [ReadOnly]
    public string CurrentProfileDebug => CurrentProfile.Value;

    [ShowInInspector]
    [ReadOnly]
    public int AvailableProfilesCount => AvailableProfiles.Value.Count;

    public void Dispose()
    {
      if (isDisposed)
        return;

      isDisposed = true;

      // Dispose subscriptions
      currentProfileSubscription?.Dispose();
      currentProfileSubscription = null;

      // Dispose reactive properties
      CurrentProfile?.Dispose();
      AvailableProfiles?.Dispose();
      CurrentProfileMetadata?.Dispose();

      log.ForMethod().Verbose("ProfileManager disposed");
    }

    public ReactiveProperty<string> CurrentProfile { get; } = new(string.Empty);
    public ReactiveProperty<List<string>> AvailableProfiles { get; } = new(new List<string>());
    public ReactiveProperty<ProfileMetaData> CurrentProfileMetadata { get; } = new(null);

    /// <summary>
    ///   Closes the current profile (clears it without deleting the save file)
    ///   This should be called when exiting the game or returning to menu
    /// </summary>
    public void CloseCurrentProfile()
    {
      if (isDisposed)
        return;

      try
      {
        if (!string.IsNullOrEmpty(CurrentProfile.Value))
        {
          log.ForMethod().Information("Closing current profile: {0}", CurrentProfile.Value);
          CurrentProfile.Value = string.Empty;
          log.ForMethod().Information("Current profile closed");
        }
        else
        {
          log.ForMethod().Verbose("No current profile to close");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to close current profile: {Message}", ex.Message);
      }
    }

    /// <summary>
    ///   Gets the file path for a specific profile
    /// </summary>
    public string GetProfileFilePath(string profileName)
    {
      if (string.IsNullOrEmpty(profileName))
        throw new ArgumentException("Profile name cannot be null or empty", nameof(profileName));

      return Path.Combine(profilesDirectory, $"{profileName}.es3");
    }

    /// <summary>
    ///   Checks if a profile exists
    /// </summary>
    public bool ProfileExists(string profileName)
    {
      if (string.IsNullOrEmpty(profileName))
        return false;

      string filePath = GetProfileFilePath(profileName);
      return File.Exists(filePath);
    }

    /// <summary>
    ///   Generates a unique profile name by appending a numeric suffix if the name already exists
    /// </summary>
    public string GenerateUniqueProfileName(string baseName)
    {
      if (string.IsNullOrEmpty(baseName))
        return "Player_0001";

      // If the name doesn't exist, return it as-is
      if (!ProfileExists(baseName))
        return baseName;

      // Try to find a unique name by appending numbers
      for (int i = 1; i <= 9999; i++)
      {
        string candidateName = $"{baseName}_{i:D4}";
        if (!ProfileExists(candidateName))
        {
          log.ForMethod().Verbose("Generated unique profile name: {0} (original: {1})", candidateName, baseName);
          return candidateName;
        }
      }

      // If we can't find a unique name with the pattern, append a random hash
      string randomSuffix = Guid.NewGuid().ToString("N")[..8];
      string fallbackName = $"{baseName}_{randomSuffix}";
      log.ForMethod().Verbose("Generated fallback unique profile name: {0} (original: {1})", fallbackName, baseName);
      return fallbackName;
    }

    /// <summary>
    ///   Creates a new profile with initial metadata and returns the actual profile name used
    /// </summary>
    public (bool success, string actualProfileName) CreateProfileWithName(string profileName, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      if (string.IsNullOrEmpty(profileName))
      {
        log.ForMethod().Error("Cannot create profile with null or empty name");
        return (false, string.Empty);
      }

      // Generate a unique name if the requested name already exists
      string actualProfileName = GenerateUniqueProfileName(profileName);

      if (actualProfileName != profileName)
        log.ForMethod().Information("Profile name '{0}' already exists, using unique name: '{1}'", profileName, actualProfileName);

      try
      {
        string filePath = GetProfileFilePath(actualProfileName);
        ProfileMetaData metadata = new(actualProfileName, "Never", config.SaveFormatVersion);

        // Create the profile file with initial metadata using ES3Settings
        ES3Settings es3Settings = new(filePath)
        {
          compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
          encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
          encryptionPassword = config.EncryptionKey
        };

        // Debug: log what we're actually saving
        log.ForMethod().Information("Creating ProfileMetaData: ProfileName='{0}', LastSaveTime='{1}', SaveFormatVersion='{2}', SaveCounter={3}",
          metadata.ProfileName, metadata.LastSaveTime, metadata.SaveFormatVersion, metadata.SaveCounter);

        // Save ProfileMetadata object with full settings (no individual fields to avoid duplication)
        ES3.Save("ProfileMetadata", metadata, es3Settings);

        // Verify it was saved correctly
        ProfileMetaData verification = ES3.Load<ProfileMetaData>("ProfileMetadata", es3Settings);
        log.ForMethod().Information("Verification after save: ProfileName='{0}', LastSaveTime='{1}', SaveFormatVersion='{2}', SaveCounter={3}",
          verification?.ProfileName ?? "NULL", verification?.LastSaveTime ?? "NULL", verification?.SaveFormatVersion ?? "NULL", verification?.SaveCounter ?? -1);

        log.ForMethod().Information("Created new profile: {0} at {1}", actualProfileName, filePath);

        // Refresh available profiles
        RefreshAvailableProfiles();

        return (true, actualProfileName);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to create profile {0}: {Message}", actualProfileName, ex.Message);
        return (false, string.Empty);
      }
    }

    /// <summary>
    ///   Creates a new profile with initial metadata
    /// </summary>
    public bool CreateProfile(string profileName, CancellationToken ct = default)
    {
      (bool success, _) = CreateProfileWithName(profileName, ct);
      return success;
    }

    /// <summary>
    ///   Deletes a profile and its save file
    /// </summary>
    public bool DeleteProfile(string profileName, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      if (string.IsNullOrEmpty(profileName))
      {
        log.ForMethod().Error("Cannot delete profile with null or empty name");
        return false;
      }

      if (!ProfileExists(profileName))
      {
        log.ForMethod().Warning("Profile {0} does not exist", profileName);
        return false;
      }

      try
      {
        string filePath = GetProfileFilePath(profileName);

        // If this is the current profile, clear it
        if (CurrentProfile.Value == profileName)
          CurrentProfile.Value = string.Empty;

        File.Delete(filePath);

        log.ForMethod().Information("Deleted profile: {0}", profileName);

        // Refresh available profiles
        RefreshAvailableProfiles();

        return true;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to delete profile {0}: {Message}", profileName, ex.Message);
        return false;
      }
    }

    /// <summary>
    ///   Loads a profile (sets it as current and loads its data)
    /// </summary>
    public UniTask<bool> LoadProfileAsync(string profileName, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      if (string.IsNullOrEmpty(profileName))
      {
        log.ForMethod().Error("Cannot load profile with null or empty name");
        return UniTask.FromResult(false);
      }

      if (!ProfileExists(profileName))
      {
        log.ForMethod().Warning("Profile {0} does not exist", profileName);
        return UniTask.FromResult(false);
      }

      try
      {
        // Set as current profile
        CurrentProfile.Value = profileName;

        // Update the ES3Service to use the profile-specific file path
        string profileFilePath = GetProfileFilePath(profileName);
        log.ForMethod().Verbose("Loading profile: {0} from file: {1}", profileName, profileFilePath);

        // Create a new ES3Settings for the profile file
        ES3Settings profileSettings = new(profileFilePath)
        {
          compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
          encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
          encryptionPassword = config.EncryptionKey
        };

        // Notify the ES3Service about the profile change
        // The ProfileAwareES3Service will automatically handle the file path switching
        NotifyES3ServiceOfProfileChange(profileFilePath);

        log.ForMethod().Verbose("Successfully loaded profile: {0}", profileName);
        return UniTask.FromResult(true);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load profile {0}: {Message}", profileName, ex.Message);
        return UniTask.FromResult(false);
      }
    }

    /// <summary>
    ///   Saves the current profile
    /// </summary>
    public bool SaveProfile(string profileName, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      if (string.IsNullOrEmpty(profileName))
      {
        log.ForMethod().Error("Cannot save profile with null or empty name");
        return false;
      }

      try
      {
        string filePath = GetProfileFilePath(profileName);
        ES3Settings es3Settings = new(filePath)
        {
          compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
          encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
          encryptionPassword = config.EncryptionKey
        };

        // Update metadata by creating new instance with updated values
        ProfileMetaData existingMetadata = GetProfileMetaData(profileName, ct);
        if (existingMetadata != null)
        {
          int worldSeed = pendingWorldSeed != 0 ? pendingWorldSeed : existingMetadata.WorldSeed;
          ProfileMetaData updatedMetadata = new(
            existingMetadata.ProfileName,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            config.SaveFormatVersion,
            existingMetadata.SaveCounter + 1,
            existingMetadata.CreatedTime,
            worldSeed
            );

          // Save ProfileMetadata object only (no individual fields to avoid duplication)
          ES3.Save("ProfileMetadata", updatedMetadata, es3Settings);
        }

        log.ForMethod().Information("Saved profile: {0}", profileName);

        // Update current profile metadata if this is the current profile
        if (CurrentProfile.Value == profileName)
          OnCurrentProfileChanged(profileName);

        return true;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save profile {0}: {Message}", profileName, ex.Message);
        return false;
      }
    }

    /// <summary>
    ///   Gets metadata for a specific profile
    /// </summary>
    public ProfileMetaData GetProfileMetaData(string profileName, CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      if (string.IsNullOrEmpty(profileName))
      {
        log.ForMethod().Warning("GetProfileMetaData: Profile name is null or empty");
        return null;
      }

      if (!ProfileExists(profileName))
      {
        log.ForMethod().Warning("GetProfileMetaData: Profile '{0}' does not exist", profileName);
        return null;
      }

      try
      {
        string filePath = GetProfileFilePath(profileName);
        log.ForMethod().Verbose("GetProfileMetaData: Loading metadata for profile '{0}' from path: {1}", profileName, filePath);

        ES3Settings es3Settings = new(filePath)
        {
          compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
          encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
          encryptionPassword = config.EncryptionKey
        };

        if (ES3.KeyExists("ProfileMetadata", es3Settings))
        {
          ProfileMetaData metadata = ES3.Load<ProfileMetaData>("ProfileMetadata", es3Settings);
          log.ForMethod().Verbose("GetProfileMetaData: Loaded ProfileMetadata for '{0}' - ProfileName: '{1}', LastSaveTime: '{2}', SaveFormatVersion: '{3}', SaveCounter: {4}",
            profileName, metadata?.ProfileName ?? "null", metadata?.LastSaveTime ?? "null", metadata?.SaveFormatVersion ?? "null", metadata?.SaveCounter ?? -1);

          // Check if the metadata is valid (has a ProfileName)
          if (metadata != null && !string.IsNullOrEmpty(metadata.ProfileName))
          {
            log.ForMethod().Verbose("GetProfileMetaData: ProfileMetadata is valid for '{0}'", profileName);
            return metadata;
          }
          log.ForMethod().Warning("GetProfileMetaData: ProfileMetadata is empty for '{0}', fixing it", profileName);

          // Fix the empty ProfileMetadata by creating a proper one from individual fields
          string profileNameVal = ES3.KeyExists("ProfileName", es3Settings) ? ES3.Load<string>("ProfileName", es3Settings) : profileName;
          string lastSaveTimeVal = ES3.KeyExists("LastSaveTime", es3Settings) ? ES3.Load<string>("LastSaveTime", es3Settings) : "Never";
          string saveFormatVersionVal = ES3.KeyExists("SaveFormatVersion", es3Settings) ? ES3.Load<string>("SaveFormatVersion", es3Settings) : config.SaveFormatVersion;
          int saveCounterVal = ES3.KeyExists("SaveCounter", es3Settings) ? ES3.Load<int>("SaveCounter", es3Settings) : 0;

          ProfileMetaData fixedMetadata = new(profileNameVal, lastSaveTimeVal, saveFormatVersionVal, saveCounterVal);

          // Since ProfileMetadata object serialization is problematic, just ensure individual fields are saved
          try
          {
            // Ensure all individual fields are saved (these work reliably)
            ES3.Save("ProfileName", fixedMetadata.ProfileName, es3Settings);
            ES3.Save("LastSaveTime", fixedMetadata.LastSaveTime, es3Settings);
            ES3.Save("SaveFormatVersion", fixedMetadata.SaveFormatVersion, es3Settings);
            ES3.Save("SaveCounter", fixedMetadata.SaveCounter, es3Settings);
            ES3.Save("CreatedTime", fixedMetadata.CreatedTime, es3Settings);

            log.ForMethod().Information("Ensured individual fields are saved for '{0}': ProfileName='{1}', LastSaveTime='{2}', SaveFormatVersion='{3}', SaveCounter={4}",
              profileName, fixedMetadata.ProfileName, fixedMetadata.LastSaveTime, fixedMetadata.SaveFormatVersion, fixedMetadata.SaveCounter);

            // Skip ProfileMetadata object - it's problematic and individual fields work fine
            log.ForMethod().Information("Skipping ProfileMetadata object save - individual fields are sufficient for '{0}'", profileName);

            return fixedMetadata;
          }
          catch (Exception saveEx)
          {
            log.ForMethod().Error(saveEx, "Failed to save individual fields for '{0}': {1}, using fallback", profileName, saveEx.Message);
            // Fall through to fallback logic
          }
        }

        // Fallback: create metadata from individual fields or file info (READ-ONLY, no saving)
        log.ForMethod().Verbose("GetProfileMetaData: Creating fallback metadata for '{0}' from individual fields (read-only)", profileName);

        // Try to load from individual fields first
        string profileNameValue = ES3.KeyExists("ProfileName", es3Settings) ? ES3.Load<string>("ProfileName", es3Settings) : profileName;
        string lastSaveTimeValue = ES3.KeyExists("LastSaveTime", es3Settings) ? ES3.Load<string>("LastSaveTime", es3Settings) : "Never";
        string saveFormatVersionValue = ES3.KeyExists("SaveFormatVersion", es3Settings) ? ES3.Load<string>("SaveFormatVersion", es3Settings) : config.SaveFormatVersion;
        int saveCounterValue = ES3.KeyExists("SaveCounter", es3Settings) ? ES3.Load<int>("SaveCounter", es3Settings) : 0;

        ProfileMetaData fallbackMetadata = new(profileNameValue, lastSaveTimeValue, saveFormatVersionValue, saveCounterValue);

        // If individual fields don't exist, fall back to file info (READ-ONLY)
        if (string.IsNullOrEmpty(fallbackMetadata.LastSaveTime) || fallbackMetadata.LastSaveTime == "Never")
        {
          FileInfo fileInfo = new(filePath);
          if (fileInfo.Exists)
            // Create new metadata with file info
            fallbackMetadata = new ProfileMetaData(profileName, fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), config.SaveFormatVersion);
        }

        log.ForMethod().Verbose(
          "GetProfileMetaData: Created fallback metadata for '{0}' - ProfileName='{1}', LastSaveTime='{2}', SaveFormatVersion='{3}', SaveCounter={4} (read-only)",
          profileName, fallbackMetadata.ProfileName, fallbackMetadata.LastSaveTime, fallbackMetadata.SaveFormatVersion, fallbackMetadata.SaveCounter);
        return fallbackMetadata;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to get metadata for profile {0}: {Message}", profileName, ex.Message);
        return null;
      }
    }

    /// <summary>
    ///   Gets metadata for all available profiles
    /// </summary>
    public List<ProfileMetaData> GetAllProfileMetadata(CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      List<ProfileMetaData> metadataList = new();
      List<string> availableProfiles = AvailableProfiles.Value;

      log.ForMethod().Verbose("GetAllProfileMetadata: Found {0} available profiles: [{1}]",
        availableProfiles?.Count ?? 0,
        availableProfiles != null ? string.Join(", ", availableProfiles) : "null");

      foreach (string profileName in availableProfiles ?? new List<string>())
      {
        ProfileMetaData metadata = GetProfileMetaData(profileName, ct);
        if (metadata != null)
        {
          metadataList.Add(metadata);
          log.ForMethod().Verbose("Successfully loaded metadata for profile: {0}", profileName);
        }
        else
        {
          log.ForMethod().Warning("Failed to load metadata for profile: {0}", profileName);
        }
      }

      log.ForMethod().Verbose("GetAllProfileMetadata: Returning {0} valid metadata entries", metadataList.Count);
      return metadataList.OrderByDescending(m =>
      {
        if (m.LastSaveTime != null && DateTime.TryParse(m.LastSaveTime, out DateTime lastSaveTime))
          return lastSaveTime;
        return DateTime.MinValue;
      }).ToList();
    }

    /// <summary>
    ///   Gets the most recent profile by last save time
    /// </summary>
    public UniTask<string> GetMostRecentProfileAsync(CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      try
      {
        List<ProfileMetaData> allMetaData = GetAllProfileMetadata(ct);
        log.ForMethod().Verbose("GetMostRecentProfileAsync: Got {0} metadata entries, allMetaData is null: {1}",
          allMetaData?.Count ?? -1, allMetaData == null);

        if (allMetaData == null)
        {
          log.ForMethod().Warning("GetMostRecentProfileAsync: allMetaData is null");
          return UniTask.FromResult(string.Empty);
        }

        if (allMetaData.Count == 0)
        {
          log.ForMethod().Warning("GetMostRecentProfileAsync: allMetaData is empty");
          return UniTask.FromResult(string.Empty);
        }

        log.ForMethod().Verbose("GetMostRecentProfileAsync: Ordering {0} profiles by LastSaveTime", allMetaData.Count);
        List<ProfileMetaData> orderedProfiles = allMetaData.OrderByDescending(m =>
        {
          if (m.LastSaveTime != null && DateTime.TryParse(m.LastSaveTime, out DateTime lastSaveTime))
            return lastSaveTime;
          return DateTime.MinValue;
        }).ToList();
        log.ForMethod().Verbose("GetMostRecentProfileAsync: Ordered profiles count: {0}", orderedProfiles.Count);

        ProfileMetaData mostRecent = orderedProfiles.FirstOrDefault();
        if (mostRecent == null)
        {
          log.ForMethod().Warning("GetMostRecentProfileAsync: FirstOrDefault returned null");
          return UniTask.FromResult(string.Empty);
        }

        log.ForMethod().Verbose("GetMostRecentProfileAsync: Most recent profile: {0} (last saved: {1})", mostRecent.ProfileName, mostRecent.LastSaveTime);
        return UniTask.FromResult(mostRecent.ProfileName);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to get most recent profile: {Message}", ex.Message);
        return UniTask.FromResult(string.Empty);
      }
    }

    /// <summary>
    ///   Forces repair of all profile metadata - useful for fixing corrupted save files
    /// </summary>
    public void SetWorldSeed(int seed)
    {
      pendingWorldSeed = seed;

      // Update live metadata so consumers that read CurrentProfileMetadata before the next
      // SaveProfile (e.g. WorldGenerationPlugin during scene load) see the new seed.
      ProfileMetaData current = CurrentProfileMetadata.Value;
      if (current != null)
      {
        current.WorldSeed = seed;
        CurrentProfileMetadata.OnNext(current);
      }

      log.ForMethod().Debug("Set world seed: {Seed}", seed);
    }

    public void RepairAllProfileMetadata()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      log.ForMethod().Information("Starting repair of all profile metadata");

      List<string> availableProfiles = AvailableProfiles.Value;
      foreach (string profileName in availableProfiles ?? new List<string>())
        try
        {
          string filePath = GetProfileFilePath(profileName);
          ES3Settings es3Settings = new(filePath)
          {
            compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
            encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
            encryptionPassword = config.EncryptionKey
          };

          log.ForMethod().Information("Repairing metadata for profile: {0}", profileName);
          ProfileMetaData repairedMetadata = RepairProfileMetadata(profileName, es3Settings);

          if (repairedMetadata != null)
            log.ForMethod().Information("Successfully repaired metadata for profile: {0}", profileName);
          else
            log.ForMethod().Warning("Failed to repair metadata for profile: {0}", profileName);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Error repairing metadata for profile {0}: {Message}", profileName, ex.Message);
        }

      log.ForMethod().Information("Completed repair of all profile metadata");
    }

    /// <summary>
    ///   Handles changes to the current profile by updating the metadata
    /// </summary>
    private void OnCurrentProfileChanged(string profileName)
    {
      if (isDisposed)
        return;

      try
      {
        if (string.IsNullOrEmpty(profileName))
        {
          CurrentProfileMetadata.Value = null;
          log.ForMethod().Verbose("Current profile cleared, metadata set to null");
        }
        else
        {
          ProfileMetaData metadata = GetProfileMetaData(profileName);
          CurrentProfileMetadata.Value = metadata;
          log.ForMethod().Verbose("Updated current profile metadata for: {0}", profileName);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to update current profile metadata for {0}: {Message}", profileName, ex.Message);
        CurrentProfileMetadata.Value = null;
      }
    }

    /// <summary>
    ///   Notifies the ES3Service about the profile change
    ///   The ProfileAwareES3Service will automatically handle the file path switching
    /// </summary>
    private void NotifyES3ServiceOfProfileChange(string profileFilePath)
    {
      try
      {
        // The ProfileAwareES3Service automatically listens to CurrentProfile changes
        // and switches file paths accordingly, so we just need to log the change
        log.ForMethod().Verbose("Profile loaded - ES3Service will use path: {0}", profileFilePath);

        // If we're using ProfileAwareES3Service, it will automatically handle the path switching
        // No additional action needed as it subscribes to CurrentProfile changes
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to notify ES3Service of profile change: {Message}", ex.Message);
      }
    }

    /// <summary>
    ///   Repairs metadata once during initialization without triggering additional saves
    /// </summary>
    private void RepairAllProfileMetadataOnce()
    {
      if (isDisposed)
        return;

      log.ForMethod().Information("Starting one-time metadata repair for all profiles");

      List<string> availableProfiles = AvailableProfiles.Value;
      foreach (string profileName in availableProfiles ?? new List<string>())
        try
        {
          string filePath = GetProfileFilePath(profileName);
          ES3Settings es3Settings = new(filePath)
          {
            compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
            encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
            encryptionPassword = config.EncryptionKey
          };

          // Check if ProfileMetadata exists but is empty
          if (ES3.KeyExists("ProfileMetadata", es3Settings))
          {
            ProfileMetaData metadata = ES3.Load<ProfileMetaData>("ProfileMetadata", es3Settings);
            if (metadata == null || string.IsNullOrEmpty(metadata.ProfileName))
            {
              log.ForMethod().Information("Found corrupted metadata for profile: {0}, repairing once", profileName);

              // Create repaired metadata from individual fields
              ProfileMetaData repairedMetadata = new(
                ES3.KeyExists("ProfileName", es3Settings) ? ES3.Load<string>("ProfileName", es3Settings) : profileName,
                ES3.KeyExists("LastSaveTime", es3Settings) ? ES3.Load<string>("LastSaveTime", es3Settings) : "Never",
                ES3.KeyExists("SaveFormatVersion", es3Settings) ? ES3.Load<string>("SaveFormatVersion", es3Settings) : config.SaveFormatVersion,
                ES3.KeyExists("SaveCounter", es3Settings) ? ES3.Load<int>("SaveCounter", es3Settings) : 0,
                ES3.KeyExists("CreatedTime", es3Settings) ? ES3.Load<DateTime>("CreatedTime", es3Settings) : DateTime.Now
                );

              // Save ProfileMetadata object only (no individual fields to avoid duplication)
              ES3.Save("ProfileMetadata", repairedMetadata, es3Settings);

              log.ForMethod().Information("Repaired metadata for profile: {0}", profileName);
            }
            else
            {
              log.ForMethod().Verbose("Profile {0} metadata is already valid", profileName);
            }
          }
          else
          {
            log.ForMethod().Verbose("Profile {0} has no ProfileMetadata, individual fields will be used", profileName);
          }
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Error repairing metadata for profile {0}: {Message}", profileName, ex.Message);
        }

      log.ForMethod().Information("Completed one-time metadata repair");
    }

    /// <summary>
    ///   Migrates existing save files to remove redundant individual field storage
    ///   This should be called once to clean up old save files
    /// </summary>
    public void MigrateSaveFilesToCleanFormat()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(ProfileManager));

      log.ForMethod().Information("Starting migration of save files to clean format");

      List<string> availableProfiles = AvailableProfiles.Value;
      foreach (string profileName in availableProfiles ?? new List<string>())
        try
        {
          string filePath = GetProfileFilePath(profileName);
          ES3Settings es3Settings = new(filePath)
          {
            compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
            encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
            encryptionPassword = config.EncryptionKey
          };

          // Check if this file has redundant individual fields
          bool hasRedundantFields = ES3.KeyExists("ProfileName", es3Settings) ||
                                    ES3.KeyExists("LastSaveTime", es3Settings) ||
                                    ES3.KeyExists("SaveFormatVersion", es3Settings) ||
                                    ES3.KeyExists("SaveCounter", es3Settings) ||
                                    ES3.KeyExists("CreatedTime", es3Settings);

          if (hasRedundantFields)
          {
            log.ForMethod().Information("Migrating profile '{0}' - removing redundant individual fields", profileName);

            // Load the ProfileMetaData object (this should contain all the data we need)
            ProfileMetaData metadata = null;
            if (ES3.KeyExists("ProfileMetadata", es3Settings))
              metadata = ES3.Load<ProfileMetaData>("ProfileMetadata", es3Settings);

            // If ProfileMetaData doesn't exist or is corrupted, create it from individual fields
            if (metadata == null || string.IsNullOrEmpty(metadata.ProfileName))
            {
              log.ForMethod().Information("ProfileMetaData missing or corrupted for '{0}', creating from individual fields", profileName);
              metadata = new ProfileMetaData(
                ES3.KeyExists("ProfileName", es3Settings) ? ES3.Load<string>("ProfileName", es3Settings) : profileName,
                ES3.KeyExists("LastSaveTime", es3Settings) ? ES3.Load<string>("LastSaveTime", es3Settings) : "Never",
                ES3.KeyExists("SaveFormatVersion", es3Settings) ? ES3.Load<string>("SaveFormatVersion", es3Settings) : config.SaveFormatVersion,
                ES3.KeyExists("SaveCounter", es3Settings) ? ES3.Load<int>("SaveCounter", es3Settings) : 0,
                ES3.KeyExists("CreatedTime", es3Settings) ? ES3.Load<DateTime>("CreatedTime", es3Settings) : DateTime.Now
                );
            }

            // Delete redundant individual fields
            ES3.DeleteKey("ProfileName", es3Settings);
            ES3.DeleteKey("LastSaveTime", es3Settings);
            ES3.DeleteKey("SaveFormatVersion", es3Settings);
            ES3.DeleteKey("SaveCounter", es3Settings);
            ES3.DeleteKey("CreatedTime", es3Settings);

            // Ensure ProfileMetaData object is properly saved
            ES3.Save("ProfileMetadata", metadata, es3Settings);

            log.ForMethod().Information("Successfully migrated profile '{0}' to clean format", profileName);
          }
          else
          {
            log.ForMethod().Verbose("Profile '{0}' is already in clean format", profileName);
          }
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Error migrating profile '{0}': {Message}", profileName, ex.Message);
        }

      log.ForMethod().Information("Completed migration of save files to clean format");
    }

    /// <summary>
    ///   Repairs corrupted profile metadata by creating a new ProfileMetaData object with proper values
    /// </summary>
    private ProfileMetaData RepairProfileMetadata(string profileName, ES3Settings es3Settings)
    {
      try
      {
        log.ForMethod().Information("Repairing corrupted metadata for profile: {0}", profileName);

        // Create a new ProfileMetaData object with proper values
        ProfileMetaData repairedMetadata = new(
          profileName,
          ES3.KeyExists("LastSaveTime", es3Settings) ? ES3.Load<string>("LastSaveTime", es3Settings) : "Never",
          ES3.KeyExists("SaveFormatVersion", es3Settings) ? ES3.Load<string>("SaveFormatVersion", es3Settings) : config.SaveFormatVersion,
          ES3.KeyExists("SaveCounter", es3Settings) ? ES3.Load<int>("SaveCounter", es3Settings) : 0,
          DateTime.Now // We don't know the original creation time
          );

        log.ForMethod().Information("Repaired metadata for '{0}': ProfileName='{1}', LastSaveTime='{2}', SaveFormatVersion='{3}', SaveCounter={4}",
          profileName, repairedMetadata.ProfileName, repairedMetadata.LastSaveTime, repairedMetadata.SaveFormatVersion, repairedMetadata.SaveCounter);

        // Create proper ES3Settings with compression and encryption configuration
        string filePath = GetProfileFilePath(profileName);
        ES3Settings properES3Settings = new(filePath)
        {
          compressionType = config.CompressSaveData ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
          encryptionType = config.EncryptSaveData ? ES3.EncryptionType.AES : ES3.EncryptionType.None,
          encryptionPassword = config.EncryptionKey
        };

        // Delete the corrupted ProfileMetadata first, then recreate it
        if (ES3.KeyExists("ProfileMetadata", properES3Settings))
        {
          ES3.DeleteKey("ProfileMetadata", properES3Settings);
          log.ForMethod().Information("Deleted corrupted ProfileMetadata for profile: {0}", profileName);
        }

        // Save the repaired metadata back to the file with proper settings
        // Only save the ProfileMetadata object (no individual fields to avoid duplication)
        try
        {
          ES3.Save("ProfileMetadata", repairedMetadata, properES3Settings);
          log.ForMethod().Information("Saved ProfileMetadata using direct save");
        }
        catch (Exception ex)
        {
          log.ForMethod().Warning("Direct save failed: {0}, trying JSON approach", ex.Message);

          // Fallback: Try saving as JSON string
          try
          {
            string json = JsonUtility.ToJson(repairedMetadata);
            ES3.Save("ProfileMetadataJSON", json, properES3Settings);
            log.ForMethod().Information("Saved ProfileMetadata as JSON string");
          }
          catch (Exception jsonEx)
          {
            log.ForMethod().Warning("JSON save also failed: {0}", jsonEx.Message);
          }
        }

        // Verify the save worked by trying to load it back
        ProfileMetaData verificationMetadata = ES3.Load<ProfileMetaData>("ProfileMetadata", properES3Settings);
        log.ForMethod().Information("Verification - loaded back ProfileMetadata: ProfileName='{0}', LastSaveTime='{1}', SaveFormatVersion='{2}', SaveCounter={3}",
          verificationMetadata?.ProfileName ?? "null",
          verificationMetadata?.LastSaveTime ?? "null",
          verificationMetadata?.SaveFormatVersion ?? "null",
          verificationMetadata?.SaveCounter ?? -1);

        log.ForMethod().Information("Successfully repaired and saved metadata for profile: {0}", profileName);
        return repairedMetadata;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to repair metadata for profile {0}: {Message}", profileName, ex.Message);
        return null;
      }
    }

    /// <summary>
    ///   Refreshes the list of available profiles from the file system
    /// </summary>
    private void RefreshAvailableProfiles()
    {
      try
      {
        if (!Directory.Exists(profilesDirectory))
        {
          AvailableProfiles.Value = new List<string>();
          return;
        }

        List<string> profileFiles = Directory.GetFiles(profilesDirectory, "*.es3")
          .Select(Path.GetFileNameWithoutExtension)
          .Where(name => !string.IsNullOrEmpty(name))
          .OrderBy(name => name)
          .ToList();

        AvailableProfiles.Value = profileFiles;

        log.ForMethod().Verbose("Refreshed available profiles: {0}", profileFiles.Count);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to refresh available profiles: {Message}", ex.Message);
        AvailableProfiles.Value = new List<string>();
      }
    }
  }
}