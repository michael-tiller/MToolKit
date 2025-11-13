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

namespace MToolKit.Runtime.Settings.Audio
{
  /// <summary>
  ///   Initializes the audio settings UI by binding the master volume slider.
  /// </summary>
  public class AudioSettingsInitializer : AbstractSettingsInitializer
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioSettingsInitializer>().ForFeature("Settings.Audio"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private FloatBoundElementSlider masterVolumeSlider;

    [SerializeField]
    [Required]
    private FloatBoundElementSlider musicVolumeSlider;

    [SerializeField]
    [Required]
    private FloatBoundElementSlider gameVolumeSlider;

    [SerializeField]
    [Required]
    private FloatBoundElementSlider interfaceVolumeSlider;

    [Inject]
    private ISettingsSystem settingsController;

    public override async UniTask ConfigureAsync()
    {
      // Check if AudioSettings are available
      if (settingsController?.AudioSettings == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("AudioSettings are not available");
        return;
      }

      masterVolumeSlider.Bind(settingsController.AudioSettings.MasterVolume);
      musicVolumeSlider.Bind(settingsController.AudioSettings.MusicVolume);
      gameVolumeSlider.Bind(settingsController.AudioSettings.GameVolume);
      interfaceVolumeSlider.Bind(settingsController.AudioSettings.InterfaceVolume);
      await UniTask.CompletedTask;
    }
  }
}