using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  ///   AssetReference specifically for QuestObjective assets.
  ///   Enables addressable loading of objective definitions while maintaining GUID-based lookup.
  /// </summary>
  [System.Serializable]
  public sealed class ObjectiveAssetReference : AssetReferenceT<QuestObjective>
  {
    /// <summary>
    ///   Constructs a new reference to a QuestObjective.
    /// </summary>
    /// <param name="guid">The asset GUID.</param>
    public ObjectiveAssetReference(string guid) : base(guid)
    {
    }

    /// <summary>
    ///   Get the GUID of the objective definition (for lookup purposes).
    ///   This will be available after the asset is loaded.
    /// </summary>
    public string ObjectiveGuid => (Asset as QuestObjective)?.Guid ?? string.Empty;

    /// <summary>
    ///   Check if this reference has a valid GUID assigned.
    /// </summary>
    public bool HasValidGuid => !string.IsNullOrEmpty(AssetGUID);
  }
}

