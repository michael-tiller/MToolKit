using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{
  /// <summary>
  ///   Linearly interpolates between two numeric state values by a float t state value (Mathf.Lerp
  ///   semantics — t implicitly clamped [0,1]) and writes the result to a fourth state key.
  /// </summary>
  [CreateNodeMenu("Core/Math/Lerp")]
  [NodeTint("#8B5C5C")]
  [NodeWidth(300)]
  public sealed class LerpNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the start value")]
    public string AKey = "a";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the end value")]
    public string BKey = "b";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the interpolation factor (clamped [0,1])")]
    public string TKey = "t";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the interpolated value is written to")]
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
