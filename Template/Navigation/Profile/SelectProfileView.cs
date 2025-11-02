using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Views;
using MToolKit.Runtime.Persistence.ES3Integration;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Navigation.Events;
using UnityEngine.EventSystems;

namespace MToolKit.Template.Navigation.Profile
{
  public class SelectProfileView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SelectProfileView>().ForFeature("MToolKit.Template.Navigation.Profile"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    [SerializeField][Required] private Button selectProfileButton;
    [SerializeField][Required] private Button cancelButton;

    [ReadOnly][ShowInInspector] public ReactiveProperty<string> SelectedProfile = new();

    [SerializeField][Required] private SelectProfileListElement selectProfileListElementPrototype;
    [ReadOnly][ShowInInspector] private List<SelectProfileListElement> SelectProfileListElements = new();

    [Inject] private IProfileManager profileManager;
    [Inject] private IObjectResolver container;

    [Inject] private IContentLoaderService contentLoader;
    private CompositeDisposable disposables = new();

    private void ClearSelectProfileListElements()
    {
      if (SelectProfileListElements == null || SelectProfileListElements.Count == 0) return;
      foreach (var element in SelectProfileListElements)
      {
        Destroy(element.gameObject);
      }
      SelectProfileListElements.Clear();
    }

    private void InitializeSelectProfileListElements()
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Starting InitializeSelectProfileListElements");

      // Check each dependency step by step
      if (selectProfileListElementPrototype == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("selectProfileListElementPrototype is NULL!");
        return;
      }
      log.ForGameObject(gameObject).ForMethod().Debug("selectProfileListElementPrototype is OK");

      if (profileManager == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("profileManager is NULL!");
        return;
      }
      log.ForGameObject(gameObject).ForMethod().Debug("profileManager is OK");

      if (container == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("container (IObjectResolver) is NULL!");
        return;
      }
      log.ForGameObject(gameObject).ForMethod().Debug("container is OK");

      selectProfileListElementPrototype.gameObject.SetActive(false);
      ClearSelectProfileListElements();

      try
      {
        log.ForGameObject(gameObject).ForMethod().Debug("Getting AvailableProfiles from profileManager");
        var allProfiles = profileManager.AvailableProfiles.Value;

        if (allProfiles == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error("allProfiles is NULL!");
          return;
        }
        log.ForGameObject(gameObject).ForMethod().Debug("allProfiles retrieved, count: {0}", allProfiles.Count);

        if (allProfiles.Count == 0)
        {
          log.ForGameObject(gameObject).ForMethod().Information("No profiles found");
          return;
        }

        // Sort profiles by LastSaveTime (newest first)
        var sortedProfiles = allProfiles
          .Select(profileName => new { ProfileName = profileName, Metadata = profileManager.GetProfileMetaData(profileName) })
          .OrderByDescending(p =>
          {
            if (p.Metadata?.LastSaveTime != null && DateTime.TryParse(p.Metadata.LastSaveTime, out DateTime lastSaveTime))
              return lastSaveTime;
            return DateTime.MinValue; // Put profiles with invalid/missing timestamps at the end
          })
          .Select(p => p.ProfileName)
          .ToList();

        // Create a SelectProfileListElement for each profile using VContainer's Instantiate
        foreach (var profileName in sortedProfiles)
        {
          log.ForGameObject(gameObject).ForMethod().Debug("Processing profile: {0}", profileName);

          if (selectProfileListElementPrototype.transform.parent == null)
          {
            log.ForGameObject(gameObject).ForMethod().Error("selectProfileListElementPrototype.transform.parent is NULL!");
            continue;
          }

          SelectProfileListElement element = container.Instantiate(selectProfileListElementPrototype, selectProfileListElementPrototype.transform.parent);

          if (element == null)
          {
            log.ForGameObject(gameObject).ForMethod().Error("container.Instantiate returned NULL for profile: {0}", profileName);
            continue;
          }

          element.gameObject.SetActive(true);

          element.Setup(this, profileName);
          SelectProfileListElements.Add(element);

          log.ForGameObject(gameObject).ForMethod().Debug("Successfully created element for profile: {0}", profileName);
        }

        log.ForGameObject(gameObject).ForMethod().Debug("Initialized {0} profile elements", allProfiles.Count);
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Failed to initialize profile list elements: {Message}", ex.Message);
      }
    }

    public override void Show()
    {
      base.Show();
      EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
    }

    private void Start()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Subscribing to the selected profile");
      disposables.Add(SelectedProfile.Subscribe(SelectedProfileChangedHandler));
      OnSetInputFieldText(SelectedProfile.Value);
      selectProfileButton.onClick.AddListener(() => OnNextButtonClickedAsync().Forget());
      cancelButton.onClick.AddListener(OnBackButtonClicked);

      // Initialize the profile list
      InitializeSelectProfileListElements();
    }

    private void OnDestroy()
    {
      disposables.Dispose();
      disposables = null;
    }

    public void OnSetInputFieldText(string text)
    {
      if (SelectedProfile.Value != text) SelectedProfile.Value = text;
    }

    private void SelectedProfileChangedHandler(string text)
    {
      log.ForGameObject(gameObject).ForMethod().Information(text);
      if (string.IsNullOrEmpty(text) == false)
        selectProfileButton.interactable = true;
      // route the profile name to the player save details
      else
        selectProfileButton.interactable = false;
    }

    private async UniTask OnNextButtonClickedAsync()
    {
      log.ForGameObject(gameObject).ForMethod().Information("Selected profile: {0}", SelectedProfile.Value);

      if (string.IsNullOrEmpty(SelectedProfile.Value))
      {
        log.ForGameObject(gameObject).ForMethod().Warning("No profile selected");
        return;
      }

      try
      {
        // Load the selected profile
        // Show interstitial alert immediately when user clicks the button
        GlobalAsyncMessageBroker.Publish(new InterstitialAlertRequestMessage(LocalizationHelper.GetLocalizedString("Loading profile...")));
        var success = await profileManager.LoadProfileAsync(SelectedProfile.Value);
        if (success)
        {
          log.ForGameObject(gameObject).ForMethod().Information("Successfully loaded profile: {0}", SelectedProfile.Value);

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
          log.ForGameObject(gameObject).ForMethod().Error("Failed to load profile: {0}", SelectedProfile.Value);
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Error loading profile {0}: {Message}", SelectedProfile.Value, ex.Message);
      }
    }

    public void OnBackButtonClicked()
    {
      log.ForGameObject(gameObject).ForMethod().Information(SelectedProfile.Value);
      GlobalAsyncMessageBroker.Publish(new BackRequestMessage(ECanvasType.Main));
    }
  }
}