using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using Sirenix.OdinInspector;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.Audio
{
  [Serializable]
  public class AudioSettingsModule : ISettingsModule, IAudioSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioSettingsModule>().ForFeature("Settings.Audio"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    public ReactiveSetting<float> MasterVolume { get; }
    public ReactiveSetting<float> MusicVolume { get; }
    public ReactiveSetting<float> GameVolume { get; }
    public ReactiveSetting<float> InterfaceVolume { get; }

    [ShowInInspector, ReadOnly]
    public float MasterVolumeValue { get => MasterVolume.Value; }
    [ShowInInspector, ReadOnly]
    public float MusicVolumeValue { get => MusicVolume.Value; }
    [ShowInInspector, ReadOnly]
    public float GameVolumeValue { get => GameVolume.Value; }
    [ShowInInspector, ReadOnly]
    public float InterfaceVolumeValue { get => InterfaceVolume.Value; }
    public ReactiveSetting<float> GetReactiveSettingForAudioType(EAudioTypes audioType)
    {
      switch (audioType)
      {
        case EAudioTypes.Master: return MasterVolume;
        case EAudioTypes.Music: return MusicVolume;
        case EAudioTypes.Game: return GameVolume;
        case EAudioTypes.Interface: return InterfaceVolume;
        default:
          log.ForMethod().Error("Unknown Audio Type: {0}", audioType);
          break;
      }

      return null;
    }

    public AudioSettingsModule(ISettingsSystem settingsController = null)
    {
      MasterVolume = new ReactiveSetting<float>(1f, "Master Volume", settingsController);
      MusicVolume = new ReactiveSetting<float>(1f, "Music Volume", settingsController);
      GameVolume = new ReactiveSetting<float>(1f, "Game Volume", settingsController);
      InterfaceVolume = new ReactiveSetting<float>(1f, "Interface Volume", settingsController);
    }

    public void OnShutdown()
    {
      MasterVolume.Dispose();
      MusicVolume.Dispose();
      GameVolume.Dispose();
      InterfaceVolume.Dispose();
    }

    public void Apply()
    {
      if (MasterVolume.IsDirty) MasterVolume.OnApply();
      if (MusicVolume.IsDirty) MusicVolume.OnApply();
      if (GameVolume.IsDirty) GameVolume.OnApply();
      if (InterfaceVolume.IsDirty) InterfaceVolume.OnApply();
    }

    public void RevertToDefaultSettings()
    {
      if (!MasterVolume.IsDefault) MasterVolume.OnRevertToDefault();
      if (!MusicVolume.IsDefault) MusicVolume.OnRevertToDefault();
      if (!GameVolume.IsDefault) GameVolume.OnRevertToDefault();
      if (!InterfaceVolume.IsDefault) InterfaceVolume.OnRevertToDefault();
    }

    public void Cancel()
    {
      MasterVolume.OnCancel();
      MusicVolume.OnCancel();
      GameVolume.OnCancel();
      InterfaceVolume.OnCancel();
    }
  }
}