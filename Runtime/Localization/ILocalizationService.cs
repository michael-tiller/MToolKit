using System;
using System.Collections.Generic;
using R3;

namespace MToolKit.Runtime.Localization
{
  /// <summary>
  ///   Interface for localization services, allowing for dependency injection and testing
  /// </summary>
  public interface ILocalizationService
  {
    /// <summary>
    ///   Gets whether the localization system has been initialized
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    ///   Observable stream of language code changes (R3 Observable)
    /// </summary>
    Observable<string> Language { get; }

    /// <summary>
    ///   Initializes the localization system
    /// </summary>
    void InitializeLocalization();

    /// <summary>
    ///   Sets the current locale by language code
    /// </summary>
    /// <param name="languageCode">The language code (e.g., "en", "fr", "es")</param>
    bool SetNewLocale(string languageCode);

    /// <summary>
    ///   Gets the list of available locale codes
    /// </summary>
    /// <returns>List of language codes</returns>
    List<string> GetAvailableLocaleCodes();

    /// <summary>
    ///   Gets the currently selected locale code
    /// </summary>
    /// <returns>The current language code</returns>
    string GetCurrentLocaleCode();
  }
}

