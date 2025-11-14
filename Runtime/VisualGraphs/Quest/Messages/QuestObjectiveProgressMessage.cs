using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Emitted when a quest objective's progress changes.
  /// Can be emitted by QuestObjectiveIncrementNodeExecutor or QuestObjectiveSetNodeExecutor.
  /// </summary>
  public readonly struct QuestObjectiveProgressMessage : IGameMessage
  {
    public override string ToString()
    {
      return $"QuestObjectiveProgressMessage: QuestGuid={QuestGuid}, ObjectiveGuid={ObjectiveGuid}, Objective={Objective}, Current={Current}, Required={Required}, Percentage={Percentage}, IsComplete={IsComplete}";
    }

    /// <summary>
    /// GUID of the quest this objective belongs to
    /// </summary>
    public readonly string QuestGuid;

    /// <summary>
    /// GUID of the objective that progressed
    /// </summary>
    public readonly string ObjectiveGuid;

    /// <summary>
    /// Reference to the objective definition
    /// </summary>
    public readonly QuestObjective Objective;

    /// <summary>
    /// Current progress value
    /// </summary>
    public readonly int Current;

    /// <summary>
    /// Required progress value
    /// </summary>
    public readonly int Required;

    /// <summary>
    /// Progress percentage (0.0 to 1.0)
    /// </summary>
    public readonly float Percentage;

    /// <summary>
    /// Whether this objective is now complete
    /// </summary>
    public readonly bool IsComplete;

    public QuestObjectiveProgressMessage(
        string questGuid,
        string objectiveGuid,
        QuestObjective objective,
        int current,
        int required,
        float percentage,
        bool isComplete)
    {
      QuestGuid = questGuid;
      ObjectiveGuid = objectiveGuid;
      Objective = objective;
      Current = current;
      Required = required;
      Percentage = percentage;
      IsComplete = isComplete;
    }
  }
}

