using System;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Settings.BoundSettings;
using Sirenix.OdinInspector;

namespace MToolKit.Runtime.Settings.Game
{
  public class GameSettingsModule : ISettingsModule, IGameSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameSettingsModule>().ForFeature("Settings.Game"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    public ReactiveSetting<bool> AutoSave { get; }
    public ReactiveSetting<bool> AnalyticsEnabled { get; }
    public bool AnalyticsEnabledValue { get => AnalyticsEnabled.Value; }
    [ShowInInspector, ReadOnly]
    public bool AutoSaveValue { get => AutoSave.Value; }
    public GameSettingsModule(ISettingsSystem settingsController = null)
    {
      AutoSave = new ReactiveSetting<bool>(true, "Auto Save", settingsController);
      AnalyticsEnabled = new ReactiveSetting<bool>(true, "Analytics Enabled", settingsController);
    }
    public void RevertToDefaultSettings()
    {
      if (!AutoSave.IsDefault)
      {
        AutoSave.OnRevertToDefault();
      }
      if (!AnalyticsEnabled.IsDefault)
      {
        AnalyticsEnabled.OnRevertToDefault();
      }
    }
    public void Apply()
    {
      if (AutoSave.IsDirty)
      {
        AutoSave.OnApply();
      }
      if (AnalyticsEnabled.IsDirty)
      {
        AnalyticsEnabled.OnApply();
      }
    }
    public void Cancel()
    {
      AutoSave.OnCancel();
      AnalyticsEnabled.OnCancel();
    }
    public void OnShutdown()
    {
      AutoSave.Dispose();
      AnalyticsEnabled.Dispose();
    }
  }
}