using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Events;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Ini;
using MToolKit.Runtime.Settings.Input;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using Sirenix.OdinInspector;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings
{
  /// <summary>
  ///   Central controller for settings management with reactive state.
  /// </summary>
  [Serializable]
  public class SettingsSystem : ISettingsSystem
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SettingsSystem>().ForFeature("Settings"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IIniService iniService;

    public SettingsSystem(InputRebinderService inputRebinderService, IIniService iniService = null)
    {
      this.iniService = iniService;
      InitializeModules(inputRebinderService);
      
      // Initialize INI integration asynchronously
      if (iniService != null)
      {
        InitializeIniIntegrationAsync().Forget();
      }
    }


    [ShowInInspector]
    [ReadOnly]
    private GraphicsSettingsModule graphicsSettings => GraphicsSettings;

    [ShowInInspector]
    [ReadOnly]
    private AudioSettingsModule audioSettings => AudioSettings;

    [ShowInInspector]
    [ReadOnly]
    private GameSettingsModule gameSettings => GameSettings;

    [ShowInInspector]
    [ReadOnly]
    private InputSettingsModule inputSettings => InputSettings;

    [ShowInInspector]
    [ReadOnly]
    private string hash => GetHashCode().ToString();

    [ShowInInspector]
    [ReadOnly]
    private bool isDirtyValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && IsDirty != null)
          return IsDirty.Value;
        return false;
      }
    }

    // Injected dependencies - none needed since we use GlobalAsyncMessageBroker

    // Settings modules
    public GraphicsSettingsModule GraphicsSettings { get; private set; }
    public AudioSettingsModule AudioSettings { get; private set; }
    public GameSettingsModule GameSettings { get; private set; }
    public InputSettingsModule InputSettings { get; private set; }
    public ReactiveProperty<bool> IsDirty { get; } = new();

    public void Apply(bool autoFinish = true, bool gotoMenu = true)
    {
      log.ForMethod().Information("Applying settings");
      AudioSettings?.Apply();
      GraphicsSettings?.Apply();
      GameSettings?.Apply();
      InputSettings?.Apply();
      
      // Save to INI file
      if (iniService != null)
      {
        SaveToIniAsync().Forget();
      }
      else
      {
        log.ForMethod().Warning("INI service is null, cannot save settings to INI file");
      }
      
      if (autoFinish) FinishApply(gotoMenu);
    }

    public void FinishApply(bool gotoMenu = true)
    {
      // If Apply() wasn't called explicitly, apply settings now
      // This handles the case where FinishApply() is called directly
      if (IsDirty.Value)
      {
        log.ForMethod().Information("Apply() was not called, applying settings now");
        AudioSettings?.Apply();
        GraphicsSettings?.Apply();
        GameSettings?.Apply();
        InputSettings?.Apply();
        
        // Save to INI file
        if (iniService != null)
        {
          SaveToIniAsync().Forget();
        }
        else
        {
          log.ForMethod().Warning("INI service is null, cannot save settings to INI file");
        }
      }
      
      SetDirty(false);

      if (gotoMenu) GoToMainMenu();
    }

    public void DefaultSettings()
    {
      log.ForMethod().Information("Reverting to default settings");
      AudioSettings?.RevertToDefaultSettings();
      GraphicsSettings?.RevertToDefaultSettings();
      GameSettings?.RevertToDefaultSettings();
      InputSettings?.RevertToDefaultSettings();
      SetDirty(false);
    }

    public void Cancel()
    {
      log.ForMethod().Information("Cancelling settings");
      AudioSettings?.Cancel();
      GraphicsSettings?.Cancel();
      GameSettings?.Cancel();
      InputSettings?.Cancel();
      SetDirty(false);
      GoToMainMenu();
    }

    public void SetDirty(bool isDirty)
    {
      IsDirty.Value = isDirty;
    }

    private void InitializeModules(InputRebinderService inputRebinderService)
    {
      log.ForMethod().Verbose("Initializing settings modules");
      AudioSettings = new AudioSettingsModule(this);
      GraphicsSettings = new GraphicsSettingsModule(this);
      GameSettings = new GameSettingsModule(this);
      InputSettings = new InputSettingsModule(inputRebinderService, this);
    }

    private void ShutdownModules()
    {
      log.ForMethod().Verbose("Shutting down settings modules");
      AudioSettings?.OnShutdown();
      GraphicsSettings?.OnShutdown();
      GameSettings?.OnShutdown();
      InputSettings?.OnShutdown();
    }

    private void GoToMainMenu()
    {
      log.ForMethod().Verbose("Going to main menu");

      // Use GlobalAsyncMessageBroker for BackRequestMessage
      if (GlobalAsyncMessageBroker.IsAvailable())
      {
        log.ForMethod().Verbose("Using GlobalAsyncMessageBroker");
        GlobalAsyncMessageBroker.Publish(new BackRequestMessage(ECanvasType.Main));
        return;
      }

      // Fallback: try to get services from GlobalInstaller if available
      if (GlobalInstaller.Instance != null)
      {
        try
        {
          IPublisher<BackRequestMessage> globalBackPublisher = GlobalInstaller.Instance.Container.Resolve<IPublisher<BackRequestMessage>>();
          if (globalBackPublisher != null)
          {
            log.ForMethod().Verbose("Using globalBackPublisher");
            globalBackPublisher.Publish(new BackRequestMessage(ECanvasType.Main));
            return;
          }
        }
        catch
        {
          // Ignore resolution errors
        }

        try
        {
          IPublisher<QuitRequestMessage> globalQuitPublisher = GlobalInstaller.Instance.Container.Resolve<IPublisher<QuitRequestMessage>>();
          if (globalQuitPublisher != null)
          {
            log.ForMethod().Verbose("Using globalQuitPublisher");
            globalQuitPublisher.Publish(new QuitRequestMessage());
            return;
          }
        }
        catch
        {
          // Ignore resolution errors
        }
      }

      log.ForMethod().Warning("No navigation method available - cannot return to main menu");
    }

    private async UniTaskVoid InitializeIniIntegrationAsync()
    {
      try
      {
        // Wait a frame to ensure INI service has loaded
        await UniTask.Yield();
        
        // Populate defaults if INI file is empty or missing keys
        if (iniService != null)
        {
          iniService.PopulateDefaultsFromSettingsSystem(this);
          
          // Load values from INI to override defaults
          LoadFromIni();

          log.ForMethod().Verbose("INI integration initialized");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize INI integration: {Message}", ex.Message);
      }
    }

    private void LoadFromIni()
    {
      if (iniService == null)
        return;

      try
      {
        log.ForMethod().Verbose("Loading settings from INI file");

        // Graphics settings
        if (GraphicsSettings != null)
        {
          if (iniService.KeyExists("Graphics", "Resolution"))
            GraphicsSettings.ResolutionIndex.Value = iniService.GetValue<int>("Graphics", "Resolution", GraphicsSettings.ResolutionIndex.Default);
          if (iniService.KeyExists("Graphics", "Quality"))
            GraphicsSettings.QualityIndex.Value = iniService.GetValue<int>("Graphics", "Quality", GraphicsSettings.QualityIndex.Default);
          if (iniService.KeyExists("Graphics", "Fullscreen"))
            GraphicsSettings.Fullscreen.Value = iniService.GetValue<bool>("Graphics", "Fullscreen", GraphicsSettings.Fullscreen.Default);
          if (iniService.KeyExists("Graphics", "VerticalSync"))
            GraphicsSettings.VerticalSync.Value = iniService.GetValue<bool>("Graphics", "VerticalSync", GraphicsSettings.VerticalSync.Default);
        }

        // Audio settings
        if (AudioSettings != null)
        {
          if (iniService.KeyExists("Audio", "MasterVolume"))
            AudioSettings.MasterVolume.Value = iniService.GetValue<float>("Audio", "MasterVolume", AudioSettings.MasterVolume.Default);
          if (iniService.KeyExists("Audio", "MusicVolume"))
            AudioSettings.MusicVolume.Value = iniService.GetValue<float>("Audio", "MusicVolume", AudioSettings.MusicVolume.Default);
          if (iniService.KeyExists("Audio", "GameVolume"))
            AudioSettings.GameVolume.Value = iniService.GetValue<float>("Audio", "GameVolume", AudioSettings.GameVolume.Default);
          if (iniService.KeyExists("Audio", "InterfaceVolume"))
            AudioSettings.InterfaceVolume.Value = iniService.GetValue<float>("Audio", "InterfaceVolume", AudioSettings.InterfaceVolume.Default);
        }

        // Game settings
        if (GameSettings != null)
        {
          if (iniService.KeyExists("Game", "AutoSave"))
            GameSettings.AutoSave.Value = iniService.GetValue<bool>("Game", "AutoSave", GameSettings.AutoSave.Default);
          if (iniService.KeyExists("Game", "AnalyticsEnabled"))
            GameSettings.AnalyticsEnabled.Value = iniService.GetValue<bool>("Game", "AnalyticsEnabled", GameSettings.AnalyticsEnabled.Default);
        }

        // Mark as applied so they're not dirty
        GraphicsSettings?.Apply();
        AudioSettings?.Apply();
        GameSettings?.Apply();
        SetDirty(false);

        log.ForMethod().Verbose("Settings loaded from INI file");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load settings from INI: {Message}", ex.Message);
      }
    }

    private async UniTaskVoid SaveToIniAsync()
    {
      if (iniService == null)
      {
        log.ForMethod().Warning("INI service is null, cannot save settings");
        return;
      }

      try
      {
        log.ForMethod().Verbose("Saving settings to INI file");

        // Graphics settings
        if (GraphicsSettings != null)
        {
          iniService.SetValue("Graphics", "Resolution", GraphicsSettings.ResolutionIndex.Value);
          iniService.SetValue("Graphics", "Quality", GraphicsSettings.QualityIndex.Value);
          iniService.SetValue("Graphics", "Fullscreen", GraphicsSettings.Fullscreen.Value);
          iniService.SetValue("Graphics", "VerticalSync", GraphicsSettings.VerticalSync.Value);
        }

        // Audio settings
        if (AudioSettings != null)
        {
          iniService.SetValue("Audio", "MasterVolume", AudioSettings.MasterVolume.Value);
          iniService.SetValue("Audio", "MusicVolume", AudioSettings.MusicVolume.Value);
          iniService.SetValue("Audio", "GameVolume", AudioSettings.GameVolume.Value);
          iniService.SetValue("Audio", "InterfaceVolume", AudioSettings.InterfaceVolume.Value);
        }

        // Game settings
        if (GameSettings != null)
        {
          iniService.SetValue("Game", "AutoSave", GameSettings.AutoSave.Value);
          iniService.SetValue("Game", "AnalyticsEnabled", GameSettings.AnalyticsEnabled.Value);
        }

        // Save to file
        await iniService.SaveAsync();
        log.ForMethod().Verbose("Settings saved to INI file");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save settings to INI: {Message}", ex.Message);
      }
    }

    public void Dispose()
    {
      log.ForMethod().Information("Disposing SettingsSystem");
      ShutdownModules();
      IsDirty.Dispose();
    }
  }
}