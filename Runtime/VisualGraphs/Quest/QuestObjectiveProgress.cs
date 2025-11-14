using System;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  ///   Runtime progress tracking for a single quest objective.
  ///   Stored in graph state during execution.
  ///   Uses GUID to identify the objective (safe, no typos!).
  /// </summary>
  [Serializable]
  public sealed class QuestObjectiveProgress
  {
    /// <summary>GUID of the objective (from QuestObjective.Guid)</summary>
    public string ObjectiveGuid;

    /// <summary>Current progress value</summary>
    public int Current;

    /// <summary>Required progress to complete</summary>
    public int Required;

    /// <summary>Is this objective complete?</summary>
    public bool IsComplete => Current >= Required;

    /// <summary>Progress as percentage (0.0 to 1.0)</summary>
    public float Percentage => Required > 0 ? (float)Current / Required : 1.0f;

    public QuestObjectiveProgress()
    {
    }

    public QuestObjectiveProgress(string objectiveGuid, int required)
    {
      ObjectiveGuid = objectiveGuid;
      Current = 0;
      Required = required;
    }

    public override string ToString()
    {
      return $"{Current}/{Required}";
    }
  }
}

