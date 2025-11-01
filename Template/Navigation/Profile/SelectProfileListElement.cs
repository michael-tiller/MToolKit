using System;
using System.Linq;
using MToolKit.Runtime.Persistence.ES3Integration;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Localization;

namespace MToolKit.Template.Navigation.Profile
{
  [RequireComponent(typeof(Button))]
  public class SelectProfileListElement : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SelectProfileListElement>().ForFeature("MToolKit.Template.Navigation.Profile"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;


    [SerializeField][Required] private Button button;
    [SerializeField][Required] private Image highlight;
    [SerializeField][Required] private TextMeshProUGUI ageText;
    [SerializeField][Required] private TextMeshProUGUI profileNameText;
    [SerializeField][Required] private TextMeshProUGUI profileTimestampText;
    [SerializeField][Required] private TextMeshProUGUI profileVersionText;

    [ReadOnly][ShowInInspector]private SelectProfileView selectProfileView;
    [ReadOnly][ShowInInspector] private ReactiveProperty<string> profileId = new();

    [Inject] private IProfileManager profileManager;

    CompositeDisposable disposables = new();
    
    
    [Inject]
    private void Construct()
    {
      log.ForMethod().Debug("Dependency injection completed - profileManager is {0}", 
        profileManager != null ? "NOT NULL" : "NULL");
    }
    private void OnDestroy()
    {
      disposables.Dispose();
      disposables = null;
    }

    private void Reset()
    {
      button = GetComponent<Button>();

      highlight = GetComponentsInChildren<Image>().FirstOrDefault(x => x.name.Contains("Highlight"));
      ageText = GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.name.Contains("Age"));
      profileNameText = GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.name.Contains("Filename"));
      profileTimestampText = GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.name.Contains("Timestamp"));
      profileVersionText = GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(x => x.name.Contains("Version"));
    }

    private void Start()
    {
      button.onClick.AddListener(OnButtonClicked);
    }

    private void ProfileIdHandler(string profileId)
    {
      if (string.IsNullOrEmpty(profileId))
        return;
        
      try
      {
        log.ForMethod().Debug("Getting metadata for profile: {0}", profileId);
        
        // Get profile metadata from ProfileManager
        ProfileMetaData metadata = profileManager.GetProfileMetaData(profileId);
        log.ForMethod().Debug("Metadata retrieved: {0}", metadata != null ? "not null" : "null");
        
        if (metadata != null)
        {
          log.ForMethod().Debug("Metadata details - ProfileName: '{0}', LastSaveTime: '{1}', SaveFormatVersion: '{2}', SaveCounter: {3}", 
            metadata.ProfileName ?? "NULL", 
            metadata.LastSaveTime ?? "NULL", 
            metadata.SaveFormatVersion ?? "NULL", 
            metadata.SaveCounter);
        }
        
        if (metadata != null)
        {
          log.ForMethod().Debug("ProfileName: {0}, LastSaveTime: {1}, SaveFormatVersion: {2}", 
            metadata.ProfileName ?? "null", metadata.LastSaveTime ?? "null", metadata.SaveFormatVersion ?? "null");
          
          // Safely set text components with null checks for both UI components and metadata properties
          if (profileNameText != null)
          {
            profileNameText.text = metadata.ProfileName ?? profileId;
            log.ForMethod().Debug("Set profileNameText");
          }
          if (profileTimestampText != null)
          {
            profileTimestampText.text = metadata.LastSaveTime ?? LocalizationHelper.GetLocalizedString("Unknown");
            log.ForMethod().Debug("Set profileTimestampText");
          }
          if (profileVersionText != null)
          {
            profileVersionText.text = metadata.SaveFormatVersion ?? LocalizationHelper.GetLocalizedString("Unknown");
            log.ForMethod().Debug("Set profileVersionText");
          }
          
          // Calculate age (time since last save)
          if (ageText != null && !string.IsNullOrEmpty(metadata.LastSaveTime) && DateTime.TryParse(metadata.LastSaveTime, out DateTime lastSaveTime))
          {
            TimeSpan age = DateTime.Now - lastSaveTime;
            if (age.TotalDays >= 365)
              ageText.text = $"{(int)age.TotalDays / 365} " + LocalizationHelper.GetLocalizedString(age.TotalDays == 1 ? "Year" : "Years");
            else if (age.TotalDays >= 30)
              ageText.text = $"{(int)age.TotalDays / 30} " + LocalizationHelper.GetLocalizedString(age.TotalDays == 1 ? "Month" : "Months");
            else if (age.TotalDays >= 1)
              ageText.text = $"{(int)age.TotalDays} " + LocalizationHelper.GetLocalizedString(age.TotalDays == 1 ? "Day" : "Days");
            else if (age.TotalHours >= 1)
              ageText.text = $"{(int)age.TotalHours} " + LocalizationHelper.GetLocalizedString(age.TotalHours == 1 ? "Hour" : "Hours");
            else if (age.TotalMinutes >= 1)
              ageText.text = $"{(int)age.TotalMinutes} " + LocalizationHelper.GetLocalizedString(age.TotalMinutes == 1 ? "Minute" : "Minutes");
            else
              ageText.text = LocalizationHelper.GetLocalizedString("Just now");
            log.ForMethod().Debug("Set ageText");
          }
          else if (ageText != null)
          {
            ageText.text = LocalizationHelper.GetLocalizedString("Unknown");
            log.ForMethod().Debug("Set ageText to Unknown");
          }
          
          log.ForMethod().Debug("Successfully loaded profile metadata for {0}", metadata.ProfileName ?? profileId);
        }
        else
        {
          log.ForMethod().Debug("Metadata is null, using fallback");
          // Fallback to basic info with null checks
          if (profileNameText != null)
            profileNameText.text = profileId;
          if (profileTimestampText != null)
            profileTimestampText.text = LocalizationHelper.GetLocalizedString("Unknown");
          if (profileVersionText != null)
            profileVersionText.text = LocalizationHelper.GetLocalizedString("Unknown");
          if (ageText != null)
            ageText.text = LocalizationHelper.GetLocalizedString("Unknown");
          
          log.ForMethod().Warning("Could not load metadata for profile: {0}", profileId);
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Error loading profile metadata for {0}: {Message}", profileId, ex.Message);
        
        // Fallback to basic info with null checks
        if (profileNameText != null)
          profileNameText.text = profileId;
        if (profileTimestampText != null)
          profileTimestampText.text = LocalizationHelper.GetLocalizedString("Error");
        if (profileVersionText != null)
          profileVersionText.text = LocalizationHelper.GetLocalizedString("Error");
        if (ageText != null)
          ageText.text = LocalizationHelper.GetLocalizedString("Error");
      }
    }

    private void SelectedProfileHandler(string x)
    {
      if (highlight != null)
        highlight.gameObject.SetActive(x == profileId.Value);
    }

    public void Setup(SelectProfileView selectProfileView, string profileId)
    {
      this.selectProfileView = selectProfileView;
      this.profileId.Value = profileId;
      
      // Subscribe to reactive properties
      disposables.Add(selectProfileView.SelectedProfile.Subscribe(SelectedProfileHandler));
      disposables.Add(this.profileId.Subscribe(ProfileIdHandler));
    }

    public void OnButtonClicked()
    {
      selectProfileView.SelectedProfile.Value = profileId.Value;
    }
  }
}