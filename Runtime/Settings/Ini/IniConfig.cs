using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Settings.Ini
{
  /// <summary>
  ///   Configuration asset for INI file settings.
  ///   Provides designer-friendly configuration for INI file behavior.
  /// </summary>
  [CreateAssetMenu(fileName = "IniConfig", menuName = "MToolKit/Settings/INI Config")]
  [InlineEditor]
  public class IniConfig : ScriptableObject
  {
    [Header("File Settings")]
    [BoxGroup("File Settings")]
    [SerializeField]
    [Required]
    private string iniFileName = "config.ini";

    [BoxGroup("File Settings")]
    [SerializeField]
    private string iniDirectory = "";

    [BoxGroup("File Settings")]
    [SerializeField]
    private bool usePersistentDataPath = true;

    // Public properties for runtime access
    public string IniFileName => iniFileName;
    public string IniDirectory => iniDirectory;
    public bool UsePersistentDataPath => usePersistentDataPath;

    /// <summary>
    ///   Gets the full INI file path based on configuration.
    /// </summary>
    public string GetIniFilePath()
    {
      string basePath = usePersistentDataPath ? Application.persistentDataPath : Application.dataPath;
      return Path.Combine(basePath, iniDirectory, iniFileName);
    }

    /// <summary>
    ///   Gets the INI directory path.
    /// </summary>
    public string GetIniDirectoryPath()
    {
      string basePath = usePersistentDataPath ? Application.persistentDataPath : Application.dataPath;
      return Path.Combine(basePath, iniDirectory);
    }

    /// <summary>
    ///   Validates the configuration settings.
    /// </summary>
    [Button("Validate Config")]
    public bool ValidateConfig()
    {
      bool isValid = true;

      if (string.IsNullOrEmpty(iniFileName))
      {
        Debug.LogError("INI file name cannot be empty");
        isValid = false;
      }

      if (string.IsNullOrEmpty(iniDirectory))
      {
        Debug.LogError("INI directory cannot be empty");
        isValid = false;
      }

      if (isValid)
        Debug.Log("IniConfig validation passed");

      return isValid;
    }
  }
}

