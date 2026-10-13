using Sirenix.OdinInspector;
using MToolKit.Runtime.VisualGraphs.Variables;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{
  /// <summary>
  ///   Adds two numeric state values and writes the sum to a third state key.
  /// </summary>
  [CreateNodeMenu("Core/Math/Add")]
  [NodeTint("#8B5C5C")]
  [NodeWidth(300)]
  public sealed class AddNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the first typed operand")]
    public string AKey = "a";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the second typed operand")]
    public string BKey = "b";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the sum is written to")]
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
