using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic
{

  /// <summary>Copies one of two typed state values according to a Boolean condition.</summary>
  [CreateNodeMenu("Core/Logic/Select")]
  [NodeWidth(300)]
  public sealed class SelectNode : VisualGraphNodeBase
  {
    /// <summary>Incoming execution connection.</summary>
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;
    /// <summary>Outgoing execution connections continued after the operation.</summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    /// <summary>Scoped Boolean key selecting whether the conditional operation applies.</summary>
    [BoxGroup("Operands")] [Required] public string ConditionKey = "condition";
    /// <summary>Scoped key read when the condition is true.</summary>
    [BoxGroup("Operands")] [Required] public string TrueKey = "trueValue";
    /// <summary>Scoped key read when the condition is false.</summary>
    [BoxGroup("Operands")] [Required] public string FalseKey = "falseValue";
    /// <summary>Declared type shared by both source values and the destination.</summary>
    [BoxGroup("Operands")] public EGraphVariableType ValueType = EGraphVariableType.Float;
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
