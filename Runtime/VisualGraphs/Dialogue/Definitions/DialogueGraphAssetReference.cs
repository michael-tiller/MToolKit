using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Definitions
{
  /// <summary>
  ///   AssetReference specifically for DialogueDefinition assets.
  ///   Enables addressable loading of dialogue definitions while maintaining ID-based lookup.
  /// </summary>
  [System.Serializable]
  public sealed class DialogueGraphAssetReference : AssetReferenceT<DialogueDefinition>
  {
    /// <summary>
    ///   Constructs a new reference to a DialogueDefinition.
    /// </summary>
    /// <param name="guid">The asset GUID.</param>
    public DialogueGraphAssetReference(string guid) : base(guid)
    {
    }

    /// <summary>
    ///   Get the DialogueId of the dialogue definition (for lookup purposes).
    ///   This will be available after the asset is loaded.
    /// </summary>
    public string DialogueId => (Asset as DialogueDefinition)?.DialogueId ?? string.Empty;

    /// <summary>
    ///   Check if this reference has a valid GUID assigned.
    /// </summary>
    public bool HasValidGuid => !string.IsNullOrEmpty(AssetGUID);
  }
}

