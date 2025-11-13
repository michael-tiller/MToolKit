using System;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Settings.UI;
using MToolKit.Runtime.Settings.UI.Abstract;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.Game
{
  public class GameSettingsInitializer : AbstractSettingsInitializer
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameSettingsInitializer>().ForFeature("Settings.Game"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private BoolBoundToggle autoSaveToggle;

    [SerializeField]
    [Required]
    private BoolBoundToggle analyticsEnabledToggle;

    [Inject]
    private ISettingsSystem settingsController;

    public override UniTask ConfigureAsync()
    {
      if (settingsController?.GameSettings == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("GameSettings are not available");
        return UniTask.CompletedTask;
      }

      autoSaveToggle.Bind(settingsController.GameSettings.AutoSave);

      autoSaveToggle.Value = settingsController.GameSettings.AutoSave.Value;

      analyticsEnabledToggle.Bind(settingsController.GameSettings.AnalyticsEnabled);

      analyticsEnabledToggle.Value = settingsController.GameSettings.AnalyticsEnabled.Value;

      return UniTask.CompletedTask;
    }
  }
}