using System;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Defines an event subscription for a graph.
    /// </summary>
    [Serializable]
    public sealed class RuntimeSubscriptionDefinition
    {
        /// <summary>Event type to subscribe to</summary>
        [SerializeField] public string EventType;
        
        /// <summary>Event domain filter (empty = match all domains)</summary>
        [SerializeField] public string EventDomain;
    }
}

