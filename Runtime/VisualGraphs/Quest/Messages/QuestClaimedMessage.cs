using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Emitted when a quest's rewards are claimed via IQuestManager.ClaimQuest.
  /// This is separate from QuestCompletedMessage (objectives done).
  /// Game's reward system should subscribe to THIS message to grant rewards.
  /// </summary>
  public readonly struct QuestClaimedMessage : IGameMessage
  {
    /// <summary>
    /// GUID of the quest that was claimed
    /// </summary>
    public readonly string QuestGuid;

    /// <summary>
    /// Reference to the quest definition
    /// </summary>
    public readonly QuestDefinition Quest;

    /// <summary>
    /// Total time from quest start to claim
    /// </summary>
    public readonly System.TimeSpan TotalDuration;

    public QuestClaimedMessage(string questGuid, QuestDefinition quest, System.TimeSpan totalDuration)
    {
      QuestGuid = questGuid;
      Quest = quest;
      TotalDuration = totalDuration;
    }
  }
}

