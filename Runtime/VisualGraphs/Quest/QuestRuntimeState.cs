using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Runtime state for a quest (active, completed, locked, etc.).
  /// Tracks state, completion count, and graph state for progress tracking.
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
    /// Current state of the quest (Locked, Available, Active, Completed, Failed)
    /// </summary>
    [field: SerializeField]
    public EQuestState State { get; set; } = EQuestState.Locked;

    /// <summary>
    /// Number of times this quest has been completed (for repeatable quests)
    /// </summary>
    [field: SerializeField]
    public int TimesCompleted { get; set; } = 0;

    /// <summary>
    /// Reference to the quest definition asset (may be null if quest is locked/not loaded)
    /// </summary>
    [field: SerializeField]
    public QuestDefinition Definition { get; private set; }

    /// <summary>
    /// Graph state for this quest (shared across all objective graphs).
    /// Only available when quest is Active or Completed.
    /// </summary>
    public IGraphState GraphState { get; }

    /// <summary>
    /// When this quest was started (only valid when State is Active or Completed)
    /// </summary>
    public DateTime? StartedAt { get; private set; }

    [ShowInInspector]
    [ReadOnly]
    public string StartedAtValue => Application.isPlaying ? StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not started" : "Not started";

    /// <summary>
    /// List of loaded graph instance IDs for objective graphs.
    /// Used for cleanup when quest completes/abandons.
    /// </summary>
    [field: SerializeField]
    public List<string> LoadedGraphInstanceIds { get; private set; }

    /// <summary>
    /// Create a runtime state for an active quest (with graph state).
    /// </summary>
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
      State = EQuestState.Active;
      LoadedGraphInstanceIds = new List<string>();
    }

    /// <summary>
    /// Create a runtime state for a quest that's not yet active (locked, available, etc.).
    /// </summary>
    public QuestRuntimeState(
        string questGuid,
        EQuestState initialState = EQuestState.Locked)
    {
      QuestGuid = questGuid ?? throw new ArgumentNullException(nameof(questGuid));
      State = initialState;
      LoadedGraphInstanceIds = new List<string>();
    }

    /// <summary>
    /// Set the quest definition (for quests that were locked/available and are now being activated).
    /// </summary>
    public void SetDefinition(QuestDefinition definition)
    {
      Definition = definition;
    }

    /// <summary>
    /// Mark quest as started (transition from Available/Locked to Active).
    /// </summary>
    public void MarkStarted(DateTime startedAt)
    {
      State = EQuestState.Active;
      StartedAt = startedAt;
    }

    /// <summary>
    /// Mark quest as completed.
    /// </summary>
    public void MarkCompleted()
    {
      State = EQuestState.Completed;
      TimesCompleted++;
    }

    /// <summary>
    /// Mark quest as failed (if IsFailable).
    /// </summary>
    public void MarkFailed()
    {
      State = EQuestState.Failed;
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

