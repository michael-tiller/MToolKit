using MToolKit.Runtime.Core.Types;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Message
{
  /// <summary>
  ///   Checks if the current message is of a specific type.
  ///   Useful for handling multiple message types in the same graph.
  /// </summary>
  [CreateNodeMenu("Message/Check Type")]
  [NodeTint("#8B6B93")]
  [NodeWidth(400)]
  public sealed class MessageTypeCheckNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if message type matches")]
    public NodeConnection Matches;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if message type doesn't match")]
    public NodeConnection DoesntMatch;

    [BoxGroup("Type")]
    [Required]
    [Tooltip("Expected message type to check against")]
    public MessageTypeReference ExpectedType = new();

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

