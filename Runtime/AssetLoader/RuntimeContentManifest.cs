using System;

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
    public string[] Catalogs = Array.Empty<string>();

    /// <summary>
    ///   Addressable labels to preload before gameplay starts.
    ///   All assets with these labels will be loaded and cached.
    /// </summary>
    public string[] Labels = Array.Empty<string>();

    /// <summary>
    ///   Addressable scene keys to load additively after content preload.
    ///   Scenes must be configured as Addressables (not in Build Settings).
    ///   Scenes are loaded in the order specified.
    /// </summary>
    public string[] Scenes = Array.Empty<string>();

    /// <summary>
    ///   Optional version identifier for cache invalidation.
    /// </summary>
    public string Version = string.Empty;
  }
}