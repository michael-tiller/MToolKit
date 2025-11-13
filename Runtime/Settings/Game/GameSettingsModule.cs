using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using Serilog.Core;
using Sirenix.OdinInspector;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.Game
{
  public class GameSettingsModule : ISettingsModule, IGameSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameSettingsModule>().ForFeature("Settings.Game"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public GameSettingsModule(ISettingsSystem settingsController = null)
    {
      AutoSave = new ReactiveSetting<bool>(true, "Auto Save", settingsController);
      AnalyticsEnabled = new ReactiveSetting<bool>(true, "Analytics Enabled", settingsController);
    }

    public bool AnalyticsEnabledValue => AnalyticsEnabled.Value;

    [ShowInInspector]
    [ReadOnly]
    public bool AutoSaveValue => AutoSave.Value;

    #region IGameSettings Members

    public ReactiveSetting<bool> AutoSave { get; }
    public ReactiveSetting<bool> AnalyticsEnabled { get; }

    #endregion

    #region ISettingsModule Members

    public void RevertToDefaultSettings()
    {
      if (!AutoSave.IsDefault)
        AutoSave.OnRevertToDefault();
      if (!AnalyticsEnabled.IsDefault)
        AnalyticsEnabled.OnRevertToDefault();
    }

    public void Apply()
    {
      if (AutoSave.IsDirty)
        AutoSave.OnApply();
      if (AnalyticsEnabled.IsDirty)
        AnalyticsEnabled.OnApply();
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

    #endregion
  }
}