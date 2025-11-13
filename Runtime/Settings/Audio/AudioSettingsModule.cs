using System;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using Serilog.Core;
using Sirenix.OdinInspector;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.Audio
{
  [Serializable]
  public class AudioSettingsModule : ISettingsModule, IAudioSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioSettingsModule>().ForFeature("Settings.Audio"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public AudioSettingsModule(ISettingsSystem settingsController = null)
    {
      MasterVolume = new ReactiveSetting<float>(1f, "Master Volume", settingsController);
      MusicVolume = new ReactiveSetting<float>(1f, "Music Volume", settingsController);
      GameVolume = new ReactiveSetting<float>(1f, "Game Volume", settingsController);
      InterfaceVolume = new ReactiveSetting<float>(1f, "Interface Volume", settingsController);
    }

    [ShowInInspector]
    [ReadOnly]
    public float MasterVolumeValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && MasterVolume != null) return MasterVolume.Value;
        return 0;
      }
    }

    [ShowInInspector]
    [ReadOnly]
    public float MusicVolumeValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && MusicVolume != null) return MusicVolume.Value;
        return 0;
      }
    }

    [ShowInInspector]
    [ReadOnly]
    public float GameVolumeValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && GameVolume != null) return GameVolume.Value;
        return 0;
      }
    }

    [ShowInInspector]
    [ReadOnly]
    public float InterfaceVolumeValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && InterfaceVolume != null) return InterfaceVolume.Value;
        return 0;
      }
    }

    public ReactiveSetting<float> MasterVolume { get; }
    public ReactiveSetting<float> MusicVolume { get; }
    public ReactiveSetting<float> GameVolume { get; }
    public ReactiveSetting<float> InterfaceVolume { get; }

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

    public ReactiveSetting<float> GetReactiveSettingForAudioType(EAudioTypes audioType)
    {
      switch (audioType)
      {
        case EAudioTypes.Master:
          return MasterVolume;
        case EAudioTypes.Music:
          return MusicVolume;
        case EAudioTypes.Game:
          return GameVolume;
        case EAudioTypes.Interface:
          return InterfaceVolume;
        default:
          log.ForMethod().Error("Unknown Audio Type: {0}", audioType);
          break;
      }

      return null;
    }
  }
}