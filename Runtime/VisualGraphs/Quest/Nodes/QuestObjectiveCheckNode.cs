using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Checks if a quest objective is complete and branches execution.
  ///   Output "Complete" executes if objective done, "Incomplete" otherwise.
  ///   Uses GUID-based references (safe, no typos!).
  /// </summary>
  [CreateNodeMenu("Quest/Objective/Check Complete")]
  [NodeTint("#B8905C")]
  public sealed class QuestObjectiveCheckNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if objective is complete")]
    public NodeConnection Complete;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if objective is incomplete")]
    public NodeConnection Incomplete;

    [BoxGroup("Objective")]
    [Required]
    [InlineEditor]
    [Tooltip("Quest objective to check (references QuestObjective asset by GUID)")]
    public QuestObjective Objective;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

