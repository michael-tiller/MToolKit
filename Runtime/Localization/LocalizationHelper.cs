using System;
using Serilog;
using ILogger = Serilog.ILogger;
using UnityEngine.Localization.Settings;

namespace MToolKit.Runtime.Localization
{
  public static class LocalizationHelper
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(LocalizationHelper)).ForFeature("Localization"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;


    public static string GetLocalizedString(string key, params object[] args)
    {
      log.ForMethod().Debug("Getting localized string for key: {0}", key);
      log.ForMethod().Verbose("Args: {0}", args);
      return LocalizationSettings.StringDatabase.GetLocalizedString("Default String Table", key, args);
    }

  }
}