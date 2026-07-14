using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Math
{
  /// <summary>
  ///   Adds two float state values and writes the sum to a third state key.
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
    [Tooltip("State key holding the first float operand")]
    public string AKey = "a";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("State key holding the second float operand")]
    public string BKey = "b";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the sum is written to")]
    public string ResultKey = "result";

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
