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
  ///   Node that publishes a StartQuestRequestMessage via GameMessageBroker.
  ///   QuestManager subscribes to this message and starts the quest.
  /// </summary>
  [CreateNodeMenu("Quest/Request Start Quest")]
  [NodeTint("#B8905C")]
  [NodeWidth(400)]
  public sealed class RequestStartQuestNode : VisualGraphNodeBase
  {
    [Input] public NodePort Input;
    [Output] public NodePort Output;

    [BoxGroup("Quest")]
    [InfoBox("The quest definition to start. Can be loaded via Addressables.")]
    [Required]
    public QuestAssetReference Quest;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

