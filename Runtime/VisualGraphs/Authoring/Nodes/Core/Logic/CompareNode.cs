using System.Collections.Generic;
using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic
{
  /// <summary>Numeric comparison operators, with equality and inequality also supported for Boolean values.</summary>
  public enum EGraphComparisonOperator
  {
    /// <summary>The first operand is greater than the second.</summary>
    GreaterThan,
    /// <summary>The first operand is greater than or equal to the second.</summary>
    GreaterOrEqual,
    /// <summary>The first operand is less than the second.</summary>
    LessThan,
    /// <summary>The first operand is less than or equal to the second.</summary>
    LessOrEqual,
    /// <summary>The operands are equal.</summary>
    Equal,
    /// <summary>The operands differ.</summary>
    NotEqual
  }

  /// <summary>Compares typed numeric or Boolean state values and writes a Boolean result.</summary>
  [CreateNodeMenu("Core/Logic/Compare")]
  [NodeWidth(300)]
  public sealed class CompareNode : VisualGraphNodeBase
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
    /// <summary>Operand type: Int, Float or Bool.</summary>
    [BoxGroup("Operands")] public EGraphVariableType ValueType = EGraphVariableType.Float;
    /// <summary>Comparison to perform; Boolean operands support equality and inequality only.</summary>
    [BoxGroup("Operands")] public EGraphComparisonOperator Operator = EGraphComparisonOperator.GreaterThan;
    /// <summary>Scoped destination key receiving the computed result.</summary>
    [BoxGroup("Result")] [Required] public string ResultKey = "result";

    /// <summary>Returns no data-port value; this node exchanges operands through graph state.</summary>
    /// <param name="port">Authoring port whose value was requested.</param>
    /// <returns>Always null.</returns>
    public override object GetValue(NodePort port) => null;
  }
}
