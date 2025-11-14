using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Runtime state for an active quest.
  /// Tracks when it started, its graph state, and objective progress.
  /// </summary>
  public sealed class QuestRuntimeState
  {
    /// <summary>
    /// GUID of the quest (from QuestDefinition.Guid)
    /// </summary>
    public string QuestGuid { get; }

    /// <summary>
    /// Reference to the quest definition asset
    /// </summary>
    public QuestDefinition Definition { get; }

    /// <summary>
    /// Graph state for this quest (shared across all objective graphs)
    /// </summary>
    public IGraphState GraphState { get; }

    /// <summary>
    /// When this quest was started
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// List of loaded graph instance IDs for objective graphs.
    /// Used for cleanup when quest completes/abandons.
    /// </summary>
    public List<string> LoadedGraphInstanceIds { get; }

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

