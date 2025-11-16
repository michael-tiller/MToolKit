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
  ///   Node that starts a quest via QuestManager.
  ///   Uses QuestAssetReference to reference the quest definition.
  /// </summary>
  [CreateNodeMenu("Quest/Start Quest")]
  [NodeTint("#B8905C")]
  [NodeWidth(400)]
  public sealed class StartQuestNode : VisualGraphNodeBase
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

