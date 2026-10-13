using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{

  /// <summary>Subtracts two typed numeric state values and writes the difference.</summary>
  [CreateNodeMenu("Core/Math/Subtract")]
  [NodeWidth(300)]
  public sealed class SubtractNode : VisualGraphNodeBase
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
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
