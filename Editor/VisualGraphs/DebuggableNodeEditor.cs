#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Dialogue.Definitions;

namespace MToolKit.Editor.VisualGraphs
{
  /// <summary>
  ///   Custom node editor that highlights nodes currently executing at runtime.
  /// </summary>
  [CustomNodeEditor(typeof(VisualGraphNodeBase))]
  public class DebuggableNodeEditor : NodeEditor
  {
    private static readonly Color HighlightColor = new Color(1f, 0.84f, 0f, 0.4f); // Yellow with transparency
    private static readonly Color ExecutingColor = new Color(0f, 1f, 0f, 0.4f); // Green with transparency

    // Cache graph ID lookups: maps graph asset GUID -> runtime graph ID
    private static readonly Dictionary<string, string> _graphIdCache = new Dictionary<string, string>();
    private static bool _cacheInitialized = false;
    private static double _lastCacheBuildTime = 0;
    private const double CacheRefreshInterval = 2.0; // Refresh cache every 2 seconds in play mode

    public override void OnBodyGUI()
    {
      // Draw highlight indicator in body (simple approach)
      DrawHighlightIndicator();

      // Draw the normal node body
      base.OnBodyGUI();
    }

    private void DrawHighlightIndicator()
    {
      if (!Application.isPlaying)
        return;

      var node = target as VisualGraphNodeBase;
      var graph = node?.graph;

      if (node == null || graph == null)
        return;

      var graphId = GetGraphId(graph);
      var nodeId = GetNodeId(node);

      if (string.IsNullOrEmpty(graphId) || string.IsNullOrEmpty(nodeId))
        return;

      var graphInfo = XNodeDebugState.GetGraphInfo(graphId);
      var isLastExecuted = XNodeDebugState.IsLastExecuted(graphId, nodeId);
      var isCurrentlyExecuting = graphInfo?.IsExecuting == true && graphInfo.LastExecutedNodeId == nodeId;

      if (!isLastExecuted && !isCurrentlyExecuting)
        return;

      // Draw a colored box at the top of the node body to indicate status
      var originalColor = GUI.color;
      var statusColor = isCurrentlyExecuting ? Color.green : Color.yellow;
      GUI.color = statusColor;

      var rect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
      EditorGUI.DrawRect(rect, statusColor);

      // Draw status text
      GUI.color = Color.white;
      var labelRect = new Rect(rect.x + 5, rect.y, rect.width - 10, rect.height);
      var statusText = isCurrentlyExecuting ? "● EXECUTING" : "● LAST EXECUTED";
      EditorGUI.LabelField(labelRect, statusText, EditorStyles.boldLabel);

      GUI.color = originalColor;
    }

    private static string GetGraphId(NodeGraph graph)
    {
      if (graph == null)
        return "";

      var assetPath = AssetDatabase.GetAssetPath(graph);
      if (string.IsNullOrEmpty(assetPath))
        return graph.name;

      var graphGuid = AssetDatabase.AssetPathToGUID(assetPath);
      if (string.IsNullOrEmpty(graphGuid))
        return System.IO.Path.GetFileNameWithoutExtension(assetPath);

      // Ensure cache is built and refreshed periodically
      EnsureCacheInitialized();

      // Check cache first
      if (_graphIdCache.TryGetValue(graphGuid, out var cachedGraphId))
        return cachedGraphId;

      // Not in cache - this shouldn't happen if cache is built correctly, but fallback
      return System.IO.Path.GetFileNameWithoutExtension(assetPath);
    }

    private static void EnsureCacheInitialized()
    {
      var currentTime = EditorApplication.timeSinceStartup;
      var needsRefresh = !_cacheInitialized ||
                        (Application.isPlaying && (currentTime - _lastCacheBuildTime) > CacheRefreshInterval);

      if (needsRefresh)
      {
        BuildGraphIdCache();
        _cacheInitialized = true;
        _lastCacheBuildTime = currentTime;
      }
    }

    private static void BuildGraphIdCache()
    {
      _graphIdCache.Clear();

      // Build reverse lookup: graph asset GUID -> runtime graph ID
      // This is done once and cached, making lookups O(1) instead of O(n*m)

      // Map quest-level graphs: QuestDefinition.Guid -> QuestDefinition.GraphAsset
      var questDefGuids = AssetDatabase.FindAssets("t:QuestDefinition");
      foreach (var defGuid in questDefGuids)
      {
        var defPath = AssetDatabase.GUIDToAssetPath(defGuid);
        var questDef = AssetDatabase.LoadAssetAtPath<QuestDefinition>(defPath);
        if (questDef?.GraphAsset != null)
        {
          var questGraphPath = AssetDatabase.GetAssetPath(questDef.GraphAsset);
          var questGraphGuid = AssetDatabase.AssetPathToGUID(questGraphPath);
          if (!string.IsNullOrEmpty(questGraphGuid) && !string.IsNullOrEmpty(questDef.Guid))
          {
            _graphIdCache[questGraphGuid] = questDef.Guid;
          }
        }
      }

      // Map dialogue graphs: DialogueDefinition.DialogueId -> DialogueDefinition.GraphAsset
      var dialogueDefGuids = AssetDatabase.FindAssets("t:DialogueDefinition");
      foreach (var defGuid in dialogueDefGuids)
      {
        var defPath = AssetDatabase.GUIDToAssetPath(defGuid);
        var dialogueDef = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(defPath);
        if (dialogueDef?.GraphAsset != null)
        {
          var dialogueGraphPath = AssetDatabase.GetAssetPath(dialogueDef.GraphAsset);
          var dialogueGraphGuid = AssetDatabase.AssetPathToGUID(dialogueGraphPath);
          if (!string.IsNullOrEmpty(dialogueGraphGuid) && !string.IsNullOrEmpty(dialogueDef.DialogueId))
          {
            _graphIdCache[dialogueGraphGuid] = dialogueDef.DialogueId;
          }
        }
      }

      // Map objective graphs: objective_{QuestObjective.Guid} -> QuestObjective.ObjectiveGraph
      var questObjectiveGuids = AssetDatabase.FindAssets("t:QuestObjective");
      foreach (var objGuid in questObjectiveGuids)
      {
        var objPath = AssetDatabase.GUIDToAssetPath(objGuid);
        var objective = AssetDatabase.LoadAssetAtPath<QuestObjective>(objPath);
        if (objective?.ObjectiveGraph != null && !string.IsNullOrEmpty(objective.Guid))
        {
          var objectiveGraphPath = AssetDatabase.GetAssetPath(objective.ObjectiveGraph);
          var objectiveGraphGuid = AssetDatabase.AssetPathToGUID(objectiveGraphPath);
          if (!string.IsNullOrEmpty(objectiveGraphGuid))
          {
            // Match runtime format: objective_{objective.Guid}
            _graphIdCache[objectiveGraphGuid] = $"objective_{objective.Guid}";
          }
        }
      }
    }

    private static string GetNodeId(VisualGraphNodeBase node)
    {
      return node?.Guid ?? "";
    }
  }
}
#endif

