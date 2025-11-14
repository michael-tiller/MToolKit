using MToolKit.Runtime.Core.Types;
using MToolKit.Runtime.VisualGraphs.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Quest entry node that subscribes to specific MessagePipe message types.
  /// </summary>
  [CreateNodeMenu("Quest/On Event")]
  [NodeTint("#6B9B6E")]
  [NodeWidth(500)]
  public sealed class QuestOnEventNode : EntryNodeBase
  {
    [BoxGroup("Message Type")]
    [Required]
    [LabelText("Message Type")]
    [Tooltip("MessagePipe message type to listen for (must implement IGameMessage)")]
    public MessageTypeReference MessageType = new();

    [BoxGroup("Options")]
    [Tooltip("Optional: Only process messages from this domain/context")]
    public string DomainFilter;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}