using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.Ini
{
  /// <summary>
  ///   Service implementation for INI file configuration management.
  ///   Provides read/write access to INI files with thread-safe operations.
  /// </summary>
  public class IniService : IIniService, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<IniService>().ForFeature("Settings.Ini"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IniConfig config;
    private readonly CancellationTokenSource disposalCts;
    private readonly SemaphoreSlim fileAccessSemaphore;
    private readonly Dictionary<string, Dictionary<string, string>> sections;
    private readonly Dictionary<string, string> allValuesFlat;
    private readonly object loadLock = new();
    private bool loadStarted;
    private volatile bool loadCompleted;
    private Exception loadError;

    private volatile bool isDisposed;
    private string filePath;

    public IniService(IniConfig config)
    {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
      disposalCts = new CancellationTokenSource();
      fileAccessSemaphore = new SemaphoreSlim(1, 1);
      sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
      allValuesFlat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

      filePath = config.GetIniFilePath();
      EnsureDirectoryExists();

      log.ForMethod().Verbose("IniService created - File: {0}, Full Path: {1}", config.IniFileName, filePath);
    }

    #region IDisposable Members

    public void Dispose()
    {
      if (isDisposed)
        return;

      isDisposed = true;

      try
      {
        disposalCts?.Cancel();
        disposalCts?.Dispose();

        IsSaving?.Dispose();
        IsLoading?.Dispose();

        log.ForMethod().Debug("IniService disposed");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Error during disposal: {Message}", ex.Message);
      }
    }

    #endregion

    #region IIniService Members

    public ReactiveProperty<bool> IsSaving { get; } = new(false);
    public ReactiveProperty<bool> IsLoading { get; } = new(false);

    public IReadOnlyDictionary<string, string> AllValues => allValuesFlat;
    public UniTask Initialization => LoadAsync();

    public async UniTask LoadAsync(CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      // Single-flight: the first caller runs the physical load; concurrent callers poll the
      // completion flag. Deliberately NOT a shared Preserve()/UniTaskCompletionSource — both are
      // single-continuation and throw "await twice" when a second caller awaits the in-flight load,
      // which is exactly what silently faulted SettingsSystem.Initialization and killed all audio.
      bool runLoad;
      lock (loadLock)
      {
        runLoad = !loadStarted;
        loadStarted = true;
      }

      if (runLoad)
      {
        try { await LoadCoreAsync(); }
        catch (Exception ex) { loadError = ex; }
        finally { loadCompleted = true; }
      }
      else
      {
        await UniTask.WaitUntil(() => loadCompleted, cancellationToken: ct);
      }

      if (loadError != null)
        throw loadError;
    }

    private async UniTask LoadCoreAsync()
    {

      try
      {
        IsLoading.Value = true;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(disposalCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        await fileAccessSemaphore.WaitAsync(timeoutCts.Token);

        try
        {
          await LoadFromDiskAsync(timeoutCts.Token);

          log.ForMethod().Verbose("INI file loaded successfully - File: {0}, Sections: {1}, Total Keys: {2}",
            filePath, sections.Count, allValuesFlat.Count);
        }
        finally
        {
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
      catch (OperationCanceledException) when (isDisposed)
      {
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

    /// <summary>Performs the physical read. Virtual to permit deterministic I/O tests.</summary>
    protected virtual UniTask LoadFromDiskAsync(CancellationToken ct)
    {
      return UniTask.RunOnThreadPool(() =>
      {
        if (isDisposed)
          return;

        sections.Clear();
        allValuesFlat.Clear();

        if (!File.Exists(filePath))
        {
          log.ForMethod().Information("INI file does not exist at {0}, creating default", filePath);
          CreateDefaultIniFile();
        }

        ReadIniFile();
      }, cancellationToken: ct);
    }

    public async UniTask SaveAsync(CancellationToken ct = default)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      if (IsSaving.Value)
      {
        log.ForMethod().Warning("Save operation already in progress");
        return;
      }

      try
      {
        IsSaving.Value = true;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, disposalCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        await fileAccessSemaphore.WaitAsync(timeoutCts.Token);

        try
        {
          await UniTask.RunOnThreadPool(() =>
          {
            if (isDisposed)
              return;

            WriteIniFile();
          }, cancellationToken: timeoutCts.Token);

          log.ForMethod().Verbose("INI file saved successfully - File: {0}", filePath);
        }
        finally
        {
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

    public string GetValue(string section, string key, string defaultValue = null)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      string fullKey = $"{section}.{key}";
      if (allValuesFlat.TryGetValue(fullKey, out string value))
        return value;

      return defaultValue;
    }

    public T GetValue<T>(string section, string key, T defaultValue = default)
    {
      string stringValue = GetValue(section, key);
      if (stringValue == null)
        return defaultValue;

      try
      {
        return ConvertValue<T>(stringValue);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to convert value '{0}' for section '{1}', key '{2}' to type {3}, using default",
          stringValue, section, key, typeof(T).Name);
        return defaultValue;
      }
    }

    public void SetValue(string section, string key, object value)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      string stringValue = value?.ToString() ?? string.Empty;
      string fullKey = $"{section}.{key}";

      // Update sections dictionary
      if (!sections.TryGetValue(section, out Dictionary<string, string> sectionDict))
      {
        sectionDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        sections[section] = sectionDict;
      }

      sectionDict[key] = stringValue;
      allValuesFlat[fullKey] = stringValue;

      log.ForMethod().Verbose("Set value for section '{0}', key '{1}': {2}", section, key, stringValue);
    }

    public bool KeyExists(string section, string key)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      string fullKey = $"{section}.{key}";
      return allValuesFlat.ContainsKey(fullKey);
    }

    public void DeleteKey(string section, string key)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      string fullKey = $"{section}.{key}";

      if (sections.TryGetValue(section, out Dictionary<string, string> sectionDict))
      {
        sectionDict.Remove(key);
        if (sectionDict.Count == 0)
          sections.Remove(section);
      }

      allValuesFlat.Remove(fullKey);

      log.ForMethod().Verbose("Deleted key '{0}' from section '{1}'", key, section);
    }

    public IEnumerable<string> GetKeys(string section)
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      if (sections.TryGetValue(section, out Dictionary<string, string> sectionDict))
        return sectionDict.Keys;

      return Enumerable.Empty<string>();
    }

    public IEnumerable<string> GetSections()
    {
      if (isDisposed)
        throw new ObjectDisposedException(nameof(IniService));

      return sections.Keys;
    }

    #endregion

    #region Private Methods

    private void EnsureDirectoryExists()
    {
      try
      {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
          Directory.CreateDirectory(directory);
          log.ForMethod().Debug("Created directory for INI file: {0}", directory);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to ensure directory exists: {Message}", ex.Message);
      }
    }

    private void CreateDefaultIniFile()
    {
      try
      {
        // Create an empty INI file - defaults will be populated by SettingsSystem
        // when it initializes and calls PopulateDefaultsFromSettingsSystem
        File.WriteAllText(filePath, "; Configuration file\n; Default values will be populated from system defaults\n", Encoding.UTF8);
        log.ForMethod().Information("Created empty INI file at {0} - defaults will be populated from SettingsSystem", filePath);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to create default INI file: {Message}", ex.Message);
        throw;
      }
    }

    /// <summary>
    ///   Populates the INI file with default values from the SettingsSystem.
    ///   This should be called after SettingsSystem is initialized.
    ///   Only populates missing keys - existing values are preserved.
    /// </summary>
    public void PopulateDefaultsFromSettingsSystem(ISettingsSystem settingsSystem)
    {
      if (settingsSystem == null)
      {
        log.ForMethod().Warning("SettingsSystem is null, cannot populate defaults");
        return;
      }

      try
      {
        bool anyValuesAdded = false;

        // Graphics settings - only set if key doesn't exist
        if (settingsSystem.GraphicsSettings != null)
        {
          if (!KeyExists("Graphics", "Resolution"))
          {
            SetValue("Graphics", "Resolution", settingsSystem.GraphicsSettings.ResolutionIndex.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Graphics", "Quality"))
          {
            SetValue("Graphics", "Quality", settingsSystem.GraphicsSettings.QualityIndex.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Graphics", "Fullscreen"))
          {
            SetValue("Graphics", "Fullscreen", settingsSystem.GraphicsSettings.Fullscreen.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Graphics", "VerticalSync"))
          {
            SetValue("Graphics", "VerticalSync", settingsSystem.GraphicsSettings.VerticalSync.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Graphics", "DisableCrt"))
          {
            SetValue("Graphics", "DisableCrt", settingsSystem.GraphicsSettings.DisableCrt.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Graphics", "DisableBloom"))
          {
            SetValue("Graphics", "DisableBloom", settingsSystem.GraphicsSettings.DisableBloom.Default);
            anyValuesAdded = true;
          }
        }

        // Audio settings - only set if key doesn't exist
        if (settingsSystem.AudioSettings != null)
        {
          if (!KeyExists("Audio", "MasterVolume"))
          {
            SetValue("Audio", "MasterVolume", settingsSystem.AudioSettings.MasterVolume.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Audio", "MusicVolume"))
          {
            SetValue("Audio", "MusicVolume", settingsSystem.AudioSettings.MusicVolume.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Audio", "GameVolume"))
          {
            SetValue("Audio", "GameVolume", settingsSystem.AudioSettings.GameVolume.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Audio", "InterfaceVolume"))
          {
            SetValue("Audio", "InterfaceVolume", settingsSystem.AudioSettings.InterfaceVolume.Default);
            anyValuesAdded = true;
          }
        }

        // Game settings - only set if key doesn't exist
        if (settingsSystem.GameSettings != null)
        {
          if (!KeyExists("Game", "AutoSave"))
          {
            SetValue("Game", "AutoSave", settingsSystem.GameSettings.AutoSave.Default);
            anyValuesAdded = true;
          }
          if (!KeyExists("Game", "AnalyticsEnabled"))
          {
            SetValue("Game", "AnalyticsEnabled", settingsSystem.GameSettings.AnalyticsEnabled.Default);
            anyValuesAdded = true;
          }
        }

        // Input settings - if there are defaults, add them here
        // (InputSettingsModule doesn't seem to have ReactiveSettings in the same way)

        if (anyValuesAdded)
        {
          log.ForMethod().Information("Populated INI file with default values from SettingsSystem - {0} total keys now", allValuesFlat.Count);
        }
        else
        {
          log.ForMethod().Verbose("INI file already contains all default values, no population needed");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to populate defaults from SettingsSystem: {Message}", ex.Message);
      }
    }

    private void ReadIniFile()
    {
      try
      {
        string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
        string currentSection = string.Empty;

        foreach (string line in lines)
        {
          string trimmedLine = line.Trim();

          // Skip empty lines and comments
          if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
            continue;

          // Check for section header [SectionName]
          Match sectionMatch = Regex.Match(trimmedLine, @"^\[([^\]]+)\]$");
          if (sectionMatch.Success)
          {
            currentSection = sectionMatch.Groups[1].Value.Trim();
            if (!sections.ContainsKey(currentSection))
              sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            continue;
          }

          // Check for key=value pair
          int equalsIndex = trimmedLine.IndexOf('=');
          if (equalsIndex > 0)
          {
            string key = trimmedLine.Substring(0, equalsIndex).Trim();
            string value = trimmedLine.Substring(equalsIndex + 1).Trim();

            // Remove quotes if present
            if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
              value = value.Substring(1, value.Length - 2);

            if (!string.IsNullOrEmpty(currentSection))
            {
              sections[currentSection][key] = value;
              allValuesFlat[$"{currentSection}.{key}"] = value;
            }
          }
        }

        log.ForMethod().Verbose("Read INI file: {0} sections, {1} total keys", sections.Count, allValuesFlat.Count);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to read INI file: {Message}", ex.Message);
        throw;
      }
    }

    private void WriteIniFile()
    {
      try
      {
        using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
        {
          bool firstSection = true;

          foreach (KeyValuePair<string, Dictionary<string, string>> section in sections.OrderBy(s => s.Key))
          {
            if (!firstSection)
              writer.WriteLine(); // Empty line between sections

            writer.WriteLine($"[{section.Key}]");

            foreach (KeyValuePair<string, string> kvp in section.Value.OrderBy(k => k.Key))
              writer.WriteLine($"{kvp.Key}={kvp.Value}");

            firstSection = false;
          }
        }

        log.ForMethod().Verbose("Wrote INI file: {0} sections, {1} total keys", sections.Count, allValuesFlat.Count);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to write INI file: {Message}", ex.Message);
        throw;
      }
    }

    private T ConvertValue<T>(string value)
    {
      if (string.IsNullOrEmpty(value))
        return default(T);

      Type targetType = typeof(T);
      Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

      // Handle common types
      if (underlyingType == typeof(bool))
        return (T)(object)bool.Parse(value);

      if (underlyingType == typeof(int))
        return (T)(object)int.Parse(value, CultureInfo.InvariantCulture);

      if (underlyingType == typeof(float))
        return (T)(object)float.Parse(value, CultureInfo.InvariantCulture);

      if (underlyingType == typeof(double))
        return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);

      if (underlyingType == typeof(string))
        return (T)(object)value;

      // Use Convert.ChangeType for other types
      return (T)Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }

    #endregion
  }
}

