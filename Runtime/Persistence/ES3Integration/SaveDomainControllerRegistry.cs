using System;
using System.Collections.Generic;
using MToolKit.Runtime.Persistence.Interfaces;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   Registry for save domain controllers that can be populated by plugins
    /// </summary>
    public class SaveDomainControllerRegistry
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SaveDomainControllerRegistry>().ForFeature("Persistence.ES3"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private readonly List<ISaveDomainController> controllers = new();

    public int Count => controllers.Count;

    public void RegisterController(ISaveDomainController controller)
    {
      if (controller != null && !controllers.Contains(controller))
      {
        controllers.Add(controller);
        log.ForMethod().Information("Registered controller for domain {0}", controller.Domain);
      }
    }

    public IEnumerable<ISaveDomainController> GetControllers()
    {
      return controllers.AsReadOnly();
    }

    public void Clear()
    {
      controllers.Clear();
      log.ForMethod().Information("Cleared all save domain controllers");
    }
  }
}