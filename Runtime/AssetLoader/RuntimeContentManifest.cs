using System;
using UnityEngine;

namespace MToolKit.Runtime.AssetLoader
{
  /// <summary>
  ///   Data-driven manifest defining catalogs, labels, and scenes to preload at startup.
  ///   This eliminates hardcoded preload logic and enables content updates without code rebuilds.
  /// </summary>
  [Serializable]
  public class RuntimeContentManifest
  {
    /// <summary>
    ///   Remote catalog URLs to load (e.g., DLC catalogs, patched content).
    ///   Supports file://, http://, and https:// protocols.
    /// </summary>
    [SerializeField]
    private string[] catalogs = Array.Empty<string>();

    /// <summary>
    ///   Addressable labels to preload before gameplay starts.
    ///   All assets with these labels will be loaded and cached.
    /// </summary>
    [SerializeField]
    private string[] labels = Array.Empty<string>();

    /// <summary>
    ///   Addressable scene keys to load additively after content preload.
    ///   Scenes must be configured as Addressables (not in Build Settings).
    ///   Scenes are loaded in the order specified.
    /// </summary>
    [SerializeField]
    private string[] scenes = Array.Empty<string>();

    /// <summary>
    ///   Optional version identifier for cache invalidation.
    /// </summary>
    [SerializeField]
    private string version = string.Empty;

    // Public properties to access the serialized fields
    public string[] Catalogs => catalogs ?? Array.Empty<string>();
    public string[] Labels => labels ?? Array.Empty<string>();
    public string[] Scenes => scenes ?? Array.Empty<string>();
    public string Version => version ?? string.Empty;

    public RuntimeContentManifest()
    {
    }

    public RuntimeContentManifest(string[] catalogs, string[] labels, string[] scenes, string version)
    {
      this.catalogs = catalogs;
      this.labels = labels;
      this.scenes = scenes;
      this.version = version;
    }
  }
}