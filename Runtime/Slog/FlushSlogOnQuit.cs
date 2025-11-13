using System;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Slog
{
  public class FlushSlogOnQuit : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<FlushSlogOnQuit>().ForFeature("Slog"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    /// <summary>
    ///   Persist until the end of app.
    /// </summary>
    private void Awake()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Installed");
      DontDestroyOnLoad(this);
      gameObject.name = nameof(FlushSlogOnQuit);
      gameObject.hideFlags = HideFlags.HideInHierarchy;
    }

    /// <summary>
    ///   Flushes the Serilog object
    /// </summary>
    private void OnApplicationQuit()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Flushing Slog on quit");
      Log.CloseAndFlush();
    }
  }
}