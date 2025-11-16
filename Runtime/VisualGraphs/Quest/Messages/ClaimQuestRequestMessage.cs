using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Request to claim a quest's rewards. QuestManager subscribes to this and calls ClaimQuest,
  /// which fires QuestClaimedMessage for reward systems to handle.
  /// Publish via GameMessageBroker.Publish(new ClaimQuestRequestMessage(questGuid))
  /// </summary>
  public readonly struct ClaimQuestRequestMessage : IGameMessage
  {
    /// <summary>
    /// GUID of the quest to claim
    /// </summary>
    public readonly string QuestGuid;

    public ClaimQuestRequestMessage(string questGuid)
    {
      QuestGuid = questGuid;
    }
  }
}

