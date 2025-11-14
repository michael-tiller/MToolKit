using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Runtime state for an active quest.
  /// Tracks when it started, its graph state, and objective progress.
  /// </summary>
  [Serializable]
  public sealed class QuestRuntimeState
  {
    /// <summary>
    /// GUID of the quest (from QuestDefinition.Guid)
    /// </summary>
    [field: SerializeField]
    public string QuestGuid { get; private set; }

    /// <summary>
    /// Reference to the quest definition asset
    /// </summary>
    [field: SerializeField]
    public QuestDefinition Definition { get; private set; }

    /// <summary>
    /// Graph state for this quest (shared across all objective graphs)
    /// </summary>
    public IGraphState GraphState { get; }


    /// <summary>
    /// When this quest was started
    /// </summary>
    public DateTime StartedAt { get; }

    [ShowInInspector]
    [ReadOnly]
    public string StartedAtValue => StartedAt.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// List of loaded graph instance IDs for objective graphs.
    /// Used for cleanup when quest completes/abandons.
    /// </summary>
    [field: SerializeField]
    public List<string> LoadedGraphInstanceIds { get; private set; }

    public QuestRuntimeState(
        string questGuid,
        QuestDefinition definition,
        IGraphState graphState,
        DateTime startedAt)
    {
      QuestGuid = questGuid ?? throw new ArgumentNullException(nameof(questGuid));
      Definition = definition ?? throw new ArgumentNullException(nameof(definition));
      GraphState = graphState ?? throw new ArgumentNullException(nameof(graphState));
      StartedAt = startedAt;
      LoadedGraphInstanceIds = new List<string>();
    }

    /// <summary>
    /// Gets the progress for a specific objective.
    /// </summary>
    public QuestObjectiveProgress GetObjectiveProgress(string objectiveGuid)
    {
      var objective = Definition.Objectives?.Find(o => o.Guid == objectiveGuid);
      return Definition.GetObjectiveProgress(GraphState, objective);
    }

    /// <summary>
    /// Gets all objective progress for this quest.
    /// </summary>
    public List<QuestObjectiveProgress> GetAllObjectiveProgress()
    {
      return Definition.GetAllObjectiveProgress(GraphState);
    }

    /// <summary>
    /// Calculates quest completion percentage (0.0 to 1.0).
    /// </summary>
    public float GetCompletionPercentage()
    {
      return Definition.GetCompletionPercentage(GraphState);
    }

    /// <summary>
    /// Checks if all required objectives are complete.
    /// </summary>
    public bool IsComplete()
    {
      return Definition.IsComplete(GraphState);
    }
  }
}

