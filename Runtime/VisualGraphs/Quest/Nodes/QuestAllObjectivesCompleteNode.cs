using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Checks if all required quest objectives are complete.
  ///   Branches based on whether quest can be completed.
  /// </summary>
  [CreateNodeMenu("Quest/Objective/Check All Complete")]
  [NodeTint("#8B6B93")]
  public sealed class QuestAllObjectivesCompleteNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if all required objectives are complete")]
    public NodeConnection AllComplete;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if some objectives are incomplete")]
    public NodeConnection Incomplete;

    [BoxGroup("Options")]
    [Tooltip("If true, emits QuestCompleteMessage to MessagePipe when all complete")]
    public bool EmitCompleteEvent = true;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

