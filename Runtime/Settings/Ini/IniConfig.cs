using System;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS || SMOKE_HARNESS
    private static readonly object pathResolverLock = new();
    private static ScopedPathResolver scopedPathResolver;

    /// <summary>
    /// Installs one development/test path owner before normal INI service construction.
    /// The callback receives the unchanged configured path; invalid results fail closed.
    /// </summary>
    public static IDisposable InstallScopedPathResolver(Func<IniConfig, string, string> resolver)
    {
      if (resolver == null) throw new ArgumentNullException(nameof(resolver));
      lock (pathResolverLock)
      {
        if (scopedPathResolver != null)
          throw new InvalidOperationException("An INI path resolver is already owned.");
        scopedPathResolver = new ScopedPathResolver(resolver);
        return scopedPathResolver;
      }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScopedPathResolver()
    {
      lock (pathResolverLock) scopedPathResolver = null;
    }

    private sealed class ScopedPathResolver : IDisposable
    {
      internal readonly Func<IniConfig, string, string> Resolve;
      internal ScopedPathResolver(Func<IniConfig, string, string> resolver) => Resolve = resolver;

      public void Dispose()
      {
        lock (pathResolverLock)
          if (ReferenceEquals(scopedPathResolver, this)) scopedPathResolver = null;
      }
    }
#endif

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
      string normalPath = Path.Combine(basePath, iniDirectory, iniFileName);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_INCLUDE_TESTS || SMOKE_HARNESS
      ScopedPathResolver owner;
      lock (pathResolverLock) owner = scopedPathResolver;
      if (owner != null)
      {
        // Never catch resolver failures or remove ownership: a default-config factory
        // retry must use the same isolation policy instead of reaching shared settings.
        string resolved = owner.Resolve(this, normalPath);
        if (string.IsNullOrWhiteSpace(resolved) || !Path.IsPathFullyQualified(resolved))
          throw new InvalidOperationException("The scoped INI resolver must return an absolute file path.");
        try { return Path.GetFullPath(resolved); }
        catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException)
        {
          throw new InvalidOperationException("The scoped INI resolver returned an invalid absolute file path.", error);
        }
      }
#endif
      return normalPath;
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

