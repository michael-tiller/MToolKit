using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  ///   AssetReference specifically for QuestCampaign assets.
  ///   Enables addressable loading of campaign definitions while maintaining GUID-based lookup.
  /// </summary>
  [System.Serializable]
  public sealed class CampaignAssetReference : AssetReferenceT<QuestCampaign>
  {
    /// <summary>
    ///   Constructs a new reference to a QuestCampaign.
    /// </summary>
    /// <param name="guid">The asset GUID.</param>
    public CampaignAssetReference(string guid) : base(guid)
    {
    }

    /// <summary>
    ///   Get the GUID of the campaign definition (for lookup purposes).
    ///   This will be available after the asset is loaded.
    /// </summary>
    public string CampaignGuid => (Asset as QuestCampaign)?.Guid ?? string.Empty;

    /// <summary>
    ///   Check if this reference has a valid GUID assigned.
    /// </summary>
    public bool HasValidGuid => !string.IsNullOrEmpty(AssetGUID);
  }
}

