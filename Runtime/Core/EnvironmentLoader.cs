using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Namespace for environment loader.
/// </summary>

namespace MToolKit.Runtime.Core
{
    /// <summary>
    ///   Loader for environment variables.
    /// </summary>
    public static class EnvironmentLoader
  {
    private static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void LoadEnvironmentVariables()
    {
      if (_loaded) return;

      string envPath = Path.Combine(Application.dataPath, "..", ".env");

      if (!File.Exists(envPath))
      {
        Debug.LogWarning($"Environment file not found at: {envPath}");
        return;
      }

      string[] lines = File.ReadAllLines(envPath);
      foreach (string line in lines)
      {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
          continue;

        string[] parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
          string key = parts[0].Trim();
          string value = parts[1].Trim();

          // Only set if not already set (system env vars take precedence)
          if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
          {
            Environment.SetEnvironmentVariable(key, value);
            Debug.Log($"Loaded environment variable: {key}");
          }
        }
      }

      _loaded = true;
      Debug.Log("Environment variables loaded from .env file");
    }
  }
}