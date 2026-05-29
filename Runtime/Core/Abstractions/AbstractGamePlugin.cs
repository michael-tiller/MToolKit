using System;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Core.Abstractions
{
  public abstract class AbstractGamePlugin : MonoBehaviour, IGamePlugin
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AbstractGamePlugin>().ForFeature("Core.Abstractions"));
    protected static ILogger log => logLazy.Value ?? Logger.None;

    protected bool isShutdown;
    protected bool isStarted;

    public bool IsStarted => isStarted;
    public bool IsShutdown => isShutdown;

    /// <summary>
    ///   Virtual Start method with lifecycle guard. Override in derived classes if needed.
    /// </summary>
    public virtual void Start()
    {
      if (isStarted)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("{0} already started, skipping.", GetType().Name);
        return;
      }

      // Check if the object is still valid before accessing gameObject
      if (this == null || gameObject == null)
        throw new MissingReferenceException($"GameObject for {GetType().Name} has been destroyed");

      log.ForGameObject(gameObject).ForMethod().Verbose("{0} plugin started", GetType().Name);
      isStarted = true;
    }

    public virtual void Register(IContainerBuilder builder) { }

    /// <summary>
    ///   Virtual Shutdown method with lifecycle guard. Override in derived classes if needed.
    /// </summary>
    public virtual void Shutdown()
    {
      // Always set isStarted to false, even if already shutdown
      isStarted = false;

      if (isShutdown)
      {
        log.ForMethod().Verbose("{0} already shut down, skipping.", GetType().Name);
        return;
      }

      // Check if the object is still valid before accessing gameObject
      if (this == null || gameObject == null)
      {
        log.ForMethod().Warning("{0} is null or destroyed during shutdown, skipping.", GetType().Name);
        return;
      }

      isShutdown = true;
      log.ForGameObject(gameObject).ForMethod().Verbose("{0} plugin shutdown", GetType().Name);
    }
  }
}