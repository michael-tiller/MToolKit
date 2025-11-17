using System;
using System.Collections.Generic;
using MToolKit.Runtime.Utilities;
using R3;
using Serilog;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Operations;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Localization
{
  public class LocalizationSystem : Singleton<LocalizationSystem>
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<LocalizationSystem>().ForFeature("Localization"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    protected override bool selfCreate => true;
    protected override bool dontDestroyOnLoad => true;

    public readonly Subject<string> Language = new();

    /// <summary>
    ///   Gets whether the localization system has been initialized
    /// </summary>
    private bool isInitialized;

    // ReSharper disable once ConvertToAutoPropertyWithPrivateSetter
    public bool IsInitialized => isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnRuntimeMethodLoad()
    {
      log.ForMethod().Information("Creating {0} singleton", nameof(LocalizationSystem));
      // Create the singleton directly to avoid the Instance property's temporary GameObject issue
      GameObject singletonObject = new($"[Singleton] {nameof(LocalizationSystem)}");
      singletonObject.AddComponent<LocalizationSystem>();
    }

    protected override void Awake()
    {
      base.Awake();
      // Initialize immediately and synchronously to prevent race conditions
      InitializeLocalization();
    }

    public static bool LocalizationSettingsReady
    {
      get
      {
        if (!LocalizationSettings.HasSettings || LocalizationSettings.AvailableLocales == null)
          return false;

        // Wait for preload operation if needed (locales are loaded asynchronously from Addressables)
        if (LocalizationSettings.AvailableLocales is IPreloadRequired localesProvider)
        {
          var preloadOp = localesProvider.PreloadOperation;
          if (!preloadOp.IsDone)
          {
#if !UNITY_WEBGL
            preloadOp.WaitForCompletion();
#endif
          }
        }

        return LocalizationSettings.AvailableLocales.Locales != null &&
               LocalizationSettings.AvailableLocales.Locales.Count > 0;
      }
    }

    private void InitializeLocalization()
    {
      try
      {
        // Wait for LocalizationSettings to be ready, especially important in Player mode
        if (LocalizationSettings.HasSettings)
        {
          var initOp = LocalizationSettings.InitializationOperation;
          if (!initOp.IsDone)
          {
            // Wait for initialization to complete synchronously (supported on Mono backend, not WebGL)
#if !UNITY_WEBGL
            initOp.WaitForCompletion();
#endif
          }
        }

        // Check if localization settings are available and have locales
        if (LocalizationSettingsReady)
        {
          isInitialized = true;
          log.ForGameObject(gameObject).ForMethod().Information("Localization system initialized immediately");
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Localization settings not ready yet, marking as initialized anyway");
          isInitialized = true; // Set to true to prevent infinite waiting
        }

        // Check if we have a saved language preference
        if (PlayerPrefs.HasKey("Language"))
        {
          string savedLanguage = PlayerPrefs.GetString("Language");
          log.ForGameObject(gameObject).ForMethod().Information("Setting locale to {0}", savedLanguage);
          SetNewLocale(savedLanguage);
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod().Information("No language preference found. Using system default");
          // Let Unity's localization system handle the default locale selection
          string currentLocale = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
          Language.OnNext(currentLocale);
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Error during localization initialization");
        isInitialized = true; // Set to true anyway to prevent infinite waiting
      }
    }

    public void SetNewLocale(string languageCode)
    {
      if (!IsInitialized)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Localization system not initialized yet. Cannot set locale to {0}", languageCode);
        return;
      }

      // Handle null or empty language code gracefully
      if (string.IsNullOrEmpty(languageCode))
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Language code is null or empty. Cannot set locale.");
        return;
      }

      try
      {
        // Ensure LocalizationSettings is initialized before accessing AvailableLocales
        if (LocalizationSettings.HasSettings)
        {
          var initOp = LocalizationSettings.InitializationOperation;
          if (!initOp.IsDone)
          {
#if !UNITY_WEBGL
            initOp.WaitForCompletion();
#endif
          }
        }

        // Check if AvailableLocales is ready
        if (LocalizationSettings.AvailableLocales == null)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("AvailableLocales is null. Cannot set locale to {0}", languageCode);
          // Still save to PlayerPrefs so it can be restored later
          PlayerPrefs.SetString("Language", languageCode);
          return;
        }

        // Ensure locales are loaded - explicitly wait for PreloadOperation if needed
        // The Locales property getter will wait, but only in play mode, so we ensure it here for Player mode
        if (LocalizationSettings.AvailableLocales is IPreloadRequired localesProvider)
        {
          var preloadOp = localesProvider.PreloadOperation;
          if (!preloadOp.IsDone)
          {
#if !UNITY_WEBGL
            preloadOp.WaitForCompletion();
#endif
          }

          // Check if the preload operation succeeded
          if (preloadOp.Status == AsyncOperationStatus.Failed)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Failed to load locales: {0}. Cannot set locale to {1}",
              preloadOp.OperationException?.Message ?? "Unknown error", languageCode);
            // Still save to PlayerPrefs so it can be restored later
            PlayerPrefs.SetString("Language", languageCode);
            return;
          }
        }

        // Access Locales property (it will also wait, but we've already waited above)
        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales == null || locales.Count == 0)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("No locales available after preload. Cannot set locale to {0}. " +
            "This may indicate that Addressables are not properly configured or locales are not built.", languageCode);
          // Still save to PlayerPrefs so it can be restored later
          PlayerPrefs.SetString("Language", languageCode);
          return;
        }

        // Try to find the locale by code
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(languageCode);

        if (locale != null)
        {
          LocalizationSettings.SelectedLocale = locale;
          Language.OnNext(languageCode);
          PlayerPrefs.SetString("Language", languageCode);
          log.ForGameObject(gameObject).ForMethod().Information("Successfully set locale to {0}", languageCode);
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Locale '{0}' not found in available locales. Available locales: {1}",
            languageCode,
            string.Join(", ", LocalizationSettings.AvailableLocales.Locales.ConvertAll(l => l.Identifier.Code)));

          // Fallback to first available locale
          if (LocalizationSettings.AvailableLocales.Locales.Count > 0)
          {
            Locale fallbackLocale = LocalizationSettings.AvailableLocales.Locales[0];
            LocalizationSettings.SelectedLocale = fallbackLocale;
            string fallbackCode = fallbackLocale.Identifier.Code;
            Language.OnNext(fallbackCode);
            PlayerPrefs.SetString("Language", fallbackCode);
            log.ForGameObject(gameObject).ForMethod().Information("Falling back to locale: {0}", fallbackCode);
          }
          else
          {
            log.ForGameObject(gameObject).ForMethod().Error("No available locales found!");
          }
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Error setting locale to {0}", languageCode);
      }
    }

    /// <summary>
    ///   Gets the list of available locale codes
    /// </summary>
    public List<string> GetAvailableLocaleCodes()
    {
      if (!IsInitialized)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Localization system not initialized yet. Cannot get available locales.");
        return new List<string>();
      }

      // Ensure LocalizationSettings is initialized before accessing AvailableLocales
      if (LocalizationSettings.HasSettings)
      {
        var initOp = LocalizationSettings.InitializationOperation;
        if (!initOp.IsDone)
        {
#if !UNITY_WEBGL
          initOp.WaitForCompletion();
#endif
        }

        // Wait for locales to be loaded from Addressables
        if (LocalizationSettings.AvailableLocales is IPreloadRequired localesProvider)
        {
          var preloadOp = localesProvider.PreloadOperation;
          if (!preloadOp.IsDone)
          {
#if !UNITY_WEBGL
            preloadOp.WaitForCompletion();
#endif
          }
        }
      }

      if (!LocalizationSettingsReady || LocalizationSettings.AvailableLocales == null ||
          LocalizationSettings.AvailableLocales.Locales == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("AvailableLocales is not ready. Returning empty list.");
        return new List<string>();
      }

      List<string> codes = new();
      foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
        codes.Add(locale.Identifier.Code);
      return codes;
    }

    /// <summary>
    ///   Gets the currently selected locale code
    /// </summary>
    public string GetCurrentLocaleCode()
    {
      if (!IsInitialized)
        return "en"; // Default fallback

      return LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
    }
  }
}