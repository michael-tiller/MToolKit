using System;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Views;
using MToolKit.Runtime.Persistence.ES3Integration;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Navigation.Events;
using UnityEngine.EventSystems;

namespace MToolKit.Template.Navigation.Profile
{
  public class NewProfileView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<NewProfileView>().ForFeature("MToolKit.Template.Navigation.Profile"));
   private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    [SerializeField][Required] private InputFieldWithText profileNameInputField;
    [SerializeField][Required] private Button createProfileButton;
    [SerializeField][Required] private Button cancelButton;

    [Inject] private IProfileManager profileManager;
    [Inject] private IContentLoaderService contentLoader;

    private IModalService modalService;
    private ES3GameSaveSystem gameSaveSystem;
    private readonly ReactiveProperty<string> inputValue = new();
    private bool isCreatingProfile = false;

    [ReadOnly][ShowInInspector] private string InputValueValue => inputValue.Value;

    [Inject]
    public void Construct(IModalService modalService, ES3GameSaveSystem gameSaveSystem)
    {
      this.modalService = modalService;
      this.gameSaveSystem = gameSaveSystem;
    }

    private void Start()
    {
      profileNameInputField.InputField.text = LocalizationHelper.GetLocalizedString("DefaultPlayerName");
      profileNameInputField.InputField.onEndEdit.AddListener(OnSetInputFieldText);
      inputValue.Subscribe(InputValueChangedHandler);
      OnSetInputFieldText(profileNameInputField.InputField.text);
      
      createProfileButton.onClick.AddListener(OnNextButtonClicked);
      cancelButton.onClick.AddListener(OnBackButtonClicked);
    }

    public void OnSetInputFieldText(string text)
    {
      inputValue.Value = text;
    }

    private void InputValueChangedHandler(string text)
    {
      log.ForGameObject(gameObject).ForMethod().Information("{0}: {1}", nameof(InputValueChangedHandler), text);
      
      // Validate profile name (no longer check for duplicates since we handle that automatically)
      bool isValid = !string.IsNullOrEmpty(text) && 
                     !string.IsNullOrWhiteSpace(text) && 
                     text.Length >= 3 && 
                     text.Length <= 50;
      
      createProfileButton.interactable = isValid;
      
      if (!isValid && !string.IsNullOrEmpty(text))
      {
        if (text.Length < 3)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Profile name must be at least 3 characters");
        }
        else if (text.Length > 50)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Profile name must be no more than 50 characters");
        }
      }
    }

    public void OnNextButtonClicked()
    {
      if (isCreatingProfile)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Profile creation already in progress, ignoring click");
        return;
      }
      
      OnNextButtonClickedAsync().Forget();
    }

    private async UniTask OnNextButtonClickedAsync()
    {
      var profileName = inputValue.Value?.Trim();
      
      if (string.IsNullOrEmpty(profileName))
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot create profile with empty name");
        return;
      }
      
      // Set flag to prevent concurrent operations
      isCreatingProfile = true;
      
      log.ForGameObject(gameObject).ForMethod().Information("Creating new profile: {0}", profileName);
      
      try
      {
        // Disable button during creation
        createProfileButton.interactable = false;
        
        // Show interstitial alert immediately when user clicks the button
        GlobalAsyncMessageBroker.Publish(new InterstitialAlertRequestMessage(LocalizationHelper.GetLocalizedString("Creating profile...")));

        if (profileName == string.Empty)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Cannot create profile with empty name, using default name");
          profileName = "Player";
          InputValueChangedHandler(profileName);
        }
        
        // Create the profile and get the actual name used
        var (success, actualProfileName) = profileManager.CreateProfileWithName(profileName);
        
        if (success)
        {
          log.ForGameObject(gameObject).ForMethod().Information("Successfully created profile: {0}", actualProfileName);
          
          // If a different name was used, inform the user
          if (actualProfileName != profileName)
          {
            log.ForGameObject(gameObject).ForMethod().Information("Profile name '{0}' was already taken, created as '{1}'", profileName, actualProfileName);
          }
          
          // Load the newly created profile
          bool loadSuccess = await profileManager.LoadProfileAsync(actualProfileName);
          if (loadSuccess)
          {
            log.ForGameObject(gameObject).ForMethod().Information("Successfully loaded new profile: {0}", actualProfileName);
            
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
          else
          {
            log.ForGameObject(gameObject).ForMethod().Error("Failed to load newly created profile: {0}", actualProfileName);
            // Re-enable button so user can try again
            createProfileButton.interactable = true;
            isCreatingProfile = false;
          }
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod().Error("Failed to create profile: {0}", profileName);
          // Re-enable button so user can try again
          createProfileButton.interactable = true;
          isCreatingProfile = false;
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Error creating profile {0}: {Message}", profileName, ex.Message);
        // Re-enable button so user can try again
        createProfileButton.interactable = true;
        isCreatingProfile = false;
      }
  
    }

    public override void Show()
    {
      base.Show();
      EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
    }

    public void OnBackButtonClicked()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Going to main menu");
      GlobalAsyncMessageBroker.Publish(new BackRequestMessage(ECanvasType.Main));
    }
  }
}