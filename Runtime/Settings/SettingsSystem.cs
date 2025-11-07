using System;
using MessagePipe;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Input;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Navigation.Events;

namespace MToolKit.Runtime.Settings
{
  /// <summary>
  /// Central controller for settings management with reactive state.
  /// </summary>
  [Serializable]
  public class SettingsSystem : ISettingsSystem
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SettingsSystem>().ForFeature("Settings"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    // Injected dependencies - none needed since we use GlobalAsyncMessageBroker

    // Settings modules
    public GraphicsSettingsModule GraphicsSettings { get; private set; }
    public AudioSettingsModule AudioSettings { get; private set; }
    public GameSettingsModule GameSettings { get; private set; }
    public InputSettingsModule InputSettings { get; private set; }
    public ReactiveProperty<bool> IsDirty { get; } = new();


    [ShowInInspector, ReadOnly]
    private GraphicsSettingsModule graphicsSettings => GraphicsSettings;
    [ShowInInspector, ReadOnly]
    private AudioSettingsModule audioSettings => AudioSettings;
    [ShowInInspector, ReadOnly]
    private GameSettingsModule gameSettings => GameSettings;
    [ShowInInspector, ReadOnly]
    private InputSettingsModule inputSettings => InputSettings;
    [ShowInInspector, ReadOnly]
    private string hash => GetHashCode().ToString();

    [ShowInInspector, ReadOnly]
    private bool isDirty
    {
      get
      {
        if (GlobalInstaller.Instance != null && IsDirty != null)
        {
          return IsDirty.Value;
        }
        return false;
      }
    }

    public SettingsSystem(InputRebinderService inputRebinderService)
    {
      InitializeModules(inputRebinderService);
    }

    private void InitializeModules(InputRebinderService inputRebinderService)
    {
      log.ForMethod().Debug("Initializing settings modules");
      AudioSettings = new AudioSettingsModule(this);
      GraphicsSettings = new GraphicsSettingsModule(this);
      GameSettings = new GameSettingsModule(this);
      InputSettings = new InputSettingsModule(inputRebinderService, this);
    }

    private void ShutdownModules()
    {
      log.ForMethod().Debug("Shutting down settings modules");
      AudioSettings?.OnShutdown();
      GraphicsSettings?.OnShutdown();
      GameSettings?.OnShutdown();
      InputSettings?.OnShutdown();
    }

    public void Apply(bool autoFinish = true, bool gotoMenu = true)
    {
      log.ForMethod().Information("Applying settings");
      AudioSettings?.Apply();
      GraphicsSettings?.Apply();
      GameSettings?.Apply();
      InputSettings?.Apply();
      if (autoFinish) FinishApply(gotoMenu);
    }

    public void FinishApply(bool gotoMenu = true)
    {
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

    private void GoToMainMenu()
    {
      log.ForMethod().Debug("Going to main menu");

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
          var globalBackPublisher = GlobalInstaller.Instance.Container.Resolve<IPublisher<BackRequestMessage>>();
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
          var globalQuitPublisher = GlobalInstaller.Instance.Container.Resolve<IPublisher<QuitRequestMessage>>();
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

    public void Dispose()
    {
      log.ForMethod().Information("Disposing SettingsSystem");
      ShutdownModules();
      IsDirty.Dispose();
    }
  }
}