using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes.Quest
{
  /// <summary>
  ///   Quest entry node that subscribes to specific events.
  /// </summary>
  [CreateNodeMenu("Quest/On Event")]
  [NodeTint("#4CAF50")]
  public sealed class QuestOnEventNode : EntryNodeBase, IEventSubscribedNode
  {
    [BoxGroup("Event")]
    [Tooltip("Event type to listen for (e.g., 'Quest.Started')")]
    public string EventType = "Quest.Started";

    [BoxGroup("Event")]
    [Tooltip("Event domain filter (leave empty for all domains)")]
    public string EventDomain = "Quest";

    string IEventSubscribedNode.EventType => EventType;
    string IEventSubscribedNode.EventDomain => EventDomain;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}