using System;
using System.Collections.Generic;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using Serilog.Core;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Core.Abstractions
{
  /// <summary>
  ///   Base class for domain plugins that manage a single service with sensible defaults.
  ///   Reduces boilerplate by providing convention-based implementations for common patterns.
  /// </summary>
  /// <typeparam name="TService">The concrete service implementation</typeparam>
  /// <typeparam name="TInterface">The service interface</typeparam>
  public abstract class DomainPlugin<TService, TInterface> : AbstractRuntimePlugin, IDependencyDeclaration
    where TService : class, TInterface
    where TInterface : class
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<DomainPlugin<TService, TInterface>>().ForFeature("Core.Abstractions"));
    protected new static ILogger log => logLazy.Value ?? Logger.None;

    protected TService service;

    /// <summary>
    ///   Required services for dependency validation. Override to specify custom dependencies.
    /// </summary>
    public virtual IEnumerable<Type> RequiredServices => new[]
    {
      typeof(TInterface)
    };

    /// <summary>
    ///   Optional services for dependency validation. Override to specify custom dependencies.
    /// </summary>
    public virtual IEnumerable<Type> OptionalServices => new[]
    {
      typeof(ILogger)
    };

    /// <summary>
    ///   Register the service and plugin with the container.
    ///   Override CreateService to provide custom service creation logic.
    /// </summary>
    public override void Register(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Registering {0}", GetType().Name);

      // Register the service as singleton
      builder.Register<TInterface>(CreateService, Lifetime.Singleton);

      // Register the plugin instance
      builder.RegisterInstance(this).AsSelf();

      log.ForGameObject(gameObject).ForMethod().Verbose("{0} registration completed", GetType().Name);
    }

    /// <summary>
    ///   Create the service instance. Override to provide custom creation logic.
    /// </summary>
    /// <param name="resolver">The object resolver for dependency injection</param>
    /// <returns>The created service instance</returns>
    protected abstract TService CreateService(IObjectResolver resolver);

    /// <summary>
    ///   Default setup implementation. Override for custom setup logic.
    /// </summary>
    public override void PerformSetup(IObjectResolver resolver)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("{0} setup phase completed", GetType().Name);
    }

    /// <summary>
    ///   Check if dependencies are ready. Default implementation checks if service can be resolved.
    ///   Override for custom dependency validation.
    /// </summary>
    public override bool AreDependenciesReady(IObjectResolver resolver)
    {
      bool canResolveService = resolver.TryResolve(out TInterface _);

      log.ForGameObject(gameObject).ForMethod().Verbose(
        "Dependencies ready check: CanResolveService={0}", canResolveService);

      return canResolveService;
    }

    /// <summary>
    ///   Default runtime initialization. Resolves service and calls Initialize() if available.
    ///   Override for custom initialization logic.
    /// </summary>
    public override void PerformRuntimeInitialization(IObjectResolver resolver)
    {
      if (isRuntimeInitialized)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose(
          "{Type} already runtime initialized, skipping", GetType().Name);
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Verbose(
        "Performing {Type} runtime initialization...", GetType().Name);

      try
      {
        // Resolve the service
        service = resolver.Resolve<TInterface>() as TService;

        if (service == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error(
            "Failed to resolve {Service}", typeof(TService).Name);
          return;
        }

        // Service is resolved and ready
        log.ForGameObject(gameObject).ForMethod().Verbose(
          "{Service} resolved successfully", typeof(TService).Name);

        isRuntimeInitialized = true;
        log.ForGameObject(gameObject).ForMethod().Verbose(
          "{Type} runtime initialization completed", GetType().Name);
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(
          ex, "Error during {Type} runtime initialization", GetType().Name);
      }
    }

    /// <summary>
    ///   Default shutdown implementation. Calls Dispose() if service implements IDisposable.
    ///   Override for custom cleanup logic.
    /// </summary>
    public override void Shutdown()
    {
      base.Shutdown();

      if (service is IDisposable disposable)
      {
        disposable.Dispose();
        log.ForGameObject(gameObject).ForMethod().Debug(
          "{0} disposed", typeof(TService).Name);
      }

      service = null;
      isRuntimeInitialized = false;
    }
  }
}