using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic
{
  /// <summary>
  ///   Computes the exclusive-or of two bool state values and writes the result to a state key.
  /// </summary>
  [CreateNodeMenu("Core/Logic/Xor")]
  [NodeTint("#5C5C8B")]
  [NodeWidth(300)]
  public sealed class XorNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("First bool state key. Missing/unresolvable treated as false.")]
    public string LeftKey = "left";

    [BoxGroup("Operands")]
    [Required]
    [Tooltip("Second bool state key. Missing/unresolvable treated as false.")]
    public string RightKey = "right";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the XOR result is written to")]
    public string ResultKey = "result";

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
