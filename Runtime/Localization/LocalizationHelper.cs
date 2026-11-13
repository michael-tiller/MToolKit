using System;
using Cysharp.Threading.Tasks;
using Serilog;
using Serilog.Core;
using UnityEngine.Localization.Settings;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Localization
{
  public static class LocalizationHelper
  {
    /// <summary>Formats a translated or fallback template with native SmartFormat, including named dictionary arguments.</summary>
    public static string FormatSmartString(string template, params object[] arguments)
    {
      if (arguments == null || arguments.Length == 0) return template ?? string.Empty;
      return UnityEngine.Localization.SmartFormat.Smart.Format(template ?? string.Empty, arguments);
    }

    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(LocalizationHelper)).ForFeature("Localization"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public static ILocalizationService LocalizationService => GlobalLocalizationService.Instance ?? throw new Exception("Localization service not found");

    public static string GetLocalizedString(string key, params object[] args)
    {
      log.ForMethod().Verbose("Getting localized string for key: {0}", key);
      log.ForMethod().Verbose("Args: {0}", args);
      return LocalizationSettings.StringDatabase.GetLocalizedString("Default String Table", key, args);
    }
    public static async UniTask<string> GetLocalizedStringAsync(string tableName, string key, params object[] args)
    {
      log.ForMethod().Verbose("Getting localized string for key: {0}", key);
      log.ForMethod().Verbose("Args: {0}", args);
      return await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, key, args);
    }
    public static string GetLocalizedString(string tableName, string key, params object[] args)
    {
      log.ForMethod().Verbose("Getting localized string for key: {0}", key);
      log.ForMethod().Verbose("Args: {0}", args);
      return LocalizationSettings.StringDatabase.GetLocalizedString(tableName, key, args);
    }
  }
}
