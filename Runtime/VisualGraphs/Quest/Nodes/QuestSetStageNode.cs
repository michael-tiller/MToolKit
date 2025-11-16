using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Sets a quest stage value and continues to next node.
  /// </summary>
  [CreateNodeMenu("Quest/Set Stage")]
  [NodeTint("#6B8FA8")]
  [NodeWidth(400)]
  public sealed class QuestSetStageNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Stage")]
    [Tooltip("Stage key to set (e.g., 'mainQuest', 'subQuest1')")]
    public string StageKey = "mainQuest";

    [BoxGroup("Stage")]
    [Tooltip("Stage value to set (e.g., stage number)")]
    public int StageValue = 1;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}