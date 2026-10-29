using System;
using System.Threading;
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
using MToolKit.Runtime.UI.Scaling;
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
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim settingsPersistenceGate = new(1, 1);
    private readonly InputAction recoverUiScaleAction;
    private bool isDisposed;

    public SettingsSystem(InputRebinderService inputRebinderService, IIniService iniService = null)
    {
      this.iniService = iniService;
      InitializeModules(inputRebinderService);
      recoverUiScaleAction = new InputAction("Recover UI Scale", InputActionType.Button);
      recoverUiScaleAction.AddCompositeBinding("ButtonWithTwoModifiers")
        .With("Modifier1", "<Keyboard>/ctrl")
        .With("Modifier2", "<Keyboard>/shift")
        .With("Button", "<Keyboard>/0");
      recoverUiScaleAction.performed += OnRecoverUiScale;
      if (Application.isPlaying) recoverUiScaleAction.Enable();
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

    private void OnRecoverUiScale(InputAction.CallbackContext context)
    {
      if (Application.isFocused && !isDisposed) RecoverUiScale().Forget();
    }

    /// <summary>
    /// Emergency recovery is independent of UI focus and layout. It commits only the scale
    /// preference; unrelated drafts retain their values and cancellation baselines.
    /// </summary>
    public async UniTask RecoverUiScale()
    {
      await Initialization;
      if (isDisposed) return;
      GraphicsSettings.UiScale.Value = 1f;
      GraphicsSettings.UiScale.OnApply();
      SetDirty(AudioSettings.MasterVolume.IsDirty || AudioSettings.MusicVolume.IsDirty ||
        AudioSettings.GameVolume.IsDirty || AudioSettings.InterfaceVolume.IsDirty ||
        GraphicsSettings.ResolutionIndex.IsDirty || GraphicsSettings.QualityIndex.IsDirty ||
        GraphicsSettings.Fullscreen.IsDirty || GraphicsSettings.VerticalSync.IsDirty ||
        GraphicsSettings.DisableCrt.IsDirty || GraphicsSettings.DisableBloom.IsDirty ||
        GameSettings.AutoSave.IsDirty || GameSettings.AnalyticsEnabled.IsDirty ||
        InputSettings.CameraPanSensitivity.IsDirty || InputSettings.CameraZoomSensitivity.IsDirty);
      if (iniService == null) return;
      // Serialize with Apply because IniService deliberately skips concurrent writes.
      // A later Apply can supersede recovery while this waits; its applied baseline
      // must persist without committing a still-newer live-preview draft.
      await settingsPersistenceGate.WaitAsync(lifetime.Token);
      try
      {
        await iniService.IsSaving.Where(saving => !saving).FirstAsync(lifetime.Token);
        if (isDisposed) return;
        iniService.SetValue("Graphics", "UiScale", UiScalePolicy.Serialize(GraphicsSettings.UiScale.LastValue));
        await iniService.SaveAsync(lifetime.Token);
      }
      finally { settingsPersistenceGate.Release(); }
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
          if (iniService.KeyExists("Graphics", "UiScale"))
            GraphicsSettings.UiScale.Value = iniService.GetValue<float>("Graphics", "UiScale", GraphicsSettings.UiScale.Default);
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

    private async UniTaskVoid SaveToIniAsync()
    {
      if (iniService == null)
      {
        log.ForMethod().Warning("INI service is null, cannot save settings");
        return;
      }

      bool acquired = false;
      try
      {
        log.ForMethod().Verbose("Saving settings to INI file");

        await settingsPersistenceGate.WaitAsync(lifetime.Token);
        acquired = true;
        await iniService.IsSaving.Where(saving => !saving).FirstAsync(lifetime.Token);
        if (isDisposed) return;

        // Apply advanced these baselines before requesting the write. A queued write
        // must not persist unrelated preview changes made while another writer ran.

        // Graphics settings
        if (GraphicsSettings != null)
        {
          iniService.SetValue("Graphics", "Resolution", GraphicsSettings.ResolutionIndex.LastValue);
          iniService.SetValue("Graphics", "Quality", GraphicsSettings.QualityIndex.LastValue);
          iniService.SetValue("Graphics", "Fullscreen", GraphicsSettings.Fullscreen.LastValue);
          iniService.SetValue("Graphics", "VerticalSync", GraphicsSettings.VerticalSync.LastValue);
          iniService.SetValue("Graphics", "DisableCrt", GraphicsSettings.DisableCrt.LastValue);
          iniService.SetValue("Graphics", "DisableBloom", GraphicsSettings.DisableBloom.LastValue);
          iniService.SetValue("Graphics", "UiScale", UiScalePolicy.Serialize(GraphicsSettings.UiScale.LastValue));
        }

        // Audio settings
        if (AudioSettings != null)
        {
          iniService.SetValue("Audio", "MasterVolume", AudioSettings.MasterVolume.LastValue);
          iniService.SetValue("Audio", "MusicVolume", AudioSettings.MusicVolume.LastValue);
          iniService.SetValue("Audio", "GameVolume", AudioSettings.GameVolume.LastValue);
          iniService.SetValue("Audio", "InterfaceVolume", AudioSettings.InterfaceVolume.LastValue);
        }

        // Game settings
        if (GameSettings != null)
        {
          iniService.SetValue("Game", "AutoSave", GameSettings.AutoSave.LastValue);
          iniService.SetValue("Game", "AnalyticsEnabled", GameSettings.AnalyticsEnabled.LastValue);
        }

        // Input camera settings
        if (InputSettings != null)
        {
          iniService.SetValue("Input", "CameraPanSensitivity", InputSettings.CameraPanSensitivity.LastValue);
          iniService.SetValue("Input", "CameraZoomSensitivity", InputSettings.CameraZoomSensitivity.LastValue);
        }

        // Save to file
        await iniService.SaveAsync(lifetime.Token);
        log.ForMethod().Verbose("Settings saved to INI file");
      }
      catch (OperationCanceledException) when (isDisposed) { }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save settings to INI: {Message}", ex.Message);
      }
      finally { if (acquired) settingsPersistenceGate.Release(); }
    }

    public void Dispose()
    {
      if (isDisposed) return;
      isDisposed = true;
      recoverUiScaleAction.performed -= OnRecoverUiScale;
      recoverUiScaleAction.Disable();
      recoverUiScaleAction.Dispose();
      lifetime.Cancel();
      lifetime.Dispose();
      log.ForMethod().Information("Disposing SettingsSystem");
      ShutdownModules();
      IsDirty.Dispose();
    }
  }
}
