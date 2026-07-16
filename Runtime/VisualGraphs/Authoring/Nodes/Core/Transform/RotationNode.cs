using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Core.Transform
{
  /// <summary>
  ///   Reads a Vector3 state value and writes it to another state key (identity/pass-through by default).
  ///   Plain graph-state get/set — no live-GameObject binding (no owner-binding mechanism exists today).
  /// </summary>
  [CreateNodeMenu("Core/Transform/Rotation")]
  [NodeTint("#5C8B6B")]
  [NodeWidth(300)]
  public sealed class RotationNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Source")]
    [Required]
    [Tooltip("Vector3 state key to read")]
    public string SourceKey = "rotation";

    [BoxGroup("Destination")]
    [Required]
    [Tooltip("Vector3 state key to write")]
    public string DestinationKey = "result";

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}
