using System;
using System.Collections.Generic;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Settings.Ini;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using Sirenix.OdinInspector;
using VContainer;

namespace MToolKit.Runtime.Settings
{
  public class SettingsPlugin : DomainPlugin<SettingsSystem, ISettingsSystem>
  {
    [ShowInInspector]
    [ReadOnly]
    private SettingsSystem settingsSystem => GetService();

    /// <summary>
    ///   Required services for dependency validation.
    /// </summary>
    public override IEnumerable<Type> RequiredServices => Array.Empty<Type>();

    /// <summary>
    ///   Optional services for dependency validation.
    /// </summary>
    public override IEnumerable<Type> OptionalServices => new[] { typeof(IIniService) };

    protected override SettingsSystem CreateService(IObjectResolver resolver)
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Created SettingsSystem instance with HashCode: {0}", service?.GetHashCode() ?? 0);

      // Create the InputRebinderService instance
      InputRebinderService inputRebinderService = new();
      
      // Try to resolve INI service (optional)
      IIniService iniService = null;
      try
      {
        iniService = resolver.Resolve<IIniService>();
      }
      catch (VContainerException)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("IIniService not available, SettingsSystem will work without INI integration");
      }

      return new SettingsSystem(inputRebinderService, iniService);
    }

    /// <summary>
    ///   Gets the service, ensuring it's properly resolved for this instance.
    ///   This ensures the Inspector shows the correct service even if this instance
    ///   wasn't properly initialized during the normal flow.
    /// </summary>
    private SettingsSystem GetService()
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
          if (resolver.Resolve<ISettingsSystem>() is SettingsSystem resolvedService)
          {
            // Cache it for future use
            service = resolvedService;
            return resolvedService;
          }
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Warning(ex, "Failed to resolve SettingsSystem from container: {Message}", ex.Message);
      }

      // Fallback to null if we can't resolve
      return null;
    }
  }
}