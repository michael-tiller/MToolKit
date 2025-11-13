using System;
using System.Collections.Generic;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace MToolKit.Runtime.Core.Abstractions
{
  /// <summary>
  ///   Base class for domain plugins that manage a service with configuration.
  ///   Extends DomainPlugin with config validation and dependency checking.
  /// </summary>
  /// <typeparam name="TService">The concrete service implementation</typeparam>
  /// <typeparam name="TInterface">The service interface</typeparam>
  /// <typeparam name="TConfig">The configuration type</typeparam>
  public abstract class ConfigPlugin<TService, TInterface, TConfig> : DomainPlugin<TService, TInterface>
    where TService : class, TInterface
    where TInterface : class
    where TConfig : ScriptableObject
  {
    [SerializeField]
    [Required]
    protected TConfig config;

    /// <summary>
    ///   Required services for dependency validation. Override to specify custom dependencies.
    /// </summary>
    public override IEnumerable<Type> RequiredServices => Array.Empty<Type>();

    /// <summary>
    ///   Optional services for dependency validation. Override to specify custom dependencies.
    /// </summary>
    public override IEnumerable<Type> OptionalServices => Array.Empty<Type>();

    /// <summary>
    ///   Register the service and plugin with the container.
    ///   Includes config validation and registration.
    /// </summary>
    public override void Register(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Registering {0}", GetType().Name);

      if (config == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error(
          "{0} is not assigned! Make sure the config is assigned in the prefab.", typeof(TConfig).Name);
        return;
      }

      // Register the config instance
      builder.RegisterInstance(config).AsSelf();

      // Call base registration
      base.Register(builder);

      log.ForGameObject(gameObject).ForMethod().Debug("{0} registration completed", GetType().Name);
    }

    /// <summary>
    ///   Check if dependencies are ready. Includes config validation.
    ///   Override for custom dependency validation.
    /// </summary>
    public override bool AreDependenciesReady(IObjectResolver resolver)
    {
      bool canResolveService = resolver.TryResolve(out TInterface _);
      bool hasConfig = config != null;

      log.ForGameObject(gameObject).ForMethod().Debug(
        "Dependencies ready check: CanResolveService={0}, HasConfig={1}",
        canResolveService, hasConfig);

      return canResolveService && hasConfig;
    }

    /// <summary>
    ///   Create the service instance. Override to provide custom creation logic.
    ///   The config is available as a protected field.
    /// </summary>
    /// <param name="resolver">The object resolver for dependency injection</param>
    /// <returns>The created service instance</returns>
    protected abstract override TService CreateService(IObjectResolver resolver);
  }
}