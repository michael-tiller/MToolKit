using System;
using System.Collections.Generic;
using R3;

namespace MToolKit.Runtime.Localization
{
  /// <summary>
  ///   Thin DI adapter that wraps GlobalLocalizationService.Instance for dependency injection.
  ///   This allows almost all runtime code to stay DI-pure while only bootstrapping/UI glue
  ///   touches the singleton through LocalizationHelper.
  /// </summary>
  public class LocalizationServiceAdapter : ILocalizationService
  {
    private readonly Lazy<GlobalLocalizationService> _serviceInstance;

    public LocalizationServiceAdapter()
    {
      _serviceInstance = new Lazy<GlobalLocalizationService>(() => GlobalLocalizationService.Instance);
    }

    private GlobalLocalizationService Service => _serviceInstance.Value
      ?? throw new InvalidOperationException("GlobalLocalizationService is not initialized");

    public bool IsInitialized => Service.IsInitialized;

    public Observable<string> Language => Service.Language;

    public void InitializeLocalization()
    {
      Service.InitializeLocalization();
    }

    public bool SetNewLocale(string languageCode)
    {
      return Service.SetNewLocale(languageCode);
    }

    public List<string> GetAvailableLocaleCodes()
    {
      return Service.GetAvailableLocaleCodes();
    }

    public string GetCurrentLocaleCode()
    {
      return Service.GetCurrentLocaleCode();
    }
  }
}

