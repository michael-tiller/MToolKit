using MToolKit.Runtime.Core.Config;

namespace MToolKit.Runtime.Core.Interfaces
{
  /// <summary>
  ///   Interface for the global constants
  /// </summary>
  public interface IGlobalConstants
  {
    /// <summary>
    ///   Gets the global constants config
    /// </summary>
    GlobalConstantsConfigAsset GlobalConstantsConfig { get; }
    /// <summary>
    ///   Gets whether the global constants are initialized
    /// </summary>
    bool IsInitialized { get; }
  }
}