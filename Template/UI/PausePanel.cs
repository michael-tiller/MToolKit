using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Input.Interfaces;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Template.ExamplePlayer.Events;
using UnityEngine.UI;
using MToolKit.Runtime.Installer;

namespace MToolKit.Template.UI
{
  public class PausePanel : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<PausePanel>().ForFeature("MToolKit.Template.UI"));
   private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    [SerializeField][Required] private CanvasGroup pauseCanvasGroup;
    [SerializeField][Required] private GameObject quitConfirmation;

    [SerializeField][Required] private Button resumeButton;
    [SerializeField][Required] private Button quitConfirmationButton;

    public readonly ReactiveProperty<bool> IsPaused = new(false);

    private IDisposable sub;
    private UIRoot uiRoot;
    private SaveSystemCoordinator saveSystemCoordinator;
    private IInputService inputService;
    private readonly SemaphoreSlim saveSemaphore = new(1, 1);

    [Inject]
    public void InjectDependencies(UIRoot uiRoot, SaveSystemCoordinator saveSystemCoordinator, IInputService inputService)
    {
      this.uiRoot = uiRoot;
      this.saveSystemCoordinator = saveSystemCoordinator;
      this.inputService = inputService;
      log.ForGameObject(gameObject).ForMethod(nameof(InjectDependencies)).Debug("InjectDependencies called. uiRoot: {0}, saveSystemCoordinator: {1}, inputService: {2}", uiRoot != null, saveSystemCoordinator != null, inputService != null);
    }

    public void ShowQuitConfirmation(bool show)
    {
      quitConfirmation.SetActive(show);
      if (show)
      {
        EventSystem.current.SetSelectedGameObject(quitConfirmationButton.gameObject);
      }
      else
      {
        EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
      }
    }

    private void Start()
    {
      // Initialize cursor state for gameplay (locked and hidden)
      Cursor.visible = false;
      Cursor.lockState = CursorLockMode.Locked;

      quitConfirmation.SetActive(false);
      
      sub = IsPaused.Subscribe(IsPausedHandler);
      pauseCanvasGroup.alpha = 0;
      pauseCanvasGroup.blocksRaycasts = false;
      pauseCanvasGroup.interactable = false;
      
      // Get the active InputService instance from GlobalInstaller to ensure we use the initialized instance
      // This prevents issues where child scopes create separate uninitialized instances
      var activeInputService = GlobalInstaller.Instance?.InputServiceInstance ?? inputService;
      
      if (activeInputService != null)
      {
        // Unsubscribe from any previously injected instance (if different)
        if (inputService != null && inputService != activeInputService)
        {
          inputService.OnPausePressed -= OnPausePressed;
          log.ForGameObject(gameObject).ForMethod(nameof(Start)).Warning(
            "PausePanel: Using GlobalInstaller InputService instance instead of injected instance. " +
            "Injected instance may not be initialized. Using instance from: {0}", 
            GlobalInstaller.Instance != null ? "GlobalInstaller" : "injection");
        }
        
        // Subscribe to the active initialized instance
        inputService = activeInputService;
        inputService.OnPausePressed += OnPausePressed;
        log.ForGameObject(gameObject).ForMethod(nameof(Start)).Information(
          "PausePanel: Successfully subscribed to input service pause events. Type: {0}, Instance source: {1}", 
          inputService.GetType().Name,
          GlobalInstaller.Instance?.InputServiceInstance == inputService ? "GlobalInstaller" : "injection");
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod(nameof(Start)).Warning("InputService is null, cannot subscribe to pause events");
      }

    }

    private void OnEnable()
  {
  }

      
    // Input handling is now done through the InputService event subscription
    // Removed Update() method to avoid legacy Input.GetKeyDown() which doesn't work reliably in builds

    private void OnDestroy()
    {
      sub?.Dispose();
      saveSemaphore?.Dispose();
      
      // Restore cursor to visible and unlocked state when destroying pause panel
      Cursor.visible = true;
      Cursor.lockState = CursorLockMode.None;
      
      // Unsubscribe from input service events
      if (inputService != null)
      {
        inputService.OnPausePressed -= OnPausePressed;
        log.ForGameObject(gameObject).ForMethod(nameof(OnDestroy)).Debug("Unsubscribed from input service pause events");
      }
    }

    public void SetPause(bool pause)
    {
      IsPaused.Value = pause;
      // Show cursor and unlock it if paused, hide and lock if unpaused
      Cursor.visible = pause;
      Cursor.lockState = pause ? CursorLockMode.None : CursorLockMode.Locked;

      // If unpausing and quit confirmation is active, hide it
      if (!pause && quitConfirmation.activeSelf)
        ShowQuitConfirmation(false);
    }

    private void OnPausePressed()
    {
      log.ForGameObject(gameObject).ForMethod(nameof(OnPausePressed)).Information("PausePanel: Pause input received! Current pause state: {0}, toggling...", IsPaused.Value);
      SetPause(!IsPaused.Value);
    }

    private void IsPausedHandler(bool isPaused)
    {
      pauseCanvasGroup.alpha = isPaused ? 1f : 0f;
      pauseCanvasGroup.blocksRaycasts = isPaused;
      pauseCanvasGroup.interactable = isPaused;
      
      // Clear selection when resuming to prevent stuck button state
      if (!isPaused && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == this.gameObject)
      {
        EventSystem.current?.SetSelectedGameObject(null);
      }
      if (isPaused)
        EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
      
      // Publish pause state change to the message pipe
      GameMessageBroker.Publish(new PauseToggledMessage(isPaused));
      log.ForGameObject(gameObject).ForMethod(nameof(IsPausedHandler)).Debug("Published PauseToggledMessage with isPaused: {0}", isPaused);
    }

    public void OnQuitConfirmed()
    {
      // Save before quitting
      OnQuitConfirmedAsync().Forget();
    }

    private async UniTask OnQuitConfirmedAsync()
    {
      await AutoSaveAsync();
      
      var menuSceneRef = GlobalConstants.Instance?.GlobalConstantsConfig?.MenuSceneReference;
      if (menuSceneRef != null && menuSceneRef.RuntimeKeyIsValid())
      {
        log.ForGameObject(gameObject).ForMethod(nameof(OnQuitConfirmedAsync)).Information("Loading menu scene from GlobalConstantsConfig: {SceneGuid}", menuSceneRef.AssetGUID);
        
        var handle = menuSceneRef.LoadSceneAsync(LoadSceneMode.Single);
        await handle.ToUniTask();
        
        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
          log.ForGameObject(gameObject).ForMethod(nameof(OnQuitConfirmedAsync)).Debug("Successfully loaded menu scene from AssetReference");
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod(nameof(OnQuitConfirmedAsync)).Error("Scene load failed from AssetReference: {Guid}, Status: {Status}", menuSceneRef.AssetGUID, handle.Status);
        }
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod(nameof(OnQuitConfirmedAsync)).Error("Invalid or missing MenuSceneReference in GlobalConstantsConfig");
      }
    }

    private async UniTask AutoSaveAsync()
    {
      if (saveSystemCoordinator == null)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(AutoSaveAsync)).Warning("SaveSystemCoordinator not available for auto-save");
        return;
      }

      // Try to acquire the semaphore, but don't wait if already saving
      if (!await saveSemaphore.WaitAsync(0))
      {
        log.ForGameObject(gameObject).ForMethod(nameof(AutoSaveAsync)).Information("Save operation already in progress, skipping duplicate save");
        return;
      }

      try
      {
        log.ForGameObject(gameObject).ForMethod(nameof(AutoSaveAsync)).Information("Auto-saving game via SaveSystemCoordinator...");
        // Use HandleSceneChangeAsync to ensure proper profile-aware saving
        await saveSystemCoordinator.HandleSceneChangeAsync();
        log.ForGameObject(gameObject).ForMethod(nameof(AutoSaveAsync)).Information("Auto-save completed successfully");
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(AutoSaveAsync)).Error(ex, "Auto-save failed: {Message}", ex.Message);
      }
      finally
      {
        saveSemaphore.Release();
      }
    }
  }
}