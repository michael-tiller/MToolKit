using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{

  /// <summary>Multiplies numeric state values when a Boolean condition is true; otherwise copies the first operand.</summary>
  [CreateNodeMenu("Core/Math/ConditionalMultiply")]
  [NodeWidth(300)]
  public sealed class ConditionalMultiplyNode : VisualGraphNodeBase
  {
    /// <summary>Incoming execution connection.</summary>
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;
    /// <summary>Outgoing execution connections continued after the operation.</summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    /// <summary>Scoped key of the first typed operand.</summary>
    [BoxGroup("Operands")] [Required] public string AKey = "a";
    /// <summary>Scoped key of the second typed operand.</summary>
    [BoxGroup("Operands")] [Required] public string BKey = "b";
    /// <summary>Numeric operand and output type: Int or Float.</summary>
    [BoxGroup("Operands")] public EGraphVariableType ValueType = EGraphVariableType.Float;
    /// <summary>Scoped Boolean key selecting whether the conditional operation applies.</summary>
    [BoxGroup("Operands")] [Required] public string ConditionKey = "condition";
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
