using MToolKit.Runtime.Settings.BoundSettings;

namespace MToolKit.Runtime.Settings.Game
{
  public interface IGameSettings
  {
    ReactiveSetting<bool> AutoSave { get; }
    ReactiveSetting<bool> AnalyticsEnabled { get; }
  }
}