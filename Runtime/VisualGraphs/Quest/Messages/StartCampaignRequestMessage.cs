using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest.Messages
{
  /// <summary>
  /// Request to start a campaign. QuestManager subscribes to this and starts the campaign.
  /// Publish via GameMessageBroker.Publish(new StartCampaignRequestMessage(campaign))
  /// </summary>
  public readonly struct StartCampaignRequestMessage : IGameMessage
  {
    /// <summary>
    /// The campaign definition to start
    /// </summary>
    public readonly QuestCampaign Campaign;

    public StartCampaignRequestMessage(QuestCampaign campaign)
    {
      Campaign = campaign;
    }
  }
}

