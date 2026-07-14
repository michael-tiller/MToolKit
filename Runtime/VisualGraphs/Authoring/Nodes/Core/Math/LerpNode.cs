using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{
  /// <summary>
  ///   Linearly interpolates between two float state values by a float t state value (Mathf.Lerp
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

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
