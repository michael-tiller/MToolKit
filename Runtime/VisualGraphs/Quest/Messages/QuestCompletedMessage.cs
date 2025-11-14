using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Emitted when a quest is completed via IQuestManager.CompleteQuest
  /// </summary>
  public readonly struct QuestCompletedMessage : IGameMessage
  {
    /// <summary>
    /// GUID of the quest that completed
    /// </summary>
    public readonly string QuestGuid;

    /// <summary>
    /// Reference to the quest definition
    /// </summary>
    public readonly QuestDefinition Quest;

    /// <summary>
    /// How long the quest took to complete
    /// </summary>
    public readonly System.TimeSpan Duration;

    public QuestCompletedMessage(string questGuid, QuestDefinition quest, System.TimeSpan duration)
    {
      QuestGuid = questGuid;
      Quest = quest;
      Duration = duration;
    }
  }
}

