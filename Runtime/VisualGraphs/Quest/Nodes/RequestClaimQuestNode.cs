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
  ///   Node that publishes a ClaimQuestRequestMessage via GameMessageBroker.
  ///   QuestManager subscribes to this message, claims the quest, and fires QuestClaimedMessage for reward systems.
  /// </summary>
  [CreateNodeMenu("Quest/Request Claim Quest")]
  [NodeTint("#9B8B5C")]
  [NodeWidth(400)]
  public sealed class RequestClaimQuestNode : VisualGraphNodeBase
  {
    [Input] public NodePort Input;
    [Output] public NodePort Output;

    [BoxGroup("Quest")]
    [InfoBox("The quest definition to claim. Can be loaded via Addressables.")]
    [Required]
    public QuestAssetReference Quest;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

