using System;
using System.Collections.Generic;
using MToolKit.Runtime.Core.Interfaces;
using VContainer;

namespace MToolKit.Runtime.Core
{
  /// <summary>
  /// The plugin registry for managing game plugins and runtime plugins.
  /// </summary>
  public sealed class PluginRegistry
  {
    /// <summary>
    /// The list of game plugins.
    /// </summary>
    private readonly List<IGamePlugin> gamePlugins = new();
    /// <summary>
    /// The list of runtime plugins.
    /// </summary>
    private readonly List<IRuntimePlugin> runtimePlugins = new();
    /// <summary>
    /// The lock object for the plugin registry.
    /// </summary>
    private readonly object _lockObject = new object();

    /// <summary>
    /// Phase 1: Register all plugin dependencies with the container builder.
    /// This method handles both IGamePlugin and IRuntimePlugin registration.
    /// </summary>
    /// <param name="builder">The container builder for registering dependencies.</param>
    public void Register(IGamePlugin plugin)
    {
      lock (_lockObject)
      {
        gamePlugins.Add(plugin);

        if (plugin is IRuntimePlugin runtime)
          runtimePlugins.Add(runtime);
      }
    }

    /// <summary>
    /// Apply all plugin dependencies with the container builder.
    /// </summary>
    /// <param name="builder">The container builder for registering dependencies.</param>
    public void ApplyAll(IContainerBuilder builder)
    {
      List<IGamePlugin> pluginsCopy;
      lock (_lockObject)
      {
        pluginsCopy = new List<IGamePlugin>(gamePlugins);
      }
      
      foreach (IGamePlugin plugin in pluginsCopy)
        plugin.Register(builder);
    }

    /// <summary>
    /// Get all game plugins.
    /// </summary>
    /// <returns>The list of game plugins.</returns>
    public IEnumerable<IGamePlugin> GetGamePlugins()
    {
      lock (_lockObject)
      {
        return new List<IGamePlugin>(gamePlugins);
      }
    }

    /// <summary>
    /// Get all runtime plugins.
    /// </summary>
    /// <returns>The list of runtime plugins.</returns>
    public IEnumerable<IRuntimePlugin> GetRuntimePlugins()
    {
      lock (_lockObject)
      {
        return new List<IRuntimePlugin>(runtimePlugins);
      }
    }

    /// <summary>
    /// Initialize a runtime plugin.
    /// </summary>
    /// <param name="plugin">The runtime plugin to initialize.</param>
    /// <param name="resolver">The object resolver.</param>
    public void InitializeRuntimePlugin(IRuntimePlugin plugin, IObjectResolver resolver)
    {
      if (plugin == null)
        throw new ArgumentNullException(nameof(plugin));

      lock (_lockObject)
      {
        if (!runtimePlugins.Contains(plugin))
          runtimePlugins.Add(plugin);
      }

      plugin.Initialize(resolver);
    }

    /// <summary>
    /// Phase 2: Perform setup for all runtime plugins.
    /// This phase handles subscriptions and early initialization where dependencies may not be ready.
    /// Validates dependencies for plugins implementing IDependencyDeclaration.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    /// <returns>The list of runtime plugins.</returns>
    public void PerformPluginSetup(IObjectResolver resolver)
    {
      List<IRuntimePlugin> pluginsCopy;
      lock (_lockObject)
      {
        pluginsCopy = new List<IRuntimePlugin>(runtimePlugins);
      }
      
      foreach (IRuntimePlugin plugin in pluginsCopy)
      {
        // Validate dependencies before setup
        if (!plugin.ValidateDependencies(resolver))
        {
          var pluginName = plugin.GetType().Name;
          var missingDependencies = GetMissingDependencies(plugin, resolver);
          throw new InvalidOperationException(
            $"Plugin {pluginName} is missing required dependencies: {string.Join(", ", missingDependencies)}");
        }
        
        plugin.PerformSetup(resolver);
      }
    }

    /// <summary>
    /// Phase 3: Perform runtime initialization for plugins with ready dependencies.
    /// This phase handles full initialization when all dependencies are guaranteed to be available.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    public void PerformPluginRuntimeInitialization(IObjectResolver resolver)
    {
      List<IRuntimePlugin> pluginsCopy;
      lock (_lockObject)
      {
        pluginsCopy = new List<IRuntimePlugin>(runtimePlugins);
      }
      
      foreach (IRuntimePlugin plugin in pluginsCopy)
      {
        var pluginName = plugin.GetType().Name;
        
        if (plugin.AreDependenciesReady(resolver))
        {
          try
          {
            plugin.PerformRuntimeInitialization(resolver);
            Serilog.Log.Logger.ForContext<PluginRegistry>().Debug("Plugin {PluginName} runtime initialization completed successfully", pluginName);
          }
          catch (Exception ex)
          {
            Serilog.Log.Logger.ForContext<PluginRegistry>().Error(ex, "Plugin {PluginName} runtime initialization failed: {Message}", pluginName, ex.Message);
          }
        }
        else
        {
          var missingDeps = GetMissingDependencies(plugin, resolver);
          var missingDepsText = missingDeps.Count > 0 ? string.Join(", ", missingDeps) : "unknown dependencies";
          Serilog.Log.Logger.ForContext<PluginRegistry>().Error("Plugin {PluginName} dependencies not ready, skipping runtime initialization. Missing: {MissingDependencies}", 
            pluginName, missingDepsText);
        }
      }
    }

    /// <summary>
    /// Gets the list of missing dependencies for a plugin.
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <param name="resolver">The object resolver.</param>
    /// <returns>List of missing dependency type names.</returns>
    private static List<string> GetMissingDependencies(IRuntimePlugin plugin, IObjectResolver resolver)
    {
      var missingDependencies = new List<string>();
      
      if (plugin is IDependencyDeclaration dependencyDeclaration)
      {
        foreach (var serviceType in dependencyDeclaration.RequiredServices)
        {
          if (!resolver.TryResolve(serviceType, out _))
          {
            missingDependencies.Add(serviceType.Name);
          }
        }
      }
      
      return missingDependencies;
    }
  }
}