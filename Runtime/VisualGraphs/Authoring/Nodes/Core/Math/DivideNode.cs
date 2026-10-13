using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{

  /// <summary>Divides two typed numeric state values, using a logged fallback for a zero divisor.</summary>
  [CreateNodeMenu("Core/Math/Divide")]
  [NodeWidth(300)]
  public sealed class DivideNode : VisualGraphNodeBase
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
    /// <summary>Numeric result used for a zero divisor; Int mode truncates toward zero.</summary>
    [BoxGroup("Operands")] [Tooltip("Fallback for a zero divisor; integer mode truncates.")] public float Fallback;
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
