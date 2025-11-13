using System;
using System.Collections.Generic;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Installer;
using Serilog;
using Sirenix.OdinInspector;
using VContainer;

namespace MToolKit.Runtime.Input
{
  /// <summary>
  ///   Plugin for managing input rebinding functionality
  ///   Provides centralized rebinding services with conflict detection and reactive events
  /// </summary>
  public class InputRebinderPlugin : DomainPlugin<InputRebinderService, InputRebinderService>
  {
    [ShowInInspector]
    [ReadOnly]
    public InputRebinderService InputRebinderService => GetService();

    /// <summary>
    ///   Required services for dependency validation.
    /// </summary>
    public override IEnumerable<Type> RequiredServices => Array.Empty<Type>();

    /// <summary>
    ///   Optional services for dependency validation.
    /// </summary>
    public override IEnumerable<Type> OptionalServices => Array.Empty<Type>();

    protected override InputRebinderService CreateService(IObjectResolver resolver)
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Created InputRebinderService instance with HashCode: {0}", service?.GetHashCode() ?? 0);
      return new InputRebinderService();
    }

    /// <summary>
    ///   Gets the service, ensuring it's properly resolved for this instance.
    ///   This ensures the Inspector shows the correct service even if this instance
    ///   wasn't properly initialized during the normal flow.
    /// </summary>
    private InputRebinderService GetService()
    {
      // If this instance has a service, return it
      if (service != null)
        return service;

      // Try to resolve the service from the container if this instance doesn't have one
      // This can happen if the scene instance wasn't properly initialized
      try
      {
        // Find the GlobalInstaller to get access to the container
        GlobalInstaller globalInstaller = FindFirstObjectByType<GlobalInstaller>();
        if (globalInstaller != null)
        {
          // Try to resolve the service from the container
          IObjectResolver resolver = globalInstaller.Container.Resolve<IObjectResolver>();
          InputRebinderService resolvedService = resolver.Resolve<InputRebinderService>();
          if (resolvedService != null)
          {
            // Cache it for future use
            service = resolvedService;
            return resolvedService;
          }
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Warning(ex, "Failed to resolve InputRebinderService from container: {Message}", ex.Message);
      }

      // Fallback to null if we can't resolve
      return null;
    }
  }
}