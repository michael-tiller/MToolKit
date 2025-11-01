using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Namespace for environment loader.
/// </summary>
namespace MToolKit.Runtime.Core
{   
/// <summary>
/// Loader for environment variables.
/// </summary>
public static class EnvironmentLoader
{
    private static bool _loaded = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void LoadEnvironmentVariables()
    {
        if (_loaded) return;
        
        var envPath = Path.Combine(Application.dataPath, "..", ".env");
        
        if (!File.Exists(envPath))
        {
            Debug.LogWarning($"Environment file not found at: {envPath}");
            return;
        }

        var lines = File.ReadAllLines(envPath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                
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
