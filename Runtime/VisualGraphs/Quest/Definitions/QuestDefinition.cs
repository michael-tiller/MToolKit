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
    [Tooltip("Auto-start this quest when the game loads")]
    public bool AutoStart;

    [BoxGroup("Configuration")]
    [Tooltip("Quest category for UI/filtering")]
    public string Category = "Main";

    [BoxGroup("Configuration")]
    [Tooltip("Whether this quest can be abandoned by the player. Set to false for critical/main story quests.")]
    public bool IsAbandonable = true;

    [BoxGroup("Addressable")]
    [Tooltip("Optional addressable key for dynamic loading")]
    public string AddressableKey;

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
  }
}