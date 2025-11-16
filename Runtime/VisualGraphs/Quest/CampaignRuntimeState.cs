using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Runtime state for a campaign.
  /// Tracks whether the campaign is active and completed.
  /// </summary>
  [System.Serializable]
  public sealed class CampaignRuntimeState
  {
    /// <summary>
    /// GUID of the campaign (from QuestCampaign.Guid)
    /// </summary>
    [field: SerializeField]
    public string CampaignGuid { get; private set; }

    /// <summary>
    /// Whether this campaign is currently active
    /// </summary>
    [field: SerializeField]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Whether this campaign has been completed
    /// </summary>
    [field: SerializeField]
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Reference to the campaign definition (may be null if not loaded)
    /// </summary>
    [field: SerializeField]
    public QuestCampaign Campaign { get; private set; }

    public CampaignRuntimeState(string campaignGuid, QuestCampaign campaign = null)
    {
      CampaignGuid = campaignGuid ?? throw new System.ArgumentNullException(nameof(campaignGuid));
      Campaign = campaign;
    }

    /// <summary>
    /// Set the campaign definition.
    /// </summary>
    public void SetCampaign(QuestCampaign campaign)
    {
      Campaign = campaign;
    }
  }
}

