using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Logic
{
  /// <summary>
  ///   Negates a bool state value and writes the result to a state key.
  /// </summary>
  [CreateNodeMenu("Core/Logic/Not")]
  [NodeTint("#5C5C8B")]
  [NodeWidth(300)]
  public sealed class NotNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Operand")]
    [Required]
    [Tooltip("Bool state key to negate. Missing/unresolvable treated as false.")]
    public string Key = "value";

    [BoxGroup("Result")]
    [Required]
    [Tooltip("State key the negated value is written to")]
    public string ResultKey = "result";

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
