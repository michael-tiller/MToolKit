#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Editor;
using Serilog;
using UnityEditor;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Editor
{
    /// <summary>
    /// Unity Editor window for runtime diagnosis of plugin states.
    /// Provides real-time monitoring of plugin lifecycle, dependencies, and runtime status.
    /// </summary>
    public class PluginDiagnosisWindow : EditorWindow
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<PluginDiagnosisWindow>().ForFeature("Editor"));
        private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        private Vector2 scrollPosition;
        private bool autoRefresh = true;
        private float refreshInterval = 5.0f;
        private double lastRefreshTime;
        private List<PluginStateModel> pluginStates = new();
        private bool showOnlyActivePlugins = false;
        private bool showDependencyDetails = true;
        private string searchFilter = "";
        private bool showDebugInfo = true;
        private int selectedInstallerTab = 0;
        private readonly string[] installerTabs = { "All", "GlobalInstaller", "GameInstaller" };
        private bool showFilters = false;

        [MenuItem("Tools/Plugin Diagnostics")]
        public static void ShowWindow()
        {
            var window = GetWindow<PluginDiagnosisWindow>("Plugin Diagnostics");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            RefreshPluginStates();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                RefreshPluginStates();
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawControls();
            DrawPluginList();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("Plugin Runtime Diagnostics", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    RefreshPluginStates();
                }
                
                autoRefresh = GUILayout.Toggle(autoRefresh, "Auto Refresh", EditorStyles.toolbarButton);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawControls()
        {
            // Installer tabs with better styling
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Installer Selection", EditorStyles.boldLabel);
                var newTab = GUILayout.Toolbar(selectedInstallerTab, installerTabs, GUILayout.Height(25));
                
                // Refresh if tab changed
                if (newTab != selectedInstallerTab)
                {
                    selectedInstallerTab = newTab;
                    RefreshPluginStates();
                }
                
                // Show current selection
                EditorGUILayout.LabelField($"Selected: {installerTabs[selectedInstallerTab]}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);

            showFilters = EditorGUILayout.BeginFoldoutHeaderGroup(showFilters, "Filters");
            if (showFilters)
            {
                    refreshInterval = EditorGUILayout.Slider("Refresh Rate", refreshInterval, 0.1f, 5.0f);
                    showOnlyActivePlugins = EditorGUILayout.Toggle("Active Only", showOnlyActivePlugins);
                    showDependencyDetails = EditorGUILayout.Toggle("Show Dependencies", showDependencyDetails);
                    showDebugInfo = EditorGUILayout.Toggle("Debug Info", showDebugInfo);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label("Search:", GUILayout.Width(50));
                searchFilter = EditorGUILayout.TextField(searchFilter);
                
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    searchFilter = "";
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPluginList()
        {
            var filteredPlugins = GetFilteredPlugins();
            
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField($"Plugins ({filteredPlugins.Count}) - {installerTabs[selectedInstallerTab]}", EditorStyles.boldLabel);
                
                // Debug info section
                if (showDebugInfo)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    {
                        EditorGUILayout.LabelField("Debug Information", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"Selected installer: {installerTabs[selectedInstallerTab]}");
                        EditorGUILayout.LabelField($"Total plugins found: {pluginStates.Count}");
                        EditorGUILayout.LabelField($"Filtered plugins: {filteredPlugins.Count}");
                        
                        // Check installer existence
                        var globalInstallerExists = CheckInstallerExists("GlobalInstaller");
                        var gameInstallerExists = CheckInstallerExists("GameInstaller");
                        EditorGUILayout.LabelField($"GlobalInstaller found: {globalInstallerExists}");
                        EditorGUILayout.LabelField($"GameInstaller found: {gameInstallerExists}");
                        
                        // Check if we're in play mode
                        EditorGUILayout.LabelField($"Play mode: {Application.isPlaying}");
                        
                        if (GUILayout.Button("Force Refresh"))
                        {
                            RefreshPluginStates();
                        }
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                {
                    if (filteredPlugins.Count == 0)
                    {
                        EditorGUILayout.HelpBox($"No plugins found for {installerTabs[selectedInstallerTab]}. Make sure the game is running and the installer is present in the scene.", MessageType.Warning);
                    }
                    else
                    {
                        foreach (var pluginState in filteredPlugins)
                        {
                            DrawPluginState(pluginState);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPluginState(PluginStateModel pluginState)
        {
            // Determine background color based on plugin type
            Color backgroundColor = GetPluginTypeBackgroundColor(pluginState);
            var originalBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // Plugin header with name and status
                EditorGUILayout.BeginHorizontal();
                {
                    var statusColor = GetStatusColor(pluginState.Status);
                    var originalColor = GUI.color;
                    GUI.color = statusColor;
                    GUILayout.Label(GetStatusIcon(pluginState.Status), GUILayout.Width(20));
                    GUI.color = originalColor;
                    
                    EditorGUILayout.LabelField(pluginState.Name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(pluginState.Status.ToString(), EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                // Plugin details
                EditorGUILayout.LabelField($"Type: {pluginState.TypeName}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Phase: {pluginState.CurrentPhase}", EditorStyles.miniLabel);
                
                if (pluginState.IsRuntimeSystem)
                {
                    EditorGUILayout.LabelField($"Runtime System: {(pluginState.IsRunning ? "Running" : "Stopped")}", EditorStyles.miniLabel);
                }

                // Dependencies
                if (showDependencyDetails && pluginState.Dependencies.Any())
                {
                    EditorGUILayout.LabelField("Dependencies:", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    foreach (var dep in pluginState.Dependencies)
                    {
                        var depColor = dep.IsResolved ? Color.green : Color.red;
                        var originalColor = GUI.color;
                        GUI.color = depColor;
                        
                        var depText = dep.IsOptional ? $"{dep.ServiceName} (Optional)" : dep.ServiceName;
                        var statusIcon = dep.IsResolved ? "✓" : "✗";
                        
                        EditorGUILayout.LabelField($"• {depText}: {statusIcon}", EditorStyles.miniLabel);
                        
                        if (!dep.IsResolved && !string.IsNullOrEmpty(dep.ResolutionError))
                        {
                            EditorGUILayout.LabelField($"  Error: {dep.ResolutionError}", EditorStyles.miniLabel);
                        }
                        
                        GUI.color = originalColor;
                    }
                    EditorGUI.indentLevel--;
                }

                // Error information
                if (!string.IsNullOrEmpty(pluginState.ErrorMessage))
                {
                    var originalColor = GUI.color;
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField($"Error: {pluginState.ErrorMessage}", EditorStyles.miniLabel);
                    GUI.color = originalColor;
                }

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    {
                        if (pluginState.GameObject != null)
                        {
                            EditorGUIUtility.PingObject(pluginState.GameObject);
                        }
                    }
                    
                    if (GUILayout.Button("Inspect", GUILayout.Width(60)))
                    {
                        if (pluginState.GameObject != null)
                        {
                            Selection.activeGameObject = pluginState.GameObject;
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            
            // Restore original background color
            GUI.backgroundColor = originalBackgroundColor;
            
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// Determines the background color for a plugin based on its type.
        /// </summary>
        private Color GetPluginTypeBackgroundColor(PluginStateModel pluginState)
        {
            // Check if it's a runtime plugin (pastel pink)
            if (pluginState.Name.Contains("RuntimePlugin"))
            {
                return new Color(1f, 0.8f, 0.9f, 0.3f); // Pastel pink with transparency
            }
            
            // Check if it's a game plugin (pastel baby blue)
            if (pluginState.Name.Contains("GamePlugin"))
            {
                return new Color(0.8f, 0.9f, 1f, 0.3f); // Pastel baby blue with transparency
            }
            
            // Default color for unknown types
            return Color.white;
        }

        private List<PluginStateModel> GetFilteredPlugins()
        {
            var filtered = pluginStates.AsEnumerable();

            if (showOnlyActivePlugins)
            {
                filtered = filtered.Where(p => p.Status == EPluginStatus.Active);
            }

            if (!string.IsNullOrEmpty(searchFilter))
            {
                filtered = filtered.Where(p => 
                    p.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    p.TypeName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        private Color GetStatusColor(EPluginStatus status)
        {
            return status switch
            {
                EPluginStatus.Active => Color.green,
                EPluginStatus.Initializing => Color.yellow,
                EPluginStatus.Error => Color.red,
                EPluginStatus.Stopped => Color.gray,
                EPluginStatus.Unknown => Color.white,
                _ => Color.white
            };
        }

        private string GetStatusIcon(EPluginStatus status)
        {
            return status switch
            {
                EPluginStatus.Active => "●",
                EPluginStatus.Initializing => "◐",
                EPluginStatus.Error => "✗",
                EPluginStatus.Stopped => "○",
                EPluginStatus.Unknown => "?",
                _ => "?"
            };
        }

        private void RefreshPluginStates()
        {
            try
            {
                var installerType = installerTabs[selectedInstallerTab];
                pluginStates = PluginDiagnosticService.GatherPluginDiagnostics(installerType);
                lastRefreshTime = EditorApplication.timeSinceStartup;
                log.Verbose("Refreshed plugin states: {PluginCount} plugins from {InstallerType}", pluginStates.Count, installerType);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error refreshing plugin states: {Message}", ex.Message);
            }
        }

        private static bool CheckInstallerExists(string installerName)
        {
            try
            {
                // Search for type by name across all loaded assemblies (namespace-agnostic)
                var installerType = PluginDiagnosticService.FindTypeByName(installerName);
                if (installerType != null)
                {
                    var instanceProperty = installerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        var installer = instanceProperty.GetValue(null);
                        if (installer != null) return true;
                    }
                }

                // Try scene search as fallback
                var installerObjects = FindObjectsByType(typeof(MonoBehaviour), FindObjectsSortMode.None)
                    .Where(mb => mb.GetType().Name == installerName)
                    .ToArray();
                
                return installerObjects.Length > 0;
            }
            catch
            {
                return false;
            }
        }


    }

}
#endif