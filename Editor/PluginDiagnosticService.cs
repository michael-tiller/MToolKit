#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Core.Abstractions;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Editor
{
    /// <summary>
    /// Service for gathering comprehensive diagnostic information about runtime plugins.
    /// Provides detailed analysis of plugin states, dependencies, and runtime behavior.
    /// </summary>
    public static class PluginDiagnosticService
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(PluginDiagnosticService)).ForFeature("Editor"));
        private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        /// <summary>
        /// Gathers comprehensive diagnostic information about all plugins in the system.
        /// </summary>
        /// <param name="installerType">Type of installer to gather plugins from ("GlobalInstaller", "GameInstaller", or "All")</param>
        /// <returns>List of plugin state models with detailed diagnostic information.</returns>
        public static List<PluginStateModel> GatherPluginDiagnostics(string installerType = "All")
        {
            var diagnostics = new List<PluginStateModel>();

            try
            {
                if (installerType == "All" || installerType == "GlobalInstaller")
                {
                    GatherInstallerPlugins("GlobalInstaller", diagnostics);
                }

                if (installerType == "All" || installerType == "GameInstaller")
                {
                    GatherInstallerPlugins("GameInstaller", diagnostics);
                }

                log.Verbose("Gathered diagnostics for {PluginCount} plugins from {InstallerType}", diagnostics.Count, installerType);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error gathering plugin diagnostics: {Message}", ex.Message);
            }

            return diagnostics;
        }

        /// <summary>
        /// Gathers plugins from a specific installer.
        /// </summary>
        /// <param name="installerName">Name of the installer ("GlobalInstaller" or "GameInstaller")</param>
        /// <param name="diagnostics">List to add diagnostics to</param>
        private static void GatherInstallerPlugins(string installerName, List<PluginStateModel> diagnostics)
        {
            try
            {
                object installer = null;

                if (installerName == "GlobalInstaller")
                {
                    installer = FindGlobalInstaller();
                }
                else if (installerName == "GameInstaller")
                {
                    installer = FindGameInstaller();
                }

                if (installer == null)
                {
                    log.Debug("No {InstallerName} found", installerName);
                    return;
                }

                // Gather plugins based on installer type
                if (installerName == "GlobalInstaller")
                {
                    GatherGlobalInstallerPluginsViaReflection(installer, diagnostics);
                }
                else if (installerName == "GameInstaller")
                {
                    GatherGameInstallerPluginsViaReflection(installer, diagnostics);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error gathering plugins from {InstallerName}: {Message}", installerName, ex.Message);
            }
        }

        /// <summary>
        /// Finds the GlobalInstaller using flexible detection.
        /// </summary>
        private static object FindGlobalInstaller()
        {
            // Try reflection approach first (namespace-agnostic)
            var installerType = FindTypeByName("GlobalInstaller");
            if (installerType != null)
            {
                var instanceProperty = installerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var installer = instanceProperty.GetValue(null);
                    if (installer != null)
                    {
                        log.Debug("Found GlobalInstaller via reflection: {Found}", installer != null);
                        return installer;
                    }
                }
            }

            // Try scene search - look for LifetimeScope components that might be GlobalInstaller
            var installerObjects = UnityEngine.Object.FindObjectsByType(typeof(MonoBehaviour), FindObjectsSortMode.None)
                .Where(mb => mb.GetType().Name == "GlobalInstaller")
                .ToArray();
            
            if (installerObjects.Length > 0)
            {
                log.Debug("Found GlobalInstaller in scene: {Found}", installerObjects.Length > 0);
                return installerObjects[0];
            }

            return null;
        }

        /// <summary>
        /// Finds the GameInstaller using flexible detection.
        /// </summary>
        private static object FindGameInstaller()
        {
            // First, find the GlobalInstaller
            var globalInstaller = FindGlobalInstaller();
            if (globalInstaller == null)
            {
                log.Debug("No GlobalInstaller found, cannot find GameInstaller via parent relationship");
                return null;
            }

            // Try to find GameInstaller as a child of GlobalInstaller
            try
            {
                var globalInstallerType = globalInstaller.GetType();
                
                // Look for Container property to access VContainer hierarchy
                var containerProperty = globalInstallerType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);
                if (containerProperty != null)
                {
                    var container = containerProperty.GetValue(globalInstaller);
                    if (container != null)
                    {
                        // Try to find child containers
                        var childContainersProperty = container.GetType().GetProperty("Children", BindingFlags.Public | BindingFlags.Instance);
                        if (childContainersProperty != null)
                        {
                            var childContainers = childContainersProperty.GetValue(container);
                            if (childContainers is System.Collections.IEnumerable enumerable)
                            {
                                foreach (var childContainer in enumerable)
                                {
                                    // Look for LifetimeScope components in child containers
                                    var lifetimeScopeProperty = childContainer.GetType().GetProperty("LifetimeScope", BindingFlags.Public | BindingFlags.Instance);
                                    if (lifetimeScopeProperty != null)
                                    {
                                        var lifetimeScope = lifetimeScopeProperty.GetValue(childContainer);
                                        if (lifetimeScope != null && lifetimeScope != globalInstaller)
                                        {
                                            log.Debug("Found GameInstaller as child of GlobalInstaller: {TypeName}", lifetimeScope.GetType().Name);
                                            return lifetimeScope;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug("Error finding GameInstaller via parent relationship: {Message}", ex.Message);
            }

            // Fallback: Try reflection approach - look for any LifetimeScope with Instance property that's not GlobalInstaller
            var lifetimeScopeTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => 
                {
                    try { return assembly.GetTypes(); }
                    catch { return new Type[0]; }
                })
                .Where(type => 
                {
                    // Look for LifetimeScope subclasses that have an Instance property
                    return IsLifetimeScope(type) &&
                           type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static) != null &&
                           !type.Name.Contains("Global"); // Exclude GlobalInstaller
                })
                .ToArray();

            foreach (var installerType in lifetimeScopeTypes)
            {
                var instanceProperty = installerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var installer = instanceProperty.GetValue(null);
                    if (installer != null)
                    {
                        log.Debug("Found GameInstaller via reflection fallback: {TypeName}", installerType.Name);
                        return installer;
                    }
                }
            }

            // Final fallback: Try scene search - look for LifetimeScope components that aren't GlobalInstaller
            var installerObjects = UnityEngine.Object.FindObjectsByType(typeof(MonoBehaviour), FindObjectsSortMode.None)
                .Where(mb => 
                {
                    var type = mb.GetType();
                    return IsLifetimeScope(type) &&
                           !type.Name.Contains("Global"); // Exclude GlobalInstaller
                })
                .ToArray();
            
            if (installerObjects.Length > 0)
            {
                log.Debug("Found GameInstaller in scene fallback: {TypeName}", installerObjects[0].GetType().Name);
                return installerObjects[0];
            }

            return null;
        }

        /// <summary>
        /// Creates detailed diagnostic information for a specific plugin.
        /// </summary>
        /// <param name="plugin">The plugin to analyze.</param>
        /// <param name="installer">The installer containing the plugin.</param>
        /// <returns>Detailed plugin state model.</returns>
        private static PluginStateModel CreateDetailedPluginDiagnostic(object plugin, object installer)
        {
            var model = new PluginStateModel
            {
                Name = plugin.GetType().Name,
                TypeName = plugin.GetType().FullName,
                GameObject = plugin is MonoBehaviour mb ? mb.gameObject : null,
                Status = DetermineDetailedPluginStatus(plugin),
                CurrentPhase = DetermineCurrentPhase(plugin),
                IsRuntimeSystem = plugin is IRuntimeSystem,
                IsRunning = IsRuntimeSystemRunning(plugin as IRuntimeSystem),
                Dependencies = GetBasicPluginDependencies(plugin, installer),
                ErrorMessage = GetPluginErrorMessage(plugin),
                InitializationTime = GetPluginInitializationTime(plugin),
                PerformanceMetrics = GetPluginPerformanceMetrics(plugin)
            };

            // Add plugin type information to the name for better identification
            var pluginTypes = new List<string>();
            if (plugin is IGamePlugin) pluginTypes.Add("GamePlugin");
            if (plugin is IRuntimePlugin) pluginTypes.Add("RuntimePlugin");
            if (plugin is IRuntimeSystem) pluginTypes.Add("RuntimeSystem");
            if (plugin is IDependencyDeclaration) pluginTypes.Add("DependencyDeclaration");
            
            if (pluginTypes.Count > 0)
            {
                model.Name = $"{model.Name} ({string.Join(", ", pluginTypes)})";
            }

            return model;
        }

        /// <summary>
        /// Determines the detailed status of a plugin based on multiple factors.
        /// </summary>
        private static EPluginStatus DetermineDetailedPluginStatus(object plugin)
        {
            if (plugin == null) return EPluginStatus.Unknown;

            try
            {
                // Check if plugin GameObject is active
                if (plugin is MonoBehaviour mb && !mb.gameObject.activeInHierarchy)
                {
                    return EPluginStatus.Stopped;
                }

                // Check runtime system status
                if (plugin is IRuntimeSystem runtimeSystem)
                {
                    return IsRuntimeSystemRunning(runtimeSystem) ? EPluginStatus.Active : EPluginStatus.Stopped;
                }

                // Check for initialization errors
                if (HasInitializationError(plugin))
                {
                    return EPluginStatus.Error;
                }

                // Check if plugin is in initialization phase
                if (IsInInitializationPhase(plugin))
                {
                    return EPluginStatus.Initializing;
                }

                // Check IGamePlugin lifecycle state
                if (plugin is IGamePlugin gamePlugin)
                {
                    // Use reflection to check IsStarted and IsShutdown properties
                    var isStartedProperty = plugin.GetType().GetProperty("IsStarted", BindingFlags.Public | BindingFlags.Instance);
                    var isShutdownProperty = plugin.GetType().GetProperty("IsShutdown", BindingFlags.Public | BindingFlags.Instance);
                    
                    if (isShutdownProperty != null && (bool)isShutdownProperty.GetValue(plugin))
                    {
                        return EPluginStatus.Stopped;
                    }
                    
                    if (isStartedProperty != null && (bool)isStartedProperty.GetValue(plugin))
                    {
                        return EPluginStatus.Active;
                    }
                    
                    return EPluginStatus.Initializing; // Registered but not started yet
                }

                return EPluginStatus.Active;
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error determining plugin status for {PluginName}: {Message}", plugin.GetType().Name, ex.Message);
                return EPluginStatus.Error;
            }
        }

        /// <summary>
        /// Determines the current initialization phase of a plugin.
        /// </summary>
        private static string DetermineCurrentPhase(object plugin)
        {
            if (plugin is IRuntimePlugin runtimePlugin)
            {
                // Check if plugin has been through all phases
                if (HasCompletedSetup(runtimePlugin))
                {
                    if (HasCompletedRuntimeInitialization(runtimePlugin))
                    {
                        return "Runtime Initialized";
                    }
                    return "Setup Complete";
                }
                return "Registration";
            }
            
            if (plugin is IGamePlugin gamePlugin)
            {
                // Check if it's also a MonoBehaviour with lifecycle state
                if (plugin is MonoBehaviour mb)
                {
                    // Use reflection to check IsStarted and IsShutdown properties
                    var isStartedProperty = plugin.GetType().GetProperty("IsStarted", BindingFlags.Public | BindingFlags.Instance);
                    var isShutdownProperty = plugin.GetType().GetProperty("IsShutdown", BindingFlags.Public | BindingFlags.Instance);
                    
                    if (isShutdownProperty != null && (bool)isShutdownProperty.GetValue(plugin))
                    {
                        return "Shutdown";
                    }
                    
                    if (isStartedProperty != null && (bool)isStartedProperty.GetValue(plugin))
                    {
                        return "Started";
                    }
                    
                    return "Registered";
                }
                
                return "Registered";
            }

            return "Unknown";
        }

        /// <summary>
        /// Checks if a runtime system is currently running.
        /// </summary>
        private static bool IsRuntimeSystemRunning(IRuntimeSystem runtimeSystem)
        {
            if (runtimeSystem == null) return false;

            try
            {
                // Check if the GameObject is active
                if (runtimeSystem is MonoBehaviour mb)
                {
                    return mb.gameObject.activeInHierarchy && mb.enabled;
                }

                // For non-MonoBehaviour runtime systems, assume they're running
                return true;
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error checking runtime system status: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets basic dependency information for a plugin.
        /// </summary>
        private static List<DependencyModel> GetBasicPluginDependencies(object plugin, object installer)
        {
            var dependencies = new List<DependencyModel>();

            try
            {
                if (plugin is IDependencyDeclaration dependencyDeclaration)
                {
                    foreach (var serviceType in dependencyDeclaration.RequiredServices)
                    {
                        var dependency = new DependencyModel
                        {
                            ServiceName = serviceType.Name,
                            ServiceType = serviceType,
                            IsResolved = CheckServiceResolution(serviceType, installer),
                            ResolutionError = null
                        };

                        if (!dependency.IsResolved)
                        {
                            dependency.ResolutionError = "Service not resolved";
                        }

                        dependencies.Add(dependency);
                    }
                }

                // Check for optional dependencies
                if (plugin is IDependencyDeclaration optionalDeclaration)
                {
                    foreach (var serviceType in optionalDeclaration.OptionalServices)
                    {
                        var dependency = new DependencyModel
                        {
                            ServiceName = $"{serviceType.Name} (Optional)",
                            ServiceType = serviceType,
                            IsResolved = CheckServiceResolution(serviceType, installer),
                            IsOptional = true
                        };

                        if (!dependency.IsResolved)
                        {
                            dependency.ResolutionError = "Optional service not resolved";
                        }

                        dependencies.Add(dependency);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error gathering dependencies for {PluginName}: {Message}", plugin.GetType().Name, ex.Message);
            }

            return dependencies;
        }

        /// <summary>
        /// Checks if a service type can be resolved from the installer's container.
        /// </summary>
        private static bool CheckServiceResolution(Type serviceType, object installer)
        {
            try
            {
                // Try to get the container from the installer
                var installerType = installer.GetType();
                
                // Look for Container property (VContainer LifetimeScope has this)
                var containerProperty = installerType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);
                if (containerProperty != null)
                {
                    var container = containerProperty.GetValue(installer);
                    if (container != null)
                    {
                        // Try to resolve the service
                        var tryResolveMethod = container.GetType().GetMethod("TryResolve", new[] { typeof(Type), typeof(object).MakeByRefType() });
                        if (tryResolveMethod != null)
                        {
                            var parameters = new object[] { serviceType, null };
                            var result = (bool)tryResolveMethod.Invoke(container, parameters);
                            return result;
                        }
                    }
                }

                // Fallback: try to find the service in the scene
                var serviceObjects = UnityEngine.Object.FindObjectsByType(typeof(MonoBehaviour), FindObjectsSortMode.None)
                    .Where(mb => serviceType.IsAssignableFrom(mb.GetType()))
                    .ToArray();
                
                return serviceObjects.Length > 0;
            }
            catch (Exception ex)
            {
                log.Debug("Error checking service resolution for {ServiceType}: {Message}", serviceType.Name, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets error message for a plugin if any exists.
        /// </summary>
        private static string GetPluginErrorMessage(object plugin)
        {
            try
            {
                // Check for common error patterns
                if (plugin is MonoBehaviour mb)
                {
                    if (!mb.gameObject.activeInHierarchy)
                    {
                        return "GameObject is inactive";
                    }

                    if (!mb.enabled)
                    {
                        return "Component is disabled";
                    }
                }

                // Check for initialization errors in runtime plugins
                if (plugin is IRuntimePlugin runtimePlugin)
                {
                    if (HasInitializationError(runtimePlugin))
                    {
                        return "Initialization failed";
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                return $"Error checking plugin: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the initialization time of a plugin.
        /// </summary>
        private static DateTime GetPluginInitializationTime(object plugin)
        {
            // This would need to be tracked in the plugin implementation
            // For now, return a placeholder
            return DateTime.Now;
        }

        /// <summary>
        /// Gets performance metrics for a plugin.
        /// </summary>
        private static PluginPerformanceMetrics GetPluginPerformanceMetrics(object plugin)
        {
            return new PluginPerformanceMetrics
            {
                UpdateCallsPerSecond = 0, // Would need to be tracked
                AverageUpdateTime = 0f,  // Would need to be tracked
                LastUpdateTime = DateTime.Now
            };
        }

        /// <summary>
        /// Checks if a plugin has completed the setup phase.
        /// </summary>
        private static bool HasCompletedSetup(IRuntimePlugin plugin)
        {
            // This would need to be tracked in the plugin implementation
            // For now, assume setup is complete if the plugin exists
            return plugin != null;
        }

        /// <summary>
        /// Checks if a plugin has completed runtime initialization.
        /// </summary>
        private static bool HasCompletedRuntimeInitialization(IRuntimePlugin plugin)
        {
            // This would need to be tracked in the plugin implementation
            // For now, assume initialization is complete if the plugin exists
            return plugin != null;
        }

        /// <summary>
        /// Checks if a plugin has an initialization error.
        /// </summary>
        private static bool HasInitializationError(object plugin)
        {
            // This would need to be tracked in the plugin implementation
            // For now, check for common error conditions
            if (plugin is MonoBehaviour mb)
            {
                return !mb.gameObject.activeInHierarchy || !mb.enabled;
            }

            return false;
        }

        /// <summary>
        /// Checks if a plugin is currently in the initialization phase.
        /// </summary>
        private static bool IsInInitializationPhase(object plugin)
        {
            // This would need to be tracked in the plugin implementation
            // For now, assume plugins are not in initialization phase
            return false;
        }

        /// <summary>
        /// Gathers plugins tracked by GlobalInstaller using reflection.
        /// </summary>
        private static void GatherGlobalInstallerPluginsViaReflection(object installer, List<PluginStateModel> diagnostics)
        {
            try
            {
                var installerType = installer.GetType();
                var originalCount = diagnostics.Count;
                
                // Method 1: Get all properties that return AbstractGamePlugin
                var pluginProperties = installerType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => typeof(AbstractGamePlugin).IsAssignableFrom(prop.PropertyType))
                    .ToArray();

                foreach (var property in pluginProperties)
                {
                    try
                    {
                        var plugin = property.GetValue(installer);
                        if (plugin is AbstractGamePlugin gamePlugin && gamePlugin != null)
                        {
                            var diagnostic = CreateDetailedPluginDiagnostic(plugin, installer);
                            diagnostic.Name = $"[Global] {gamePlugin.name}";
                            diagnostics.Add(diagnostic);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Debug("Error accessing property {PropertyName}: {Message}", property.Name, ex.Message);
                    }
                }
                
                // Method 2: Find all AbstractGamePlugin instances in the scene as fallback
                if (diagnostics.Count == originalCount)
                {
                    var allPlugins = UnityEngine.Object.FindObjectsByType<AbstractGamePlugin>(FindObjectsSortMode.None);
                    foreach (var plugin in allPlugins)
                    {
                        // Only include persistent plugins (DontDestroyOnLoad)
                        var gameObject = plugin.gameObject;
                        if (gameObject.scene.buildIndex == -1) // DontDestroyOnLoad objects
                        {
                            var diagnostic = CreateDetailedPluginDiagnostic(plugin, installer);
                            diagnostic.Name = $"[Global] {plugin.name}";
                            diagnostics.Add(diagnostic);
                        }
                    }
                }
                
                log.Debug("Found {PluginCount} plugins in GlobalInstaller (original count: {OriginalCount})", 
                    diagnostics.Count - originalCount, originalCount);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error gathering GlobalInstaller plugins via reflection: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Gathers plugins tracked by GameInstaller using reflection.
        /// </summary>
        private static void GatherGameInstallerPluginsViaReflection(object installer, List<PluginStateModel> diagnostics)
        {
            try
            {
                var installerType = installer.GetType();
                var originalCount = diagnostics.Count;
                
                // Method 1: Get all properties that return AbstractGamePlugin
                var pluginProperties = installerType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => typeof(AbstractGamePlugin).IsAssignableFrom(prop.PropertyType))
                    .ToArray();

                foreach (var property in pluginProperties)
                {
                    try
                    {
                        var plugin = property.GetValue(installer);
                        if (plugin is AbstractGamePlugin gamePlugin && gamePlugin != null)
                        {
                            var diagnostic = CreateDetailedPluginDiagnostic(plugin, installer);
                            diagnostic.Name = $"[Game] {gamePlugin.name}";
                            diagnostics.Add(diagnostic);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Debug("Error accessing property {PropertyName}: {Message}", property.Name, ex.Message);
                    }
                }
                
                // Method 2: Find all AbstractGamePlugin instances in active scenes as fallback
                if (diagnostics.Count == originalCount)
                {
                    var allPlugins = UnityEngine.Object.FindObjectsByType<AbstractGamePlugin>(FindObjectsSortMode.None);
                    foreach (var plugin in allPlugins)
                    {
                        var gameObject = plugin.gameObject;
                        // Only include scene-specific plugins (not DontDestroyOnLoad)
                        if (gameObject.scene.buildIndex >= 0)
                        {
                            var diagnostic = CreateDetailedPluginDiagnostic(plugin, installer);
                            diagnostic.Name = $"[Game] {plugin.name}";
                            diagnostics.Add(diagnostic);
                        }
                    }
                }
                
                log.Debug("Found {PluginCount} plugins in GameInstaller (original count: {OriginalCount})", 
                    diagnostics.Count - originalCount, originalCount);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Error gathering GameInstaller plugins via reflection: {Message}", ex.Message);
            }
        }

        public static Type FindTypeByName(string typeName)
        {
            // Search through all loaded assemblies for the type by name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == typeName);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Skip assemblies that can't be queried (e.g., dynamic assemblies)
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if a type is a LifetimeScope (VContainer base class).
        /// </summary>
        private static bool IsLifetimeScope(Type type)
        {
            if (type == null) return false;
            
            // Check if the type inherits from LifetimeScope by walking up the inheritance chain
            var currentType = type;
            while (currentType != null)
            {
                if (currentType.Name == "LifetimeScope")
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            
            return false;
        }
    }
}

#endif