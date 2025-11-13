using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   Service interface for managing multiple save profiles using ES3
    /// </summary>
    public interface IProfileManager
  {
    ReactiveProperty<string> CurrentProfile { get; }
    ReactiveProperty<List<string>> AvailableProfiles { get; }
    ReactiveProperty<ProfileMetaData> CurrentProfileMetadata { get; }

    bool CreateProfile(string profileName, CancellationToken ct = default);
    (bool success, string actualProfileName) CreateProfileWithName(string profileName, CancellationToken ct = default);
    string GenerateUniqueProfileName(string baseName);
    bool DeleteProfile(string profileName, CancellationToken ct = default);
    UniTask<bool> LoadProfileAsync(string profileName, CancellationToken ct = default);
    bool SaveProfile(string profileName, CancellationToken ct = default);
    ProfileMetaData GetProfileMetaData(string profileName, CancellationToken ct = default);
    List<ProfileMetaData> GetAllProfileMetadata(CancellationToken ct = default);
    UniTask<string> GetMostRecentProfileAsync(CancellationToken ct = default);
    bool ProfileExists(string profileName);
    string GetProfileFilePath(string profileName);
    void RepairAllProfileMetadata();
    void CloseCurrentProfile();
  }
}