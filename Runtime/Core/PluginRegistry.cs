using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;


namespace MToolKit.Runtime.Core
{
  
  /// <summary>
  ///   The plugin registry for managing game plugins and runtime plugins.
  /// </summary>
  public sealed class PluginRegistry
  {
  
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<PluginRegistry>().ForFeature("MToolKit.Core"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    
    /// <summary>
    ///   The list of game plugins.
    /// </summary>
    private readonly List<IGamePlugin> gamePlugins = new();

    /// <summary>
    ///   The list of runtime plugins.
    /// </summary>
    private readonly List<IRuntimePlugin> runtimePlugins = new();

    /// <summary>
    ///   The lock object for the plugin registry.
    /// </summary>
    private readonly object _lockObject = new();

    /// <summary>
    ///   Phase 1: Register all plugin dependencies with the container builder.
    ///   This method handles both IGamePlugin and IRuntimePlugin registration.
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
    ///   Apply all plugin dependencies with the container builder.
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
    ///   Get all game plugins.
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
    ///   Get all runtime plugins.
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
    ///   Initialize a runtime plugin.
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
    ///   Phase 2: Perform setup for all runtime plugins.
    ///   This phase handles subscriptions and early initialization where dependencies may not be ready.
    ///   Validates dependencies for plugins implementing IDependencyDeclaration.
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
          string pluginName = plugin.GetType().Name;
          List<string> missingDependencies = GetMissingDependencies(plugin, resolver);
          throw new InvalidOperationException(
            $"Plugin {pluginName} is missing required dependencies: {string.Join(", ", missingDependencies)}");
        }

        using (StartupProfiling.Phase($"Plugin.{plugin.GetType().Name}.Setup"))
          plugin.PerformSetup(resolver);
      }
    }

    /// <summary>
    ///   Phase 3: Perform runtime initialization for plugins with ready dependencies.
    ///   This phase handles full initialization when all dependencies are guaranteed to be available.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    public void PerformPluginRuntimeInitialization(IObjectResolver resolver)
    {
      List<IRuntimePlugin> pluginsCopy;
      lock (_lockObject)
      {
        pluginsCopy = new List<IRuntimePlugin>(runtimePlugins);
      }

      int initialized = 0;
      foreach (IRuntimePlugin plugin in pluginsCopy)
      {
        string pluginName = plugin.GetType().Name;

        if (plugin.AreDependenciesReady(resolver))
        {
          try
          {
            using (StartupProfiling.Phase($"Plugin.{pluginName}.RuntimeInit"))
              plugin.PerformRuntimeInitialization(resolver);
            log.Verbose("Plugin {PluginName} runtime initialization completed successfully", pluginName);
            initialized++;
          }
          catch (Exception ex)
          {
            log.Error(ex, "Plugin {PluginName} runtime initialization failed: {Message}", pluginName, ex.Message);
          }
        }
        else
        {
          List<string> missingDeps = GetMissingDependencies(plugin, resolver);
          string missingDepsText = missingDeps.Count > 0 ? string.Join(", ", missingDeps) : "unknown dependencies";
          log.Error("Plugin {PluginName} dependencies not ready, skipping runtime initialization. Missing: {MissingDependencies}",
            pluginName, missingDepsText);
        }
      }
      log.Debug("Runtime-initialized {Count}/{Total} plugins", initialized, pluginsCopy.Count);
    }

    /// <summary>
    ///   Async Phase 3: awaits each plugin's <see cref="IRuntimePlugin.PerformRuntimeInitializationAsync"/>
    ///   sequentially. Use this when at least one plugin needs its async runtime work
    ///   to complete before subsequent boot steps (e.g. a scene transition) — fire-and-forget
    ///   from the sync variant races scene transitions and can get cancelled mid-flight.
    /// </summary>
    public async UniTask PerformPluginRuntimeInitializationAsync(IObjectResolver resolver, CancellationToken ct = default)
    {
      List<IRuntimePlugin> pluginsCopy;
      lock (_lockObject)
      {
        pluginsCopy = new List<IRuntimePlugin>(runtimePlugins);
      }

      int initialized = 0;
      foreach (IRuntimePlugin plugin in pluginsCopy)
      {
        string pluginName = plugin.GetType().Name;

        if (plugin.AreDependenciesReady(resolver))
        {
          try
          {
            using (StartupProfiling.Phase($"Plugin.{pluginName}.RuntimeInit"))
              await plugin.PerformRuntimeInitializationAsync(resolver, ct);
            log.Verbose("Plugin {PluginName} async runtime initialization completed successfully", pluginName);
            initialized++;
          }
          catch (Exception ex)
          {
            log.Error(ex, "Plugin {PluginName} async runtime initialization failed: {Message}", pluginName, ex.Message);
          }
        }
        else
        {
          List<string> missingDeps = GetMissingDependencies(plugin, resolver);
          string missingDepsText = missingDeps.Count > 0 ? string.Join(", ", missingDeps) : "unknown dependencies";
          log.Error("Plugin {PluginName} dependencies not ready, skipping async runtime initialization. Missing: {MissingDependencies}",
            pluginName, missingDepsText);
        }
      }
      log.Debug("Async runtime-initialized {Count}/{Total} plugins", initialized, pluginsCopy.Count);
    }

    /// <summary>
    ///   Gets the list of missing dependencies for a plugin.
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <param name="resolver">The object resolver.</param>
    /// <returns>List of missing dependency type names.</returns>
    private static List<string> GetMissingDependencies(IRuntimePlugin plugin, IObjectResolver resolver)
    {
      List<string> missingDependencies = new();

      if (plugin is IDependencyDeclaration dependencyDeclaration)
        foreach (Type serviceType in dependencyDeclaration.RequiredServices)
          if (!resolver.TryResolve(serviceType, out _))
            missingDependencies.Add(serviceType.Name);

      return missingDependencies;
    }
  }
}