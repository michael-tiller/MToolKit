using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Node that starts another quest graph.
  ///   Example of using asset references for graph-to-graph communication.
  /// </summary>
  [CreateNodeMenu("Quest/Start Graph")]
  [NodeTint("#B8905C")]
  public sealed class QuestStartGraphNode : VisualGraphNodeBase
  {
    [Input] public NodePort Input;
    [Output] public NodePort Output;

    [BoxGroup("Target Graph")]
    [InfoBox("This is an example of using asset references - not string IDs!")]
    [Required]
    [Tooltip("The quest graph to start")]
    public QuestGraphAsset TargetGraph; // ✅ Direct asset reference!

    [BoxGroup("Target Graph")]
    [InfoBox("Optional: Use Addressables for dynamic loading")]
#if UNITY_ADDRESSABLES
    public AssetReference TargetGraphAddressable; // ✅ Or Addressable reference!
#endif

    [BoxGroup("Options")]
    [Tooltip("If target is already running, restart it from beginning")]
    public bool RestartIfRunning = false;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

