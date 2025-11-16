using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Node that publishes a StartCampaignRequestMessage via GameMessageBroker.
  ///   QuestManager subscribes to this message and starts the campaign.
  /// </summary>
  [CreateNodeMenu("Quest/Request Start Campaign")]
  [NodeTint("#8B7D6B")]
  [NodeWidth(400)]
  public sealed class RequestStartCampaignNode : VisualGraphNodeBase
  {
    [Input] public NodePort Input;
    [Output] public NodePort Output;

    [BoxGroup("Campaign")]
    [InfoBox("The campaign definition to start. Can be loaded via Addressables.")]
    [Required]
    public CampaignAssetReference Campaign;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

