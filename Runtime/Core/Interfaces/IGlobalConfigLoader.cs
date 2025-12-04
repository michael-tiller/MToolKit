using MToolKit.Runtime.Core.Config;

namespace MToolKit.Runtime.Core.Interfaces
{
  /// <summary>
  ///   Interface for the global config loader
  /// </summary>
  public interface IGlobalConfigLoader
  {
    /// <summary>
    ///   Gets the global plugin config
    /// </summary>
    GlobalPluginConfigAsset GlobalPluginConfig { get; }
    /// <summary>
    ///   Gets the plugin config
    /// </summary>
    PluginConfigAsset PluginConfig { get; }
  }
}