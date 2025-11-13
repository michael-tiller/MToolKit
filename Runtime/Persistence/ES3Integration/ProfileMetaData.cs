using System;
using UnityEngine;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   Profile metadata structure for storing profile information
    ///   Uses string-based DateTime storage to avoid serialization issues
    /// </summary>
    [Serializable]
  public class ProfileMetaData
  {
    [field: SerializeField]
    public string ProfileName { get; private set; }

    [field: SerializeField]
    public string CreatedTimeString { get; private set; }

    [field: SerializeField]
    public string LastSaveTime { get; private set; }

    [field: SerializeField]
    public string SaveFormatVersion { get; private set; }

    [field: SerializeField]
    public int SaveCounter { get; private set; }

    public ProfileMetaData()
    {
      ProfileName = "Player_" + Guid.NewGuid().ToString("N")[..8];
      LastSaveTime = "Never";
      CreatedTimeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      SaveFormatVersion = "1.0.0";
      SaveCounter = 0;
    }

    // Parameterized constructor for easier initialization
    public ProfileMetaData(string profileName = "Player", string lastSaveTime = "Never", string saveFormatVersion = "1.0.0", int saveCounter = 0, DateTime createdTime = default)
    {
      ProfileName = profileName;
      LastSaveTime = lastSaveTime;
      SaveFormatVersion = saveFormatVersion;
      SaveCounter = saveCounter;
      CreatedTimeString = createdTime != default ? createdTime.ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // Computed properties for DateTime access
    public DateTime CreatedTime => DateTime.TryParse(CreatedTimeString, out DateTime created) ? created : DateTime.Now;
  }
}