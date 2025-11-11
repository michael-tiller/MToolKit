namespace MToolKit.Runtime.VisualGraphs.Authoring
{
    /// <summary>
    /// Interface for nodes that subscribe to specific events.
    /// Used by the exporter to collect graph event subscriptions.
    /// </summary>
    public interface IEventSubscribedNode
    {
        /// <summary>Event type to subscribe to</summary>
        string EventType { get; }
        
        /// <summary>Event domain filter (can be empty for global)</summary>
        string EventDomain { get; }
    }
}

