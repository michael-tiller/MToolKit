using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Simple registry for quest campaigns.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/VisualGraphs/Quest Database", fileName = "QuestDatabase")]
  public sealed class QuestDatabase : ScriptableObject
  {
    [Title("Campaign Registry")]
    [InfoBox("Add campaigns in the order you want them available.")]
    public List<Definitions.QuestCampaign> Campaigns = new();

    /// <summary>
    /// Gets the first campaign, if any.
    /// </summary>
    public Definitions.QuestCampaign GetFirstCampaign()
    {
      return Campaigns.Count > 0 ? Campaigns[0] : null;
    }

    /// <summary>
    /// Gets the first quest from the first campaign, if any.
    /// </summary>
    public Definitions.QuestDefinition GetFirstQuest()
    {
      var campaign = GetFirstCampaign();
      if (campaign == null || campaign.Quests.Count == 0) return null;
      return campaign.Quests[0];
    }
  }
}

