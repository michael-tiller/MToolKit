// Navigation/Controllers/NavigationController.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.Config;
using MToolKit.Runtime.Navigation.DataStructures;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Events;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Views;
using MToolKit.Runtime.Settings.Enums;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Navigation
{
  /// <summary>
  ///   Controls the navigation flow within the application, managing different canvas types and handling quit requests.
  ///   Uses property injection to resolve dependencies from the DI container.
  /// </summary>
  public class NavigationSystem : MonoBehaviour, IDisposable, IModalService
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<NavigationSystem>().ForFeature("Navigation"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    /// <summary>
    ///   Navigation service handling view stack operations.
    /// </summary>
    [Inject]
    private INavigationService navigationService;


    [TabGroup("Config")]
    [SerializeField]
    [Required]
    private CanvasGroup noScreenCanvasGroup;

    [TabGroup("Config")]
    [SerializeField]
    [Required]
    private InterstitialAlertView interstitialAlertView;

    [TabGroup("Debug")]
    [ShowInInspector]
    [ReadOnly]
    [Inject]
    private Dictionary<ECanvasType, Transform> canvasTransforms;


    /// <summary>
    ///   List of all canvas configurations.
    /// </summary>
    [TabGroup("Debug")]
    [ShowInInspector]
    [ReadOnly]
    [Inject]
    private CanvasConfigDict canvasConfigs;

    private CancellationTokenSource cts;
    private CompositeDisposable disposables;
    private bool isInitializing;
    private bool initialViewsShown;

    /// <summary>
    ///   Unity's Start method. Initializes canvas configurations and begins asynchronous initialization.
    /// </summary>
    private void Start()
    {
      if (isInitializing)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("NavigationController already initializing, skipping duplicate Start call.");
        return;
      }

      cts = new CancellationTokenSource();
      if (cts == null) log.ForGameObject(gameObject).ForMethod().Warning("No CTS has been set.");

      log.ForGameObject(gameObject).ForMethod().Verbose("Starting NavigationController");

      // Debug: Check if interstitialAlertView is properly assigned
      if (interstitialAlertView == null)
        log.ForGameObject(gameObject).ForMethod().Error("interstitialAlertView is null in Start()! Check NavigationPlugin prefab configuration.");
      else
        log.ForGameObject(gameObject).ForMethod().Debug("interstitialAlertView prefab assigned: {0}, Canvas: {1}",
          interstitialAlertView.name, interstitialAlertView.Canvas);

      InitializeCanvasConfigs();
      isInitializing = true;
      InitializeAsync(cts.Token).Forget();
    }

    /// <summary>
    ///   Validates the canvas configurations to ensure all necessary canvases are set.
    ///   Logs an error if any required main canvas is not configured.
    /// </summary>
    private void InitializeCanvasConfigs()
    {
      if (canvasTransforms == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("canvasTransforms is null - NavigationInstaller may not have been configured properly");
        return;
      }

      foreach (KeyValuePair<ECanvasType, Transform> config in canvasTransforms)
        if (config.Value == null)
          log.ForGameObject(gameObject).ForMethod().Error("Canvas Transform not set in canvasTransforms: {0}", config.Key);
    }

    /// <summary>
    ///   Asynchronously initializes the navigation controller by showing initial canvases and subscribing to messages.
    /// </summary>
    /// <param name="token">Cancellation token for asynchronous tasks.</param>
    private async UniTaskVoid InitializeAsync(CancellationToken token)
    {
      try
      {
        // Check if object is still valid before proceeding
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed during initialization, aborting.");
          return;
        }

        log.ForGameObject(gameObject).ForMethod().Verbose("InitializeAsync called");

        if (disposables != null)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Previous initialization detected. Cleaning up.");
          disposables.Dispose();
          disposables = null;
        }

        // Reset initialization flags
        initialViewsShown = false;

        // Check again before accessing components
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed during initialization, aborting.");
          return;
        }

        ToggleCanvasGroup(noScreenCanvasGroup, false);
        await ShowInitialCanvasElements(token);

        // Check again after async operation
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed after ShowInitialCanvasElements, aborting.");
          return;
        }

        log.ForGameObject(gameObject).ForMethod().Verbose("About to call SubscribeToMessages");
        await SubscribeToMessages(token);

        // Final check before completion
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed after SubscribeToMessages, aborting.");
          return;
        }

        log.ForGameObject(gameObject).ForMethod().Verbose("NavigationController initialization completed successfully.");
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Warning("Initialization was canceled.");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error("Initialization failed: {0}", ex.Message);
      }
      finally
      {
        isInitializing = false;
      }
    }

    private void ToggleCanvasGroup(CanvasGroup group, bool isOn)
    {
      if (group == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("ToggleCanvasGroup not set in CanvasGroup: {0}", group);
        return;
      }

      group.alpha = isOn ? 1f : 0f;
      SetCanvasGroupInteractable(group, isOn);
      group.blocksRaycasts = isOn;
    }

    public void SetAllCanvasGroupInteractable(bool isOn)
    {
      if (canvasTransforms == null) return;
      canvasTransforms.ForEach(kvp => SetCanvasGroupInteractable(kvp.Value.GetComponent<CanvasGroup>(), isOn));
    }

    public void SetCanvasGroupInteractable(CanvasGroup group, bool isOn)
    {
      group.interactable = isOn;
    }


    /// <summary>
    ///   Displays the initial elements for all specified canvas types.
    /// </summary>
    /// <param name="token">Cancellation token for asynchronous tasks.</param>
    private async UniTask ShowInitialCanvasElements(CancellationToken token)
    {
      try
      {
        // Check if object is still valid before proceeding
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed during ShowInitialCanvasElements, aborting.");
          return;
        }

        if (initialViewsShown)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Initial views already shown, skipping.");
          return;
        }

        foreach (ECanvasType canvasType in Enum.GetValues(typeof(ECanvasType)))
        {
          // Check again before each iteration
          if (this == null || gameObject == null)
          {
            log.ForMethod().Warning("NavigationSystem or GameObject destroyed during ShowInitialCanvasElements iteration, aborting.");
            return;
          }

          if (canvasType != ECanvasType.None)
            await ShowInitialView(canvasType, token);
        }

        // Check again before setting flag and calling ValidateViewsAsync
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed before completing ShowInitialCanvasElements, aborting.");
          return;
        }

        initialViewsShown = true;
        await ValidateViewsAsync();
      }
      catch (Exception ex)
      {
        log.ForMethod().Error("ShowInitialCanvasElements failed: {0}", ex.Message);
      }
    }

    private async UniTask ValidateViewsAsync()
    {
      try
      {
        await UniTask.Yield();

        // Check if object is still valid before proceeding
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed during ValidateViewsAsync, aborting.");
          return;
        }

        // Only check for empty canvases if initial views have been shown
        if (!initialViewsShown)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("Initial views not yet shown, skipping validation.");
          return;
        }

        // Check if any canvas is empty and show initial views
        foreach (ECanvasType canvasType in Enum.GetValues(typeof(ECanvasType)))
        {
          // Check again before each iteration
          if (this == null || gameObject == null)
          {
            log.ForMethod().Warning("NavigationSystem or GameObject destroyed during ValidateViewsAsync iteration, aborting.");
            return;
          }

          if (canvasType == ECanvasType.None) continue;

          if (!navigationService.TryPeek(canvasType, out _))
          {
            // Canvas is empty, show initial view
            if (canvasType == ECanvasType.Main) log.ForGameObject(gameObject).ForMethod().Warning("Canvas {0} is empty after initial setup, showing initial view", canvasType);
            await ShowInitialView(canvasType, CancellationToken.None);
          }
        }

        // Check again before final operations
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem or GameObject destroyed before completing ValidateViewsAsync, aborting.");
          return;
        }

        // Only show error screen if ALL canvases are empty after trying to show initial views
        if (AreAllViewsEmpty())
        {
          log.ForGameObject(gameObject).ForMethod().Warning("All views are empty. Displaying error screen");
          ToggleCanvasGroup(noScreenCanvasGroup, true);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error("ValidateViewsAsync failed: {0}", ex.Message);
      }
    }

    private bool AreAllViewsEmpty()
    {
      if (canvasTransforms == null) return true;

      foreach (KeyValuePair<ECanvasType, Transform> config in canvasTransforms)
      {
        if (config.Value == null) continue;
        Transform[] allChildren = config.Value.GetComponentsInChildren<Transform>();
        if (allChildren.Length > 1)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("Not all views are empty");
          return false;
        }
      }

      log.ForGameObject(gameObject).ForMethod().Verbose("All views are empty");
      return true;
    }

    /// <summary>
    ///   Displays the initial view for a specific canvas type if configured.
    /// </summary>
    /// <param name="type">Canvas type to show.</param>
    /// <param name="token">Cancellation token for asynchronous tasks.</param>
    private async UniTask<View> ShowInitialView(ECanvasType type, CancellationToken token)
    {
      if (type == ECanvasType.None)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Canvas type not set in ShowInitialView: {0}", type);
        return null;
      }

      CanvasConfig canvasConfig = GetCanvasConfig(type);
      if (canvasConfig == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("CanvasConfig for type {0} not found!", type);
        return null;
      }

      if (canvasConfig.InitialViewPrefab == null)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("No initial view configured for canvas: {0}, skipping", type);
        return null;
      }

      View view = await ShowViewAsync(type, canvasConfig.InitialViewPrefab, token);
      return view;
    }

    private bool applicationIsQuitting;

    private void OnApplicationQuit()
    {
      // Safe logging - use try-catch to handle gameObject access during quit
      try
      {
        if (gameObject != null)
          log.ForGameObject(gameObject).ForMethod().Verbose("Quitting.");
        else
          log.ForMethod().Verbose("Quitting (GameObject already destroyed).");
      }
      catch (NullReferenceException)
      {
        // GameObject is destroyed, use safe logging
        log.ForMethod().Verbose("Quitting (GameObject destroyed during quit).");
      }
      applicationIsQuitting = true;
    }

    private async UniTask<View> ShowViewAsync(ECanvasType type, View prefab, CancellationToken token)
    {
      if (applicationIsQuitting)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("application is quitting");
        return null;
      }

      if (type == ECanvasType.None)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Canvas type not set in ShowViewAsync: {0}", type);
        return null;
      }

      if (prefab == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Prefab is null in ShowViewAsync for canvas type: {0}", type);
        return null;
      }

      try
      {
        log.ForGameObject(gameObject).ForMethod().Information("Pushing view: {0} (type: {1}) for canvas type: {2}",
          prefab.name, prefab.GetType().Name, type);

        View result = await PushAsync(type, prefab, token);

        log.ForGameObject(gameObject).ForMethod().Debug("PushAsync returned view: {0} (type: {1})",
          result?.name ?? "NULL", result?.GetType().Name ?? "NULL");

        return result;
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Failed to push view: {0} for canvas type: {1}: {2}", prefab.name, type, ex.Message);
        return null;
      }
    }

    public bool DoesCanvasContainPrefabType<T>(ECanvasType type) where T : View
    {
      CanvasConfig canvasConfig = GetCanvasConfig(type);
      if (canvasConfig == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Modal CanvasConfig not found.");
        return false;
      }

      View tPrefab = canvasConfig.GetViewPrefab<T>();
      if (tPrefab == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Failed to retrieve {0} prefab from CanvasConfig: {1}", typeof(T),
          type);
        return false;
      }

      return true;
    }


    /// <summary>
    ///   Handles quit requests by displaying a confirmation modal.
    /// </summary>
    /// <param name="token">Cancellation token for asynchronous tasks.</param>
    private async UniTask HandleQuitRequest(CancellationToken token)
    {
      void OnConfirm()
      {
        ConfirmAsync().Forget();
      }

      void OnCancel()
      {
        CancelAsync().Forget();
      }

      async UniTask ConfirmAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(ConfirmAsync)).Debug("Confirmed quit request.");
        SetAllCanvasGroupInteractable(false);
        GlobalAsyncMessageBroker.Publish(new InterstitialAlertRequestMessage(LocalizationHelper.GetLocalizedString("Quitting...")));
        await UniTask.Yield();
        QuitGame();
      }

      async UniTask CancelAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(CancelAsync)).Debug("Cancelled quit request.");
        await PopAsync(ECanvasType.Overlay, token);
      }

      await CreateModalView<ModalView>(
        token,
        nameof(HandleQuitRequest),
        LocalizationHelper.GetLocalizedString("Quit Game?"),
        LocalizationHelper.GetLocalizedString("Are you sure you want to quit? All progress will be saved."),
        EModalButtonType.Negative,
        LocalizationHelper.GetLocalizedString("Yes"),
        OnConfirm,
        EModalButtonType.Primary,
        LocalizationHelper.GetLocalizedString("No"),
        OnCancel
        );
    }


    private async UniTask<T> PushAsync<T>(ECanvasType canvasType, T prefab, CancellationToken token) where T : View
    {
      return await navigationService.PushAsync(canvasType, prefab, token);
    }

    public async UniTask PopAsync(ECanvasType canvasType, CancellationToken token)
    {
      await navigationService.PopAsync(canvasType, token);
      await ValidateViewsAsync();
    }

    private async UniTask ClearStackAsync(ECanvasType canvasType, CancellationToken token)
    {
      await navigationService.ClearStackAsync(canvasType, token);
    }

    public bool TryPeek(ECanvasType canvasType, out IView view)
    {
      return navigationService.TryPeek(canvasType, out view);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
      EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }

    /// <summary>
    ///   Subscribes to quit request messages to handle application quit events.
    ///   Ensures no duplicate subscriptions occur.
    /// </summary>
    /// <param name="token">Cancellation token for asynchronous tasks.</param>
    private UniTask SubscribeToMessages(CancellationToken token)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("SubscribeToMessages called");

      // Dispose existing subscription if needed
      disposables?.Dispose();
      disposables = new CompositeDisposable
      {
        // Add all subscriptions to disposables
        GlobalAsyncMessageBroker.GetSubscriber<QuitRequestMessage>()?.Subscribe(_ => HandleQuitRequest(token).Forget()),
        GlobalAsyncMessageBroker.GetSubscriber<NavigationRequestMessage>()?.Subscribe(msg => OnNavigationRequest(msg, token)),
        GlobalAsyncMessageBroker.GetSubscriber<BackRequestMessage>()?.Subscribe(msg => OnBackRequest(msg, token)),
        GlobalAsyncMessageBroker.GetSubscriber<ClearRequestMessage>()?.Subscribe(msg => OnClearRequest(msg, token)),
        GlobalAsyncMessageBroker.GetSubscriber<InterstitialAlertRequestMessage>()?.Subscribe(msg => OnInterstitialAlertRequest(msg, token))
      };

      log.ForGameObject(gameObject).ForMethod().Verbose("SubscribeToMessages completed - all subscriptions added via GlobalAsyncMessageBroker");
      return UniTask.CompletedTask;
    }

    public void OnInterstitialAlertRequest(InterstitialAlertRequestMessage msg, CancellationToken token)
    {
      try
      {
        log.ForGameObject(gameObject).ForMethod().Information("Handling InterstitialAlertRequestMessage: Message={0}", msg.Message);

        // Check if the NavigationSystem is still valid (not destroyed)
        if (this == null || gameObject == null)
        {
          log.ForMethod().Warning("NavigationSystem is being destroyed, skipping interstitial alert request");
          return;
        }

        // Debug: Check if interstitialAlertView is properly assigned
        if (interstitialAlertView == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error("interstitialAlertView is null! Check NavigationPlugin prefab configuration.");
          return;
        }

        log.ForGameObject(gameObject).ForMethod().Debug("interstitialAlertView prefab: {0}, Canvas: {1}",
          interstitialAlertView.name, interstitialAlertView.Canvas);

        UniTask.Void(async () =>
        {
          try
          {
            // Check again before async operation
            if (this == null || gameObject == null || interstitialAlertView == null)
            {
              log.ForMethod().Warning("NavigationSystem or interstitialAlertView destroyed during async operation, aborting");
              return;
            }

            // Check if the prefab's GameObject is still valid (not destroyed)
            if (interstitialAlertView.gameObject == null)
            {
              log.ForMethod().Warning("InterstitialAlertView prefab GameObject has been destroyed, aborting");
              return;
            }

            View view = await ShowViewAsync(interstitialAlertView.Canvas, interstitialAlertView, token);

            // Check again after async operation
            if (this == null || gameObject == null)
            {
              log.ForMethod().Warning("NavigationSystem destroyed after ShowViewAsync, aborting");
              return;
            }

            // Debug: Log the actual type of the returned view
            log.ForGameObject(gameObject).ForMethod().Debug("ShowViewAsync returned view type: {0}, name: {1}",
              view?.GetType().Name ?? "NULL", view?.name ?? "NULL");

            InterstitialAlertView alertView = view as InterstitialAlertView;
            if (alertView != null)
            {
              log.ForGameObject(gameObject).ForMethod().Verbose("Successfully flagged alert={0}.", msg.Message);
              alertView.SetMessage(msg.Message);
            }
            else
            {
              log.ForGameObject(gameObject).ForMethod().Error("InterstitialAlertView was not found while flagged alert={0}. " +
                                                              "Returned view type: {1}, name: {2}", msg.Message,
                view?.GetType().Name ?? "NULL", view?.name ?? "NULL");

              // Try to get the InterstitialAlertView component from the returned view
              if (view != null)
              {
                InterstitialAlertView componentView = view.GetComponent<InterstitialAlertView>();
                if (componentView != null)
                {
                  log.ForGameObject(gameObject).ForMethod().Information("Found InterstitialAlertView component on returned view, using it instead");
                  componentView.SetMessage(msg.Message);
                }
                else
                {
                  log.ForGameObject(gameObject).ForMethod().Error("No InterstitialAlertView component found on returned view");
                }
              }
            }
          }
          catch (Exception ex)
          {
            log.ForMethod().Error("OnInterstitialAlertRequest Exception while flagging alert={0}: {1}", msg.Message,
              ex.Message);
          }
        });
      }
      catch (Exception ex)
      {
        log.ForMethod().Error("OnInterstitialAlertRequest received exception: {0}", ex.Message);
      }
    }


    private void OnNavigationRequest(NavigationRequestMessage msg, CancellationToken token)
    {
      try
      {
        if (msg.Body.View == null)
        {
          log.Warning("NavigationRequestMessage received without view.");
          return;
        }

        log.ForGameObject(gameObject).ForMethod().Information("Handling OnNavigationRequest: CanvasId={0}, ViewType={1}", msg.Body.CanvasType, msg.Body.View);
        UniTask.Void(async () =>
        {
          try
          {
            await ShowViewAsync(msg.Body.CanvasType, msg.Body.View, token);
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).ForMethod().Error("OnNavigationRequest Exception while pushing ViewType={0} on CanvasId={1}: {2}",
              msg.Body.View, msg.Body.CanvasType, ex.Message);
          }
        });
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error("OnNavigationRequest received exception: {0}", ex.Message);
      }
    }

    private void OnBackRequest(BackRequestMessage msg, CancellationToken token)
    {
      try
      {
        log.ForGameObject(gameObject).ForMethod().Information("Handling BackRequest: CanvasId={0}", msg.Canvas);
        UniTask.Void(async () =>
        {
          try
          {
            log.ForGameObject(gameObject).ForMethod().Verbose("Starting pop operation for CanvasId={0}", msg.Canvas);
            await navigationService.PopAsync(msg.Canvas, token);
            log.ForGameObject(gameObject).ForMethod().Verbose("Pop operation completed for CanvasId={0}", msg.Canvas);

            // Immediately check if the canvas is empty and show initial view
            bool hasView = navigationService.TryPeek(msg.Canvas, out IView peekedView);
            log.ForGameObject(gameObject).ForMethod().Verbose("After pop - Canvas {0} has view: {1}, view type: {2}",
              msg.Canvas, hasView, peekedView?.GetType().Name ?? "null");

            if (!hasView)
            {
              log.ForGameObject(gameObject).ForMethod().Verbose("Canvas {0} is empty, showing initial view", msg.Canvas);
              await ShowInitialView(msg.Canvas, token);
              log.ForGameObject(gameObject).ForMethod().Debug("Initial view shown for Canvas {0}", msg.Canvas);
            }
            else
            {
              log.ForGameObject(gameObject).ForMethod().Verbose("Canvas {0} still has view: {1}, not showing initial view",
                msg.Canvas, peekedView != null ? peekedView.GetType().Name : "null");
            }

            await ValidateViewsAsync();
            log.ForGameObject(gameObject).ForMethod().Debug("Successfully popped the top view on CanvasId={0}.", msg.Canvas);
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Exception while popping view on CanvasId={0}: {1}", msg.Canvas,
              ex.Message);
          }
        });
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error("OnBackRequest received exception: {0}", ex.Message);
      }
    }

    private void OnClearRequest(ClearRequestMessage msg, CancellationToken token)
    {
      try
      {
        log.ForGameObject(gameObject).ForMethod().Information("Handling ClearRequest: CanvasId={0}", msg.Canvas);
        UniTask.Void(async () =>
        {
          try
          {
            await ClearStackAsync(msg.Canvas, token);
            log.ForGameObject(gameObject).ForMethod().Verbose("Successfully cleared the view stack on CanvasId={0}.", msg.Canvas);
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Exception while clearing stack on CanvasId={0}: {1}",
              msg.Canvas,
              ex.Message);
          }
        });
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error("ClearRequest received exception: {0}", ex.Message);
      }
    }

    /// <summary>
    ///   Retrieves the <see cref="CanvasConfig" /> for a specified canvas type.
    /// </summary>
    /// <param name="type">Canvas type to find configuration for.</param>
    /// <returns>The <see cref="CanvasConfig" /> if found; otherwise, null.</returns>
    public CanvasConfig GetCanvasConfig(ECanvasType type)
    {
      foreach (KeyValuePair<ECanvasType, CanvasConfig> config in canvasConfigs)
        if (config.Key == type)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("CanvasConfig found for type: {0}", type);
          return config.Value;
        }

      log.ForGameObject(gameObject).ForMethod().Error("CanvasConfig for type {0} not found!", type);
      return null;
    }

    /// <summary>
    ///   Disposes resources used by the <see cref="NavigationSystem" />.
    ///   Unsubscribes from messages and cancels any ongoing tasks.
    /// </summary>
    public void Dispose()
    {
      // Safe logging - use try-catch to handle gameObject access during disposal
      try
      {
        if (gameObject != null)
          log.ForGameObject(gameObject).ForMethod().Verbose("Disposing resources.");
        else
          log.ForMethod().Verbose("Disposing resources (GameObject already destroyed).");
      }
      catch (NullReferenceException)
      {
        // GameObject is destroyed, use safe logging
        log.ForMethod().Verbose("Disposing resources (GameObject destroyed during disposal).");
      }

      disposables?.Dispose();

      // Safely dispose CancellationTokenSource with proper exception handling
      if (cts != null)
      {
        try
        {
          cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
          // CancellationTokenSource was already disposed, ignore
        }

        try
        {
          cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
          // CancellationTokenSource was already disposed, ignore
        }

        cts = null;
      }
    }

    public void Cleanup(ECanvasType canvas, View view)
    {
      navigationService.Cleanup(canvas, view);
    }


    // Helper for normal ModalView dialogs.
    public async UniTask CreateModalView<T>(
      CancellationToken token,
      string modalName,
      string title,
      string message,
      EModalButtonType type1,
      string text1,
      UnityAction action1,
      EModalButtonType type2 = EModalButtonType.None,
      string text2 = null,
      UnityAction action2 = null,
      EModalButtonType type3 = EModalButtonType.None,
      string text3 = null,
      UnityAction action3 = null)
      where T : ModalView
    {
      ECanvasType canvasType = ECanvasType.Overlay;
      CanvasConfig canvasConfig = GetCanvasConfig(canvasType);
      if (canvasConfig == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("{0}: Modal CanvasConfig not found.", modalName);
        return;
      }

      // Prevent duplicate modals (using the prefab name as identifier).
      if (TryPeek(canvasType, out IView existingView)
          && existingView is T modalView
          && modalView.gameObject.name == canvasConfig.GetViewPrefab<T>()?.name)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("{0}: Modal already displayed. Skipping.", modalName);
        return;
      }

      if (DoesCanvasContainPrefabType<T>(canvasType))
      {
        View modalPrefab = canvasConfig.GetViewPrefab<T>();
        if (modalPrefab == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error("{0}: Failed to retrieve ModalView prefab from CanvasConfig: {1}", modalName,
            canvasType);
          return;
        }

        try
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("{0}: Displaying modal.", modalName);
          View modal = await PushAsync(canvasType, modalPrefab, token);
          T newModal = modal.GetComponent<T>();

          newModal.Initialize(
            title,
            message,
            new ModalButtonConfig(type1, text1, action1),
            new ModalButtonConfig(type2, text2, action2),
            new ModalButtonConfig(type3, text3, action3)
            );
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).ForMethod().Error("{0}: Failed to display modal: {1}", modalName, ex.Message);
        }
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Error("{0}: View {1} is not configured to show on canvas {2}.", modalName, typeof(T),
          canvasType);
      }
    }

    // Helper for timed modal dialogs (TimedModalView).
    public async UniTask CreateTimedModalView(
      CancellationToken token,
      string modalName,
      string title,
      string message,
      EModalButtonType type1,
      string text1,
      UnityAction action1,
      float timeout,
      UnityAction timeoutCallback,
      EModalButtonType type2 = EModalButtonType.None,
      string text2 = null,
      UnityAction action2 = null,
      EModalButtonType type3 = EModalButtonType.None,
      string text3 = null,
      UnityAction action3 = null)
    {
      ECanvasType canvasType = ECanvasType.Overlay;
      CanvasConfig canvasConfig = GetCanvasConfig(canvasType);
      if (canvasConfig == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("{0}: Modal CanvasConfig not found.", modalName);
        return;
      }

      if (TryPeek(canvasType, out IView existingView)
          && existingView is TimedModalView modalView
          && modalView.gameObject.name == canvasConfig.GetViewPrefab<TimedModalView>()?.name)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("{0}: TimedModalView already displayed. Skipping.", modalName);
        return;
      }

      if (DoesCanvasContainPrefabType<TimedModalView>(canvasType))
      {
        View modalPrefab = canvasConfig.GetViewPrefab<TimedModalView>();
        if (modalPrefab == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error("{0}: Failed to retrieve TimedModalView prefab from CanvasConfig: {1}", modalName,
            canvasType);
          return;
        }

        try
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("{0}: Displaying timed modal.", modalName);
          View modal = await PushAsync(canvasType, modalPrefab, token);
          TimedModalView newModal = modal.GetComponent<TimedModalView>();

          newModal.Initialize(
            title,
            message,
            new ModalButtonConfig(type1, text1, action1),
            new ModalButtonConfig(type2, text2, action2),
            new ModalButtonConfig(type3, text3, action3),
            timeout,
            timeoutCallback
            );
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).ForMethod().Error("{0}: Failed to display timed modal: {1}", modalName, ex.Message);
        }
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Error("{0}: View {1} is not configured to show on canvas {2}.", modalName,
          typeof(TimedModalView), canvasType);
      }
    }
  }
}