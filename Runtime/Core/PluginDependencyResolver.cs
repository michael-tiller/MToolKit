using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Core
{
  /// <summary>
  ///   Resolves plugin dependency order using topological sorting.
  ///   Handles circular dependency detection and missing dependency validation.
  /// </summary>
  public class PluginDependencyResolver
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<PluginDependencyResolver>().ForFeature("Core"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    /// <summary>
    ///   Resolves the correct order for plugin registration based on service dependencies.
    ///   Only handles registration order - runtime dependencies are handled by AreDependenciesReady.
    /// </summary>
    /// <param name="plugins">The plugins to order</param>
    /// <returns>Plugins in dependency-resolved order</returns>
    /// <exception cref="CircularDependencyException">Thrown when circular dependencies are detected</exception>
    public List<AbstractGamePlugin> ResolveOrder(List<AbstractGamePlugin> plugins)
    {
      if (plugins == null || plugins.Count == 0)
      {
        log.Warning("No plugins provided for dependency resolution");
        return new List<AbstractGamePlugin>();
      }

      log.Debug("Resolving registration order for {0} plugins", plugins.Count);

      // Build dependency graph based on service dependencies
      (Dictionary<Type, List<Type>> graph, Dictionary<Type, int> inDegree, Dictionary<Type, AbstractGamePlugin> pluginTypeMap) = BuildDependencyGraph(plugins);

      // Perform topological sort
      List<AbstractGamePlugin> orderedPlugins = PerformTopologicalSort(plugins, graph, inDegree, pluginTypeMap);

      log.Debug("Registration order resolved. Order: {0}",
        string.Join(" -> ", orderedPlugins.Select(p => p.GetType().Name)));

      return orderedPlugins;
    }

    private (Dictionary<Type, List<Type>> graph, Dictionary<Type, int> inDegree, Dictionary<Type, AbstractGamePlugin> pluginTypeMap)
      BuildDependencyGraph(List<AbstractGamePlugin> plugins)
    {
      Dictionary<Type, List<Type>> graph = new();
      Dictionary<Type, int> inDegree = new();
      Dictionary<Type, AbstractGamePlugin> pluginTypeMap = new();

      // Initialize plugin type mapping
      foreach (AbstractGamePlugin plugin in plugins)
      {
        Type pluginType = plugin.GetType();
        pluginTypeMap[pluginType] = plugin;
        inDegree[pluginType] = 0;
      }

      // Build dependency graph from IDependencyDeclaration
      foreach (AbstractGamePlugin plugin in plugins)
        if (plugin is IDependencyDeclaration dep)
        {
          Type pluginType = plugin.GetType();
          log.Debug("Building dependencies for {0}:", pluginType.Name);

          foreach (Type requiredType in dep.RequiredServices)
          {
            // Skip config types - they're not service dependencies
            if (requiredType.Name.EndsWith("Config") ||
                typeof(ScriptableObject).IsAssignableFrom(requiredType))
            {
              log.Verbose("  Skipping config dependency: {0}", requiredType.Name);
              continue;
            }

            // Check if this is a service registered externally in GameInstaller
            if (IsExternallyRegisteredService(requiredType))
            {
              log.Debug("  {0} depends on {1} (externally registered service)",
                pluginType.Name, requiredType.Name);
            }
            else
            {
              // Find which plugin provides this service
              AbstractGamePlugin providerPlugin = FindServiceProvider(plugins, requiredType);
              if (providerPlugin != null)
              {
                Type providerType = providerPlugin.GetType();

                // Only add dependency edge if it doesn't create a circular dependency
                // Circular dependencies are handled by AreDependenciesReady at runtime
                if (!WouldCreateCircularDependency(providerType, pluginType, graph))
                {
                  // Add edge: providerType -> pluginType (provider must be registered before dependent)
                  if (!graph.ContainsKey(providerType))
                    graph[providerType] = new List<Type>();
                  graph[providerType].Add(pluginType);
                  inDegree[pluginType]++;

                  log.Debug("  {0} depends on {1} (provided by {2})",
                    pluginType.Name, requiredType.Name, providerType.Name);
                }
                else
                {
                  log.Debug("  {0} depends on {1} (provided by {2}) - circular dependency, handled at runtime",
                    pluginType.Name, requiredType.Name, providerType.Name);
                }
              }
              else
              {
                log.Debug("  {0} depends on {1} (external service)",
                  pluginType.Name, requiredType.Name);
              }
            }
          }
        }

      return (graph, inDegree, pluginTypeMap);
    }

    private AbstractGamePlugin FindServiceProvider(List<AbstractGamePlugin> plugins, Type serviceType)
    {
      foreach (AbstractGamePlugin plugin in plugins)
        // Check if this plugin actually provides/registers the service
        if (IsServiceProvider(plugin, serviceType))
          return plugin;
      return null;
    }

    private bool IsExternallyRegisteredService(Type serviceType)
    {
      // Services registered directly in GameInstaller (not by plugins)
      string serviceName = serviceType.Name;

      // Services registered in GameInstaller
      return serviceName == "IInventoryService" ||
             serviceName == "ITilemapService" ||
             serviceName == "IMouseOverService" ||
             serviceName == "INavigationService" ||
             serviceName == "IOneShotAudioService" ||
             serviceName == "IInventoryPanelCoordinator";
    }

    private bool WouldCreateCircularDependency(Type providerType, Type dependentType, Dictionary<Type, List<Type>> graph)
    {
      // Check if adding providerType -> dependentType would create a cycle
      // by checking if dependentType can reach providerType through existing edges

      HashSet<Type> visited = new();
      Stack<Type> stack = new();
      stack.Push(dependentType);

      while (stack.Count > 0)
      {
        Type current = stack.Pop();
        if (visited.Contains(current))
          continue;

        visited.Add(current);

        if (current == providerType)
          return true; // Found a path back to providerType

        if (graph.ContainsKey(current))
          foreach (Type neighbor in graph[current])
            if (!visited.Contains(neighbor))
              stack.Push(neighbor);
      }

      return false;
    }

    private bool IsServiceProvider(AbstractGamePlugin plugin, Type serviceType)
    {
      Type pluginType = plugin.GetType();
      Type baseType = pluginType.BaseType;

      // Check if this is a DomainPlugin<TService, TInterface> or ConfigPlugin<TService, TInterface, TConfig>
      if (baseType != null && baseType.IsGenericType)
      {
        Type genericTypeDef = baseType.GetGenericTypeDefinition();
        Type[] genericArgs = baseType.GetGenericArguments();

        // Check by name to avoid TypeLoadException with generic type definitions
        string typeName = genericTypeDef.Name;
        if ((typeName.StartsWith("DomainPlugin`") || typeName.StartsWith("ConfigPlugin`")) &&
            genericArgs.Length >= 2)
        {
          Type serviceTypeArg = genericArgs[0]; // TService (concrete type)
          Type interfaceType = genericArgs[1]; // TInterface

          // Check both the concrete service type and the interface type
          if (serviceTypeArg == serviceType || interfaceType == serviceType)
            return true;
        }
      }

      // Check for known service providers by plugin type and service name
      if (pluginType.Name == "PlayerPlugin")
        // PlayerPlugin provides IPlayerService and IRangeCalculationService
        if (serviceType.Name == "IPlayerService" || serviceType.Name == "IRangeCalculationService")
          return true;

      if (pluginType.Name == "InventoryPlugin")
        // InventoryPlugin provides IInventoryService
        if (serviceType.Name == "IInventoryService")
          return true;

      if (pluginType.Name == "TooltipPlugin")
        // TooltipPlugin provides ITooltipSystem
        if (serviceType.Name == "ITooltipSystem")
          return true;

      if (pluginType.Name == "NavigationPlugin")
        // NavigationPlugin provides INavigationService
        if (serviceType.Name == "INavigationService")
          return true;

      // Check for specific service registrations by naming convention
      return pluginType.Name.Contains(serviceType.Name.Replace("System", "").Replace("Service", ""));
    }


    private List<AbstractGamePlugin> PerformTopologicalSort(
      List<AbstractGamePlugin> plugins,
      Dictionary<Type, List<Type>> graph,
      Dictionary<Type, int> inDegree,
      Dictionary<Type, AbstractGamePlugin> pluginTypeMap)
    {
      Queue<Type> queue = new(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
      List<AbstractGamePlugin> result = new();
      int processedCount = 0;

      log.Debug("Starting topological sort with {0} plugins", plugins.Count);
      log.Debug("Initial queue (no dependencies): {0}", string.Join(", ", queue.Select(t => pluginTypeMap.TryGetValue(t, out AbstractGamePlugin p) ? p.GetType().Name : t.Name)));

      while (queue.Count > 0)
      {
        Type current = queue.Dequeue();
        processedCount++;

        if (pluginTypeMap.TryGetValue(current, out AbstractGamePlugin plugin))
        {
          result.Add(plugin);
          log.Verbose("Added plugin to resolution order: {0}", plugin.GetType().Name);
        }

        // Process dependents
        if (graph.ContainsKey(current))
          foreach (Type dependent in graph[current])
          {
            inDegree[dependent]--;
            if (inDegree[dependent] == 0)
            {
              queue.Enqueue(dependent);
              log.Verbose("Added dependent to queue: {0}", pluginTypeMap.TryGetValue(dependent, out AbstractGamePlugin dep) ? dep.GetType().Name : dependent.Name);
            }
          }
      }

      // Check for circular dependencies
      if (processedCount != plugins.Count)
      {
        int remaining = plugins.Count - processedCount;
        List<string> unprocessedPlugins = plugins.Where(p => !result.Contains(p)).Select(p => p.GetType().Name).ToList();

        log.Error("Circular dependency detected. {0} plugins could not be processed: {1}",
          remaining, string.Join(", ", unprocessedPlugins));

        // Log the dependency graph for debugging
        LogDependencyGraph(graph, pluginTypeMap);

        throw new CircularDependencyException($"Circular dependency detected. {remaining} plugins could not be processed: {string.Join(", ", unprocessedPlugins)}");
      }

      return result;
    }

    private void LogDependencyGraph(Dictionary<Type, List<Type>> graph, Dictionary<Type, AbstractGamePlugin> pluginTypeMap)
    {
      log.Debug("Dependency graph:");
      foreach (KeyValuePair<Type, List<Type>> kvp in graph)
      {
        string fromPlugin = pluginTypeMap.TryGetValue(kvp.Key, out AbstractGamePlugin from) ? from.GetType().Name : kvp.Key.Name;
        IEnumerable<string> toPlugins = kvp.Value.Select(t => pluginTypeMap.TryGetValue(t, out AbstractGamePlugin to) ? to.GetType().Name : t.Name);
        log.Debug("  {0} -> {1}", fromPlugin, string.Join(", ", toPlugins));
      }
    }
  }

  /// <summary>
  ///   Exception thrown when circular dependencies are detected in plugin resolution.
  /// </summary>
  public class CircularDependencyException : Exception
  {
    public CircularDependencyException(string message) : base(message) { }
    public CircularDependencyException(string message, Exception innerException) : base(message, innerException) { }
  }

  /// <summary>
  ///   Exception thrown when required dependencies are missing.
  /// </summary>
  public class MissingDependencyException : Exception
  {
    public List<(AbstractGamePlugin plugin, Type missingType)> MissingDependencies { get; }

    public MissingDependencyException(string message, List<(AbstractGamePlugin plugin, Type missingType)> missingDependencies)
      : base(message)
    {
      MissingDependencies = missingDependencies;
    }
  }
}