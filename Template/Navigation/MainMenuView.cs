using System;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.DataStructures;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Views;
using MToolKit.Runtime.Persistence.ES3Integration;
using Cysharp.Threading.Tasks;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Navigation.Events;
using UnityEngine.EventSystems;

namespace MToolKit.Template.Navigation
{
  public class MainMenuView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<MainMenuView>().ForFeature("MToolKit.Template.Navigation"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;


    [SerializeField][Required] private NavigationRequestMessageBody newGameViewRequestBody;
    [SerializeField][Required] private NavigationRequestMessageBody loadViewRequestBody;

    [SerializeField][Required] private NavigationRequestMessageBody settingsViewRequestBody;

    [SerializeField][Required] private UnityEngine.UI.Button continueButton;
    [SerializeField][Required] private UnityEngine.UI.Button newGameButton;
    [SerializeField][Required] private UnityEngine.UI.Button loadButton;

    [SerializeField][Required] private UnityEngine.UI.Button localizationButton;
    [SerializeField][Required] private GameObject localizationMenuContainer;

    private IModalService modalService;
    private ES3GameSaveSystem gameSaveSystem;
    private IProfileManager profileManager;
    private IContentLoaderService contentLoader;
    private bool isConstructed;
    private CompositeDisposable disposables = new();

    [Inject]
    public void Construct(IModalService modalService, ES3GameSaveSystem gameSaveSystem, IProfileManager profileManager, IContentLoaderService contentLoader)
    {
      if (isConstructed)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("called twice, ignoring second call");
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Verbose("Constructing");
      this.modalService = modalService;
      this.gameSaveSystem = gameSaveSystem;
      this.profileManager = profileManager;
      this.contentLoader = contentLoader;
      isConstructed = true;


      // Set up reactive subscriptions
      SetupReactiveSubscriptions();

      // Initialize button states asynchronously to ensure ProfileManager is ready
      InitializeButtonStatesAsync().Forget();
      localizationMenuContainer.SetActive(false);
    }


    #region Button Methods

    public void NewGame()
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked NewGame");
      NewGameAsync().Forget();
    }

    public void Continue()
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked Continue");
      ContinueAsync().Forget();
    }

    public void LoadGame()
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked LoadGame");
      GlobalAsyncMessageBroker.Publish(new NavigationRequestMessage(loadViewRequestBody));
    }

    public void Settings()
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked Settings");
      GlobalAsyncMessageBroker.Publish(new NavigationRequestMessage(settingsViewRequestBody));
    }

    public void OnQuit()
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked OnQuit");
      GlobalAsyncMessageBroker.Publish(new QuitRequestMessage());
    }


    public void SetLanguage(string language)
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked SetLocalization: {0}", language);

      LocalizationSystem.Instance.SetNewLocale(language);
    }
    public void ToggleLocalizationMenu()
    {
      localizationMenuContainer.SetActive(!localizationMenuContainer.activeSelf);
    }
    #endregion

    #region Localization
    #endregion

    #region Initialization

    private async UniTask InitializeButtonStatesAsync()
    {
      // Wait a frame to ensure ProfileManager has finished its initialization
      await UniTask.NextFrame();

      // Give ProfileManager a moment to scan the filesystem
      await UniTask.Delay(100);

      log.ForGameObject(gameObject).ForMethod(nameof(InitializeButtonStatesAsync)).Information("Initializing button states after ProfileManager setup");

      // Now update button states
      UpdateContinueButtonState();
      UpdateLoadButtonState();

      localizationButton.onClick.AddListener(ToggleLocalizationMenu);

      EventSystem.current.SetSelectedGameObject(profileManager.AvailableProfiles.Value.Count > 0 ? continueButton.gameObject : newGameButton.gameObject);
    }

    public override void Show()
    {
      base.Show();
      EventSystem.current.SetSelectedGameObject(profileManager.AvailableProfiles.Value.Count > 0 ? continueButton.gameObject : newGameButton.gameObject);
    }

    #endregion

    #region Save File Management

    private void UpdateContinueButtonState()
    {
      if (continueButton == null || profileManager == null)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(UpdateContinueButtonState)).Warning("Continue button or ProfileManager not available");
        return;
      }

      try
      {
        // Check if any profiles exist
        var availableProfiles = profileManager.AvailableProfiles.Value;
        bool hasSaveFile = availableProfiles != null && availableProfiles.Count > 0;

        continueButton.interactable = hasSaveFile;

        log.ForGameObject(gameObject).ForMethod(nameof(UpdateContinueButtonState)).Debug("Continue button state updated: {0} (profiles: {1}) - Profile names: [{2}]",
          hasSaveFile ? "enabled" : "disabled",
          availableProfiles?.Count ?? 0,
          availableProfiles != null ? string.Join(", ", availableProfiles) : "null");
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(UpdateContinueButtonState)).Error(ex, "Failed to check profile existence: {Message}", ex.Message);
        continueButton.interactable = false;
      }
    }

    private void UpdateLoadButtonState()
    {
      if (loadButton == null || profileManager == null)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(UpdateLoadButtonState)).Warning("Load button or ProfileManager not available");
        return;
      }

      try
      {
        // Check if any profiles exist
        var availableProfiles = profileManager.AvailableProfiles.Value;
        bool hasSaveFile = availableProfiles != null && availableProfiles.Count > 0;

        loadButton.interactable = hasSaveFile;

        log.ForGameObject(gameObject).ForMethod(nameof(UpdateLoadButtonState)).Debug("Load button state updated: {0} (profiles: {1}) - Profile names: [{2}]",
          hasSaveFile ? "enabled" : "disabled",
          availableProfiles?.Count ?? 0,
          availableProfiles != null ? string.Join(", ", availableProfiles) : "null");
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(UpdateLoadButtonState)).Error(ex, "Failed to check profile existence: {Message}", ex.Message);
        loadButton.interactable = false;
      }
    }

    private void OnDestroy()
    {
      disposables?.Dispose();
    }

    private void SetupReactiveSubscriptions()
    {
      if (profileManager == null || continueButton == null || loadButton == null)
      {
        log.ForGameObject(gameObject).ForMethod(nameof(SetupReactiveSubscriptions)).Warning("ProfileManager, continueButton, or loadButton not available for reactive setup");
        return;
      }

      // React to profile changes - when AvailableProfiles changes, update both buttons
      profileManager.AvailableProfiles
        .Subscribe(_ =>
        {
          UpdateContinueButtonState();
          UpdateLoadButtonState();
        })
        .AddTo(disposables);

      log.ForGameObject(gameObject).ForMethod(nameof(SetupReactiveSubscriptions)).Debug("Reactive subscriptions set up for continue and load button states");
    }

    /// <summary>
    /// Public method to refresh button states (can be called when returning to main menu)
    /// </summary>
    public void RefreshButtonStates()
    {
      UpdateContinueButtonState();
      UpdateLoadButtonState();
    }

    #endregion

    #region Async Methods

    private UniTask NewGameAsync()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Starting New Game");
      GlobalAsyncMessageBroker.Publish(new NavigationRequestMessage(newGameViewRequestBody));
      return UniTask.CompletedTask;
    }

    private async UniTask ContinueAsync()
    {
      log.ForGameObject(gameObject).ForMethod().Information("Continuing Game");

      // Guard clause: Don't proceed if no profiles are available
      if (profileManager == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("ProfileManager not available, cannot continue game");
        return;
      }

      var availableProfiles = profileManager.AvailableProfiles.Value;
      if (availableProfiles == null || availableProfiles.Count == 0)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("No profiles available, cannot continue game. AvailableProfiles: {0}",
          availableProfiles == null ? "null" : $"Count={availableProfiles.Count}");
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Debug("Proceeding with continue - found {0} profiles: [{1}]",
        availableProfiles.Count, string.Join(", ", availableProfiles));

      try
      {
        // Get the most recent profile
        if (profileManager != null)
        {
          string mostRecentProfile = await profileManager.GetMostRecentProfileAsync();
          log.ForGameObject(gameObject).ForMethod().Debug("GetMostRecentProfileAsync returned: '{0}'", mostRecentProfile ?? "null");

          GlobalAsyncMessageBroker.Publish(new InterstitialAlertRequestMessage(LocalizationHelper.GetLocalizedString("Loading profile...")));

          if (!string.IsNullOrEmpty(mostRecentProfile))
          {
            log.ForGameObject(gameObject).ForMethod().Information("Loading most recent profile: {0}", mostRecentProfile);

            // Load the most recent profile
            bool profileLoaded = await profileManager.LoadProfileAsync(mostRecentProfile);

            if (profileLoaded)
            {
              log.ForGameObject(gameObject).ForMethod().Information("Profile loaded successfully, loading save data");

              // Load save data using the game save system
              if (gameSaveSystem != null)
              {
                await gameSaveSystem.LoadAsync();
                log.ForGameObject(gameObject).ForMethod().Information("Save data loaded successfully");
              }
              else
              {
                log.ForGameObject(gameObject).ForMethod().Warning("GameSaveSystem not available, continuing without loading save data");
              }
            }
            else
            {
              log.ForGameObject(gameObject).ForMethod().Error("Failed to load profile: {0}", mostRecentProfile);
              // Continue anyway - let the game handle missing save data
            }
          }
          else
          {
            log.ForGameObject(gameObject).ForMethod().Warning("GetMostRecentProfileAsync returned empty result, but profiles exist. This may indicate a timing issue.");
            // Don't continue to game scene if we can't find a profile to load
            return;
          }
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod().Warning("ProfileManager not available, continuing without loading save data");
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Failed to load save data: {Message}", ex.Message);
        // Continue anyway - let the game handle missing save data
      }

      var sceneRef = GlobalConstants.Instance?.GlobalConstantsConfig?.GameplaySceneReference;
      if (sceneRef != null && sceneRef.RuntimeKeyIsValid())
      {
        await contentLoader.LoadSceneAsync(sceneRef);
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Error("Invalid or missing GameplaySceneReference in GlobalConstantsConfig");
      }
    }

    #endregion
  }
}