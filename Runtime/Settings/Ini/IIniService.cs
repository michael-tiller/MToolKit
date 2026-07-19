using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using R3;

namespace MToolKit.Runtime.Settings.Ini
{
  /// <summary>
  ///   Service interface for INI file configuration management.
  ///   Provides read/write access to INI files for global configuration.
  /// </summary>
  public interface IIniService
  {
    /// <summary>Completes when the INI file has finished its single, shared load.</summary>
    UniTask Initialization { get; }

    /// <summary>
    ///   Reactive property indicating if a save operation is in progress.
    /// </summary>
    ReactiveProperty<bool> IsSaving { get; }

    /// <summary>
    ///   Reactive property indicating if a load operation is in progress.
    /// </summary>
    ReactiveProperty<bool> IsLoading { get; }

    /// <summary>
    ///   Gets all values from the INI file as a dictionary.
    ///   Key format: "Section.Key" (e.g., "Graphics.Resolution")
    /// </summary>
    IReadOnlyDictionary<string, string> AllValues { get; }

    /// <summary>
    ///   Loads the INI file and populates AllValues.
    ///   Creates a default INI file if one doesn't exist.
    /// </summary>
    UniTask LoadAsync(CancellationToken ct = default);

    /// <summary>
    ///   Saves all current values to the INI file.
    /// </summary>
    UniTask SaveAsync(CancellationToken ct = default);

    /// <summary>
    ///   Gets a value from the INI file.
    /// </summary>
    /// <param name="section">The section name (e.g., "Graphics")</param>
    /// <param name="key">The key name (e.g., "Resolution")</param>
    /// <param name="defaultValue">Default value if key doesn't exist</param>
    /// <returns>The value as a string, or defaultValue if not found</returns>
    string GetValue(string section, string key, string defaultValue = null);

    /// <summary>
    ///   Gets a value from the INI file and converts it to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="section">The section name</param>
    /// <param name="key">The key name</param>
    /// <param name="defaultValue">Default value if key doesn't exist</param>
    /// <returns>The converted value, or defaultValue if not found</returns>
    T GetValue<T>(string section, string key, T defaultValue = default);

    /// <summary>
    ///   Sets a value in the INI file (in memory only, call SaveAsync to persist).
    /// </summary>
    /// <param name="section">The section name</param>
    /// <param name="key">The key name</param>
    /// <param name="value">The value to set</param>
    void SetValue(string section, string key, object value);

    /// <summary>
    ///   Checks if a key exists in the INI file.
    /// </summary>
    /// <param name="section">The section name</param>
    /// <param name="key">The key name</param>
    /// <returns>True if the key exists, false otherwise</returns>
    bool KeyExists(string section, string key);

    /// <summary>
    ///   Deletes a key from the INI file (in memory only, call SaveAsync to persist).
    /// </summary>
    /// <param name="section">The section name</param>
    /// <param name="key">The key name</param>
    void DeleteKey(string section, string key);

    /// <summary>
    ///   Gets all keys in a section.
    /// </summary>
    /// <param name="section">The section name</param>
    /// <returns>List of key names in the section</returns>
    IEnumerable<string> GetKeys(string section);

    /// <summary>
    ///   Gets all section names.
    /// </summary>
    /// <returns>List of section names</returns>
    IEnumerable<string> GetSections();

    /// <summary>
    ///   Populates the INI file with default values from the SettingsSystem.
    ///   This should be called after SettingsSystem is initialized to ensure
    ///   the INI file contains system default values if it was just created.
    /// </summary>
    /// <param name="settingsSystem">The SettingsSystem to get defaults from</param>
    void PopulateDefaultsFromSettingsSystem(ISettingsSystem settingsSystem);
  }
}

