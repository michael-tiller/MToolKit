using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  ///   Quest definition asset linking a quest to a graph and configuration.
  ///   Contains objectives that the graph can reference and track.
  ///   Uses GUID for safe references (no string ID typos!).
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quests/Quest Definition", fileName = "QuestDefinition", order = 291)]
  public sealed class QuestDefinition : GuidScriptableObject
  {

    [BoxGroup("Display")]
    [TextArea(3, 8)]
    [Tooltip("Quest description shown in quest log")]
    public string Description = "";

    [BoxGroup("Display")]
    [PreviewField(80, ObjectFieldAlignment.Left)]
    [Tooltip("Quest icon displayed in UI")]
    public Sprite Icon;

    [BoxGroup("Objectives")]
    [InfoBox("Define all objectives for this quest. Graph nodes reference objectives by ObjectiveId.")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true, ShowFoldout = true)]
    public List<QuestObjective> Objectives = new();

    [BoxGroup("Graph")]
    [Tooltip("Optional quest graph for complex behavior (hidden objectives, cutscenes, quest-specific logic). Not needed for simple quests.")]
    public QuestGraphAsset GraphAsset;

    [BoxGroup("Variables")]
    [InlineEditor]
    [Tooltip("Initial variables for this quest (applied after global, before save)")]
    public GraphVariableSet InitialVariables;


    [BoxGroup("Configuration")]
    [Tooltip("Quest category for UI/filtering and assignment policy decisions")]
    public EQuestCategory Category = EQuestCategory.Main;

    [BoxGroup("Configuration")]
    [Tooltip("How this quest becomes available and starts")]
    public EQuestActivationMode ActivationMode = EQuestActivationMode.Manual;

    [BoxGroup("Configuration")]
    [Tooltip("Visibility rules for this quest in the quest log/UI")]
    public EQuestVisibility Visibility = EQuestVisibility.AlwaysVisible;

    [BoxGroup("Configuration")]
    [Tooltip("Whether this quest can be abandoned by the player. Set to false for critical/main story quests.")]
    public bool IsAbandonable = true;

    [BoxGroup("Configuration")]
    [Tooltip("Whether this quest can be repeated after completion")]
    public bool IsRepeatable = false;

    [BoxGroup("Configuration")]
    [Tooltip("Whether this quest can fail (e.g., time limit, critical failure condition)")]
    public bool IsFailable = false;

    /// <summary>
    ///   Get progress for a specific objective from graph state.
    /// </summary>
    public QuestObjectiveProgress GetObjectiveProgress(IGraphState state, QuestObjective objective)
    {
      if (objective == null) return null;

      var key = $"objective_{objective.Guid}";
      if (state.TryGet(key, out QuestObjectiveProgress progress))
        return progress;

      // Return default (0 progress) if not found
      return new QuestObjectiveProgress(objective.Guid, objective.RequiredProgress);
    }

    /// <summary>
    ///   Get progress for all objectives.
    /// </summary>
    public List<QuestObjectiveProgress> GetAllObjectiveProgress(IGraphState state)
    {
      return Objectives.Select(obj => GetObjectiveProgress(state, obj)).ToList();
    }

    /// <summary>
    ///   Calculate overall quest completion percentage (0.0 to 1.0).
    /// </summary>
    public float GetCompletionPercentage(IGraphState state)
    {
      if (Objectives.Count == 0) return 1.0f;

      var requiredObjectives = Objectives.Where(o => !o.Optional).ToList();
      if (requiredObjectives.Count == 0) return 1.0f;

      var completedCount = requiredObjectives.Count(obj =>
      {
        var progress = GetObjectiveProgress(state, obj);
        return progress?.IsComplete ?? false;
      });

      return (float)completedCount / requiredObjectives.Count;
    }

    /// <summary>
    ///   Check if all required objectives are complete.
    /// </summary>
    public bool IsComplete(IGraphState state)
    {
      return GetCompletionPercentage(state) >= 1.0f;
    }

    /// <summary>
    ///   Check if quest can be completed (all required objectives done).
    /// </summary>
    public bool CanComplete(IGraphState state)
    {
      return IsComplete(state);
    }

#if UNITY_EDITOR
    [BoxGroup("Graph")]
    [Button("Create & Link Quest Graph", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    [ShowIf("@GraphAsset == null")]
    [Tooltip("Links to existing QuestGraphAsset if found, otherwise creates a new one in the same folder with '_Graph' suffix")]
    private void CreateAndLinkQuestGraph()
    {
      var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
      if (string.IsNullOrEmpty(assetPath))
      {
        UnityEngine.Debug.LogError("Cannot create graph: Asset must be saved to disk first.");
        return;
      }

      var folderPath = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
      var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
      var graphName = $"{assetName}_Graph";
      var graphPath = $"{folderPath}/{graphName}.asset";

      // Check if graph already exists - if so, just link it
      var existingGraph = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(graphPath);
      if (existingGraph != null)
      {
        GraphAsset = existingGraph;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEngine.Debug.Log($"Linked to existing quest graph: {graphPath}");
        return;
      }

      // Create new graph asset
      var graphAsset = ScriptableObject.CreateInstance<QuestGraphAsset>();
      graphAsset.name = graphName;
      graphAsset.Description = $"Quest graph for {DisplayName}";

      UnityEditor.AssetDatabase.CreateAsset(graphAsset, graphPath);
      UnityEditor.AssetDatabase.SaveAssets();
      UnityEditor.AssetDatabase.Refresh();

      // Link it
      GraphAsset = graphAsset;
      UnityEditor.EditorUtility.SetDirty(this);

      UnityEngine.Debug.Log($"Created and linked quest graph: {graphPath}");
    }
#endif
  }
}