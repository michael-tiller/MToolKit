using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Emitted when a quest is abandoned via IQuestManager.AbandonQuest
  /// </summary>
  public readonly struct QuestAbandonedMessage : IGameMessage
  {
    /// <summary>
    /// GUID of the quest that was abandoned
    /// </summary>
    public readonly string QuestGuid;

    /// <summary>
    /// Reference to the quest definition
    /// </summary>
    public readonly QuestDefinition Quest;

    /// <summary>
    /// Completion percentage when abandoned (0.0 to 1.0)
    /// </summary>
    public readonly float ProgressWhenAbandoned;

    public QuestAbandonedMessage(string questGuid, QuestDefinition quest, float progressWhenAbandoned)
    {
      QuestGuid = questGuid;
      Quest = quest;
      ProgressWhenAbandoned = progressWhenAbandoned;
    }
  }
}

