using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Request to start a quest. QuestManager subscribes to this and calls StartQuestAsync.
  /// Publish via GameMessageBroker.Publish(new StartQuestRequestMessage(questDefinition))
  /// </summary>
  public readonly struct StartQuestRequestMessage : IGameMessage
  {
    /// <summary>
    /// The quest definition to start
    /// </summary>
    public readonly QuestDefinition Quest;

    public StartQuestRequestMessage(QuestDefinition quest)
    {
      Quest = quest;
    }
  }
}

