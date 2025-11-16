using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Emitted when a campaign is completed (all required quests finished).
  /// Game systems can subscribe to this to grant campaign rewards, show completion UI, etc.
  /// </summary>
  public readonly struct CampaignCompletedMessage : IGameMessage
  {
    /// <summary>
    /// GUID of the campaign that was completed
    /// </summary>
    public readonly string CampaignGuid;

    /// <summary>
    /// Reference to the campaign definition
    /// </summary>
    public readonly QuestCampaign Campaign;

    public CampaignCompletedMessage(string campaignGuid, QuestCampaign campaign)
    {
      CampaignGuid = campaignGuid;
      Campaign = campaign;
    }
  }
}

