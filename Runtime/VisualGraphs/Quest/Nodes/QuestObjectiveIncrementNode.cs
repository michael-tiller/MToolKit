using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Increments progress for a specific quest objective.
  ///   Example: Kill goblin → Increment "Kill Goblins" objective by 1
  ///   Uses GUID-based references (safe, no typos!).
  /// </summary>
  [CreateNodeMenu("Quest/Objective/Increment Progress")]
  [NodeTint("#6B9B6E")]
  [NodeWidth(400)]
  public sealed class QuestObjectiveIncrementNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Objective")]
    [Required]
    [InlineEditor]
    [Tooltip("Quest objective to increment (references QuestObjective asset by GUID)")]
    public QuestObjective Objective;

    [BoxGroup("Objective")]
    [MinValue(1)]
    [Tooltip("Amount to increment progress by (usually 1)")]
    public int Amount = 1;

    [BoxGroup("Options")]
    [Tooltip("If true, emits QuestObjectiveProgressMessage to MessagePipe")]
    public bool EmitProgressEvent = true;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

