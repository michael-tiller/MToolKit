using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  ///   AssetReference specifically for QuestDefinition assets.
  ///   Enables addressable loading of quest definitions while maintaining GUID-based lookup.
  /// </summary>
  [System.Serializable]
  public sealed class QuestAssetReference : AssetReferenceT<QuestDefinition>
  {
    /// <summary>
    ///   Constructs a new reference to a QuestDefinition.
    /// </summary>
    /// <param name="guid">The asset GUID.</param>
    public QuestAssetReference(string guid) : base(guid)
    {
    }

    /// <summary>
    ///   Get the GUID of the quest definition (for lookup purposes).
    ///   This will be available after the asset is loaded.
    /// </summary>
    public string QuestGuid => (Asset as QuestDefinition)?.Guid ?? string.Empty;

    /// <summary>
    ///   Check if this reference has a valid GUID assigned.
    /// </summary>
    public bool HasValidGuid => !string.IsNullOrEmpty(AssetGUID);
  }
}

