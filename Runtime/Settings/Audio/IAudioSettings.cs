using MToolKit.Runtime.Settings.BoundSettings;

namespace MToolKit.Runtime.Settings.Audio
{
  /// <summary>
  /// Module for managing audio Config.
  /// </summary>
  public interface IAudioSettings
  {
    /// <summary>
    /// The master volume setting (normalized 0–1).
    /// </summary>
    ReactiveSetting<float> MasterVolume { get; }
    ReactiveSetting<float> MusicVolume { get; }
    ReactiveSetting<float> GameVolume { get; }
    ReactiveSetting<float> InterfaceVolume { get; }
  }
}