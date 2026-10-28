using System;
using System.Collections.Generic;
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
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings
{
  /// <summary>
  ///   Central controller for settings management with reactive state.
  /// </summary>
  [Serializable]
  public class SettingsSystem : ISettingsSystem, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SettingsSystem>().ForFeature("Settings"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IIniService iniService;
    private bool isDisposed;
    private Dictionary<(string section, string key), object> pendingIniSnapshot;
    private bool writingIni;

    public SettingsSystem(InputRebinderService inputRebinderService, IIniService iniService = null,
      InputActionAsset inputActionAsset = null)
    {
      this.iniService = iniService;
      InitializeModules(inputRebinderService, inputActionAsset);
      if (iniService != null)
        RunInitializationAsync().Forget();
      else
        initializationCompleted = true;
    }

    private bool initializationCompleted;
    private Exception initializationFault;

    private async UniTaskVoid RunInitializationAsync()
    {
      try
      {
        await InitializeIniIntegrationAsync();
      }
      catch (Exception ex)
      {
        initializationFault = ex;
        log.ForMethod().Error("Settings initialization faulted: {0}", ex.Message);
      }
      finally
      {
        initializationCompleted = true;
      }
    }

    // Each caller gets its own WaitUntil poll: UniTask continuations are single-flight, so a shared
    // Initialization task (even Preserve()'d) throws "await twice" when two consumers await it while
    // still pending. Same pattern as IniService.LoadAsync.
    private async UniTask WaitForInitializationAsync()
    {
      if (!initializationCompleted)
        await UniTask.WaitUntil(() => initializationCompleted);
      if (initializationFault != null)
        throw new InvalidOperationException("Settings initialization faulted", initializationFault);
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
    public UniTask Initialization => WaitForInitializationAsync();

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
        QueueIniSave();
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
          QueueIniSave();
        }
        else
        {
          log.ForMethod().Warning("INI service is null, cannot save settings to INI file");
        }
      }
      
      SetDirty(false);

      //if (gotoMenu) GoToMainMenu();
    }

    public void DefaultSettings()
    {
      log.ForMethod().Information("Reverting to default settings");
      AudioSettings?.RevertToDefaultSettings();
      GraphicsSettings?.RevertToDefaultSettings();
      GameSettings?.RevertToDefaultSettings();
      InputSettings?.RevertToDefaultSettings();
      // A confirmed reset is already applied; persist it even though each
      // setting has advanced its cancellation baseline to the default value.
      Apply(autoFinish: false, gotoMenu: false);
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
      //GoToMainMenu();
    }

    public void SetDirty(bool isDirty)
    {
      IsDirty.Value = isDirty;
    }

    private void InitializeModules(InputRebinderService inputRebinderService, InputActionAsset inputActionAsset)
    {
      log.ForMethod().Verbose("Initializing settings modules");
      AudioSettings = new AudioSettingsModule(this);
      GraphicsSettings = new GraphicsSettingsModule(this);
      GameSettings = new GameSettingsModule(this);
      InputSettings = new InputSettingsModule(inputRebinderService, this, inputActionAsset);
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

    private async UniTask InitializeIniIntegrationAsync()
    {
      try
      {
        // Populate defaults if INI file is empty or missing keys
        if (iniService != null)
        {
          await iniService.LoadAsync();
          if (isDisposed) return;
          iniService.PopulateDefaultsFromSettingsSystem(this);
          
          // Load values from INI to override defaults
          LoadFromIni();

          log.ForMethod().Verbose("INI integration initialized");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize INI integration: {Message}", ex.Message);
        throw;
      }
    }

    private void LoadFromIni()
    {
      if (iniService == null)
        return;

      // Binding failures must fault Initialization and preserve the persisted source.
      // The module rolls back the asset if Unity cleared overrides before a parse error.
      if (InputSettings != null && iniService.KeyExists("Input", "BindingOverridesJson"))
        InputSettings.LoadBindingOverridesJson(iniService.GetValue<string>("Input", "BindingOverridesJson", string.Empty));

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
          if (iniService.KeyExists("Graphics", "DisableCrt"))
            GraphicsSettings.DisableCrt.Value = iniService.GetValue<bool>("Graphics", "DisableCrt", GraphicsSettings.DisableCrt.Default);
          if (iniService.KeyExists("Graphics", "DisableBloom"))
            GraphicsSettings.DisableBloom.Value = iniService.GetValue<bool>("Graphics", "DisableBloom", GraphicsSettings.DisableBloom.Default);
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

        // Input camera settings
        if (InputSettings != null)
        {
          if (iniService.KeyExists("Input", "CameraPanSensitivity"))
            InputSettings.CameraPanSensitivity.Value = iniService.GetValue<float>("Input", "CameraPanSensitivity", InputSettings.CameraPanSensitivity.Default);
          if (iniService.KeyExists("Input", "CameraZoomSensitivity"))
            InputSettings.CameraZoomSensitivity.Value = iniService.GetValue<float>("Input", "CameraZoomSensitivity", InputSettings.CameraZoomSensitivity.Default);
        }

        // ponytail: headless builds must not blast audio
        if (Application.isBatchMode && AudioSettings != null)
          AudioSettings.MasterVolume.Value = 0f;

        // Mark as applied so they're not dirty
        GraphicsSettings?.Apply();
        AudioSettings?.Apply();
        GameSettings?.Apply();
        InputSettings?.Apply();
        SetDirty(false);

        log.ForMethod().Verbose("Settings loaded from INI file");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load settings from INI: {Message}", ex.Message);
      }
    }

    private void QueueIniSave()
    {
      if (iniService == null || isDisposed)
        return;
      if (!initializationCompleted || initializationFault != null)
      {
        log.ForMethod().Warning("Settings persistence skipped because initialization is incomplete or faulted");
        return;
      }

      // Capture now: live controls may become drafts again while an earlier write awaits disk.
      var snapshot = new Dictionary<(string section, string key), object>();
      if (GraphicsSettings != null)
      {
        snapshot[("Graphics", "Resolution")] = GraphicsSettings.ResolutionIndex.LastValue;
        snapshot[("Graphics", "Quality")] = GraphicsSettings.QualityIndex.LastValue;
        snapshot[("Graphics", "Fullscreen")] = GraphicsSettings.Fullscreen.LastValue;
        snapshot[("Graphics", "VerticalSync")] = GraphicsSettings.VerticalSync.LastValue;
        snapshot[("Graphics", "DisableCrt")] = GraphicsSettings.DisableCrt.LastValue;
        snapshot[("Graphics", "DisableBloom")] = GraphicsSettings.DisableBloom.LastValue;
      }
      if (AudioSettings != null)
      {
        snapshot[("Audio", "MasterVolume")] = AudioSettings.MasterVolume.LastValue;
        snapshot[("Audio", "MusicVolume")] = AudioSettings.MusicVolume.LastValue;
        snapshot[("Audio", "GameVolume")] = AudioSettings.GameVolume.LastValue;
        snapshot[("Audio", "InterfaceVolume")] = AudioSettings.InterfaceVolume.LastValue;
      }
      if (GameSettings != null)
      {
        snapshot[("Game", "AutoSave")] = GameSettings.AutoSave.LastValue;
        snapshot[("Game", "AnalyticsEnabled")] = GameSettings.AnalyticsEnabled.LastValue;
      }
      if (InputSettings != null)
      {
        snapshot[("Input", "CameraPanSensitivity")] = InputSettings.CameraPanSensitivity.LastValue;
        snapshot[("Input", "CameraZoomSensitivity")] = InputSettings.CameraZoomSensitivity.LastValue;
        snapshot[("Input", "BindingOverridesJson")] = InputSettings.BindingOverridesJson.LastValue;
      }
      pendingIniSnapshot = snapshot;
      if (!writingIni) WritePendingIniSnapshotsAsync().Forget();
    }

    private async UniTaskVoid WritePendingIniSnapshotsAsync()
    {
      writingIni = true;
      try
      {
        while (!isDisposed && pendingIniSnapshot != null)
        {
          // IniService drops concurrent saves and reads mutable dictionaries on a worker.
          // Wait before SetValue as well as before SaveAsync.
          if (iniService.IsSaving.Value)
            await UniTask.WaitUntil(() => isDisposed || !iniService.IsSaving.Value);
          if (isDisposed) return;
          var snapshot = pendingIniSnapshot;
          pendingIniSnapshot = null;
          foreach (var entry in snapshot)
            iniService.SetValue(entry.Key.section, entry.Key.key, entry.Value);
          await iniService.SaveAsync();
          log.ForMethod().Verbose("Settings saved to INI file");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save settings to INI: {Message}", ex.Message);
      }
      finally { writingIni = false; }
    }

    public void Dispose()
    {
      if (isDisposed) return;
      isDisposed = true;
      pendingIniSnapshot = null;
      log.ForMethod().Information("Disposing SettingsSystem");
      ShutdownModules();
      IsDirty.Dispose();
    }
  }
}
