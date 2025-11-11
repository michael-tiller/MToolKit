using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring.Nodes
{
    /// <summary>
    /// Quest entry node that subscribes to specific events.
    /// </summary>
    [Node.CreateNodeMenu("Quest/On Event")]
    [Node.NodeTint("#4CAF50")]
    public sealed class QuestOnEventNode : EntryNodeBase, IEventSubscribedNode
    {
        [BoxGroup("Event")]
        [Tooltip("Event type to listen for (e.g., 'Quest.Started')")]
        public string eventType = "Quest.Started";

        [BoxGroup("Event")]
        [Tooltip("Event domain filter (leave empty for all domains)")]
        public string eventDomain = "Quest";

        string IEventSubscribedNode.EventType => eventType;
        string IEventSubscribedNode.EventDomain => eventDomain;

        public override object GetValue(NodePort port)
        {
            return null;
        }
    }
}

