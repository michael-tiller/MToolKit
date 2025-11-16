#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.VisualGraphs.Quest;
using Serilog;
using UnityEditor;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Editor
{
  /// <summary>
  ///   Unity Editor window for runtime diagnosis of QuestManager state.
  ///   Provides real-time monitoring of active, completed, and claimed quests.
  /// </summary>
  public class QuestManagerDiagnosticWindow : EditorWindow
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<QuestManagerDiagnosticWindow>().ForFeature("Editor"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private bool autoRefresh = true;
    private double lastRefreshTime;
    private float refreshInterval = 1.0f;
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private bool showDebugInfo = false;
    private bool showObjectiveDetails = true;

    private QuestManagerDiagnosticData diagnosticData;

    [MenuItem("Tools/MToolKit/Quest Manager Diagnostics")]
    public static void ShowWindow()
    {
      QuestManagerDiagnosticWindow window = GetWindow<QuestManagerDiagnosticWindow>("Quest Manager Diagnostics");
      window.minSize = new Vector2(800, 600);
      window.Show();
    }

    private void OnEnable()
    {
      EditorApplication.update += OnEditorUpdate;
      RefreshDiagnostics();
    }

    private void OnDisable()
    {
      EditorApplication.update -= OnEditorUpdate;
    }

    private void OnGUI()
    {
      DrawHeader();
      DrawControls();
      DrawQuestLists();
    }

    private void OnEditorUpdate()
    {
      if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
      {
        RefreshDiagnostics();
        Repaint();
      }
    }

    private void DrawHeader()
    {
      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
      {
        GUILayout.Label("Quest Manager Runtime Diagnostics", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
          RefreshDiagnostics();

        autoRefresh = GUILayout.Toggle(autoRefresh, "Auto Refresh", EditorStyles.toolbarButton);
      }
      EditorGUILayout.EndHorizontal();
    }

    private void DrawControls()
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      {
        refreshInterval = EditorGUILayout.Slider("Refresh Rate (seconds)", refreshInterval, 0.1f, 5.0f);
        showObjectiveDetails = EditorGUILayout.Toggle("Show Objective Details", showObjectiveDetails);
        showDebugInfo = EditorGUILayout.Toggle("Debug Info", showDebugInfo);
      }
      EditorGUILayout.EndVertical();

      EditorGUILayout.Space(5);

      EditorGUILayout.BeginHorizontal();
      {
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);

        if (GUILayout.Button("Clear", GUILayout.Width(50)))
          searchFilter = "";
      }
      EditorGUILayout.EndHorizontal();

      // Summary stats
      if (diagnosticData != null)
      {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        {
          GUILayout.Label($"Active: {diagnosticData.ActiveQuests.Count}", EditorStyles.boldLabel);
          GUILayout.Space(10);
          GUILayout.Label($"Completed: {diagnosticData.CompletedQuests.Count}", EditorStyles.boldLabel);
          GUILayout.Space(10);
          GUILayout.Label($"Claimed: {diagnosticData.ClaimedQuestGuids.Count}", EditorStyles.boldLabel);
        }
        EditorGUILayout.EndHorizontal();
      }
    }

    private void DrawQuestLists()
    {
      if (diagnosticData == null)
      {
        EditorGUILayout.HelpBox("QuestManager not found. Make sure the game is running and QuestManager is registered in the DI container.", MessageType.Warning);
        return;
      }

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      // Active Quests
      EditorGUILayout.LabelField($"Active Quests ({diagnosticData.ActiveQuests.Count})", EditorStyles.boldLabel);
      if (diagnosticData.ActiveQuests.Count == 0)
      {
        EditorGUILayout.HelpBox("No active quests", MessageType.Info);
      }
      else
      {
        foreach (var quest in GetFilteredQuests(diagnosticData.ActiveQuests))
        {
          DrawQuestState(quest, "Active", Color.green);
        }
      }

      EditorGUILayout.Space(10);

      // Completed (Unclaimed) Quests
      EditorGUILayout.LabelField($"Completed (Unclaimed) Quests ({diagnosticData.CompletedQuests.Count})", EditorStyles.boldLabel);
      if (diagnosticData.CompletedQuests.Count == 0)
      {
        EditorGUILayout.HelpBox("No completed unclaimed quests", MessageType.Info);
      }
      else
      {
        foreach (var quest in GetFilteredQuests(diagnosticData.CompletedQuests))
        {
          DrawQuestState(quest, "Completed", Color.yellow);
        }
      }

      EditorGUILayout.Space(10);

      // Claimed Quests
      EditorGUILayout.LabelField($"Claimed Quests ({diagnosticData.ClaimedQuestGuids.Count})", EditorStyles.boldLabel);
      if (diagnosticData.ClaimedQuestGuids.Count == 0)
      {
        EditorGUILayout.HelpBox("No claimed quests", MessageType.Info);
      }
      else
      {
        foreach (var questGuid in GetFilteredQuestGuids(diagnosticData.ClaimedQuestGuids))
        {
          DrawClaimedQuest(questGuid);
        }
      }

      EditorGUILayout.EndScrollView();
    }

    private void DrawQuestState(QuestStateInfo quest, string status, Color statusColor)
    {
      Color originalColor = GUI.color;
      GUI.color = statusColor;

      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      {
        // Quest header
        EditorGUILayout.BeginHorizontal();
        {
          GUILayout.Label("●", GUILayout.Width(15));
          EditorGUILayout.LabelField(quest.DisplayName ?? "Unknown Quest", EditorStyles.boldLabel);
          GUILayout.FlexibleSpace();
          EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        // Quest details
        EditorGUILayout.LabelField($"GUID: {quest.QuestGuid[..Math.Min(16, quest.QuestGuid.Length)]}...", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Started: {quest.StartedAt:yyyy-MM-dd HH:mm:ss}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Completion: {quest.CompletionPercentage:P0}", EditorStyles.miniLabel);

        // Objectives
        if (showObjectiveDetails && quest.Objectives != null && quest.Objectives.Count > 0)
        {
          EditorGUILayout.LabelField("Objectives:", EditorStyles.miniBoldLabel);
          EditorGUI.indentLevel++;
          foreach (var objective in quest.Objectives)
          {
            Color objColor = objective.IsComplete ? Color.green : Color.white;
            Color originalObjColor = GUI.color;
            GUI.color = objColor;

            string statusIcon = objective.IsComplete ? "✓" : "○";
            EditorGUILayout.LabelField($"{statusIcon} {objective.DisplayName}: {objective.Current}/{objective.Required} ({objective.Percentage:P0})", EditorStyles.miniLabel);

            GUI.color = originalObjColor;
          }
          EditorGUI.indentLevel--;
        }
      }
      EditorGUILayout.EndVertical();

      GUI.color = originalColor;
      EditorGUILayout.Space(2);
    }

    private void DrawClaimedQuest(string questGuid)
    {
      EditorGUILayout.BeginVertical(EditorStyles.helpBox);
      {
        EditorGUILayout.LabelField($"GUID: {questGuid[..Math.Min(16, questGuid.Length)]}...", EditorStyles.miniLabel);
      }
      EditorGUILayout.EndVertical();
      EditorGUILayout.Space(2);
    }

    private List<QuestStateInfo> GetFilteredQuests(List<QuestStateInfo> quests)
    {
      if (string.IsNullOrEmpty(searchFilter))
        return quests;

      return quests.Where(q =>
        (q.DisplayName?.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
        q.QuestGuid.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)
      ).ToList();
    }

    private List<string> GetFilteredQuestGuids(List<string> questGuids)
    {
      if (string.IsNullOrEmpty(searchFilter))
        return questGuids;

      return questGuids.Where(guid => guid.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void RefreshDiagnostics()
    {
      try
      {
        var questManager = ResolveQuestManager();
        if (questManager == null)
        {
          diagnosticData = null;
          return;
        }

        diagnosticData = new QuestManagerDiagnosticData
        {
          ActiveQuests = questManager.GetActiveQuests()
            .Select(q => CreateQuestStateInfo(q))
            .ToList(),
          CompletedQuests = questManager.GetCompletedUnclaimedQuests()
            .Select(q => CreateQuestStateInfo(q))
            .ToList(),
          ClaimedQuestGuids = questManager.GetClaimedQuestGuids().ToList()
        };

        lastRefreshTime = EditorApplication.timeSinceStartup;
      }
      catch (Exception ex)
      {
        log.Error(ex, "Error refreshing quest diagnostics: {Message}", ex.Message);
        diagnosticData = null;
      }
    }

    private QuestStateInfo CreateQuestStateInfo(QuestRuntimeState runtimeState)
    {
      var questDef = runtimeState.Definition;
      var objectives = runtimeState.GetAllObjectiveProgress()
        .Select(progress =>
        {
          var objective = questDef.Objectives?.Find(o => o.Guid == progress.ObjectiveGuid);
          return new ObjectiveInfo
          {
            DisplayName = objective?.DisplayName ?? "Unknown Objective",
            Current = progress.Current,
            Required = progress.Required,
            Percentage = progress.Percentage,
            IsComplete = progress.IsComplete
          };
        })
        .ToList();

      return new QuestStateInfo
      {
        QuestGuid = runtimeState.QuestGuid,
        DisplayName = questDef.DisplayName,
        StartedAt = runtimeState.StartedAt.HasValue ? runtimeState.StartedAt.Value : DateTime.UtcNow,
        CompletionPercentage = runtimeState.GetCompletionPercentage(),
        Objectives = objectives
      };
    }

    private IQuestManager ResolveQuestManager()
    {
      if (!Application.isPlaying)
      {
        log.Debug("Application is not playing, cannot resolve QuestManager");
        return null;
      }

      try
      {
        // Try GameRoot.Resolver first (simplest approach)
        var gameRootType = Type.GetType("MToolKit.Runtime.Core.GameRoot, MToolKit.Runtime");
        if (gameRootType != null)
        {
          var resolverProperty = gameRootType.GetProperty("Resolver", BindingFlags.Public | BindingFlags.Static);
          if (resolverProperty != null)
          {
            var resolver = resolverProperty.GetValue(null);
            if (resolver != null)
            {
              log.Debug("Using GameRoot.Resolver to resolve QuestManager");
              var tryResolveMethod = resolver.GetType().GetMethod("TryResolve", new[] { typeof(IQuestManager).MakeByRefType() });
              if (tryResolveMethod != null)
              {
                var parameters = new object[] { null };
                var result = (bool)tryResolveMethod.Invoke(resolver, parameters);
                if (result && parameters[0] != null)
                {
                  log.Debug("Successfully resolved QuestManager from GameRoot.Resolver");
                  return parameters[0] as IQuestManager;
                }
                else
                {
                  log.Debug("GameRoot.Resolver.TryResolve returned false for IQuestManager");
                }
              }
            }
            else
            {
              log.Debug("GameRoot.Resolver is null");
            }
          }
        }

        // Fallback: QuestManager is stored as a field in VisualGraphPlugin
        // Find VisualGraphPlugin in the scene and access questManager field directly
        var visualGraphPlugin = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
          .FirstOrDefault(mb => mb.GetType().Name == "VisualGraphPlugin");

        if (visualGraphPlugin != null)
        {
          log.Debug("Found VisualGraphPlugin, attempting to access questManager field");

          // Try to get questManager field via reflection
          var questManagerField = visualGraphPlugin.GetType().GetField("questManager",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

          if (questManagerField != null)
          {
            var questManager = questManagerField.GetValue(visualGraphPlugin) as IQuestManager;
            if (questManager != null)
            {
              log.Debug("Successfully accessed QuestManager from VisualGraphPlugin field");
              return questManager;
            }
            else
            {
              log.Debug("VisualGraphPlugin.questManager field is null");
            }
          }
          else
          {
            log.Debug("questManager field not found on VisualGraphPlugin");
          }

          // Fallback: Try to resolve from container
          log.Debug("Attempting to resolve QuestManager from VisualGraphPlugin container");
          var questManagerFromContainer = ResolveFromContainer(visualGraphPlugin, typeof(IQuestManager));
          if (questManagerFromContainer != null)
          {
            log.Debug("Successfully resolved QuestManager from VisualGraphPlugin container");
            return questManagerFromContainer as IQuestManager;
          }
          else
          {
            log.Debug("Failed to resolve QuestManager from VisualGraphPlugin container");
          }
        }
        else
        {
          log.Debug("VisualGraphPlugin not found in scene");
        }

        // Fallback: Try to find GlobalInstaller or GameInstaller and resolve from container
        var globalInstaller = FindInstaller("GlobalInstaller");
        if (globalInstaller != null)
        {
          log.Debug("Found GlobalInstaller, attempting to resolve QuestManager");
          var questManager = ResolveFromContainer(globalInstaller, typeof(IQuestManager));
          if (questManager != null)
          {
            log.Debug("Successfully resolved QuestManager from GlobalInstaller container");
            return questManager as IQuestManager;
          }
        }

        var gameInstaller = FindInstaller("GameInstaller");
        if (gameInstaller != null)
        {
          log.Debug("Found GameInstaller, attempting to resolve QuestManager");
          var questManager = ResolveFromContainer(gameInstaller, typeof(IQuestManager));
          if (questManager != null)
          {
            log.Debug("Successfully resolved QuestManager from GameInstaller container");
            return questManager as IQuestManager;
          }
        }

        var hasGameRootResolver = gameRootType != null &&
          gameRootType.GetProperty("Resolver", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) != null;
        log.Warning("QuestManager not found in any container. GameRoot.Resolver: {HasResolver}, VisualGraphPlugin found: {Found}",
          hasGameRootResolver, visualGraphPlugin != null);
        return null;
      }
      catch (Exception ex)
      {
        log.Error(ex, "Error resolving QuestManager: {Message}", ex.Message);
        return null;
      }
    }

    private object FindInstaller(string installerName)
    {
      try
      {
        var installerType = PluginDiagnosticService.FindTypeByName(installerName);
        if (installerType != null)
        {
          var instanceProperty = installerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
          if (instanceProperty != null)
          {
            return instanceProperty.GetValue(null);
          }
        }

        var installerObjects = UnityEngine.Object.FindObjectsByType(typeof(MonoBehaviour), FindObjectsSortMode.None)
          .Where(mb => mb.GetType().Name == installerName)
          .ToArray();

        return installerObjects.Length > 0 ? installerObjects[0] : null;
      }
      catch
      {
        return null;
      }
    }

    private object ResolveFromContainer(object installer, Type serviceType)
    {
      try
      {
        var installerType = installer.GetType();
        log.Debug("Attempting to resolve {ServiceType} from {InstallerType}", serviceType.Name, installerType.Name);

        // Try to get Container property (VContainer LifetimeScope)
        var containerProperty = installerType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (containerProperty != null)
        {
          var container = containerProperty.GetValue(installer);
          if (container != null)
          {
            log.Debug("Found Container property, type: {ContainerType}", container.GetType().Name);

            // Try TryResolve first (safer)
            var tryResolveMethod = container.GetType().GetMethod("TryResolve", new[] { typeof(Type), typeof(object).MakeByRefType() });
            if (tryResolveMethod != null)
            {
              var parameters = new object[] { serviceType, null };
              var result = (bool)tryResolveMethod.Invoke(container, parameters);
              if (result && parameters[1] != null)
              {
                log.Debug("Successfully resolved {ServiceType} via TryResolve", serviceType.Name);
                return parameters[1];
              }
              else
              {
                log.Debug("TryResolve returned false for {ServiceType}", serviceType.Name);
              }
            }

            // Fallback to Resolve
            var resolveMethod = container.GetType().GetMethod("Resolve", new[] { typeof(Type) });
            if (resolveMethod != null)
            {
              log.Debug("Attempting Resolve method for {ServiceType}", serviceType.Name);
              return resolveMethod.Invoke(container, new object[] { serviceType });
            }
            else
            {
              log.Debug("Resolve method not found on container type {ContainerType}", container.GetType().Name);
            }
          }
          else
          {
            log.Debug("Container property returned null");
          }
        }
        else
        {
          log.Debug("Container property not found on {InstallerType}", installerType.Name);
        }
      }
      catch (Exception ex)
      {
        log.Error(ex, "Error resolving {ServiceType} from container: {Message}", serviceType.Name, ex.Message);
      }

      return null;
    }

    private class QuestManagerDiagnosticData
    {
      public List<QuestStateInfo> ActiveQuests = new();
      public List<QuestStateInfo> CompletedQuests = new();
      public List<string> ClaimedQuestGuids = new();
    }

    private class QuestStateInfo
    {
      public string QuestGuid;
      public string DisplayName;
      public DateTime StartedAt;
      public float CompletionPercentage;
      public List<ObjectiveInfo> Objectives;
    }

    private class ObjectiveInfo
    {
      public string DisplayName;
      public int Current;
      public int Required;
      public float Percentage;
      public bool IsComplete;
    }
  }
}

#endif

