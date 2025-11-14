using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Emitted when a quest is started via IQuestManager.StartQuestAsync
  /// </summary>
  public readonly struct QuestStartedMessage : IGameMessage
  {
    /// <summary>
    /// GUID of the quest that started
    /// </summary>
    public readonly string QuestGuid;

    /// <summary>
    /// Reference to the quest definition
    /// </summary>
    public readonly QuestDefinition Quest;

    public QuestStartedMessage(string questGuid, QuestDefinition quest)
    {
      QuestGuid = questGuid;
      Quest = quest;
    }
  }
}

