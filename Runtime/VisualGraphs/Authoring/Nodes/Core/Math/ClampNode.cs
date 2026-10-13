using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{
  /// <summary>
  ///   Clamps a numeric state value between two numeric state bounds and writes the result to a fourth state key.
  /// </summary>
  [CreateNodeMenu("Core/Math/Clamp")]
  [NodeTint("#8B5C5C")]
  [NodeWidth(300)]
  public sealed class ClampNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the value to clamp")]
    public string ValueKey = "value";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the lower bound")]
    public string MinKey = "min";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the upper bound")]
    public string MaxKey = "max";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the clamped value is written to")]
    public string ResultKey = "result";

    /// <summary>Numeric operand and output type; Float preserves existing serialized graph behavior.</summary>
    [BoxGroup("Operands")]
    [Tooltip("Int or Float arithmetic; Float preserves existing graph behavior.")]
    public EGraphVariableType ValueType = EGraphVariableType.Float;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
