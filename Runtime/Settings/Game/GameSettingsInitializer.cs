using System;
using UnityEngine;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Settings.UI;
using Sirenix.OdinInspector;
using MToolKit.Runtime.Settings.UI.Abstract;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using VContainer;

namespace MToolKit.Runtime.Settings.Game
{
  public class GameSettingsInitializer : AbstractSettingsInitializer 
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameSettingsInitializer>().ForFeature("Settings.Game"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
        [SerializeField, Required] private BoolBoundToggle autoSaveToggle;
        [SerializeField, Required] private BoolBoundToggle analyticsEnabledToggle;
        [Inject] private ISettingsSystem settingsController;

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