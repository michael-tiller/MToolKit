using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Core event message interface for graph event routing.
    /// </summary>
    public interface IEventMessage
    {
        /// <summary>Event type identifier (e.g., "Quest.Started", "Dialogue.LineShown")</summary>
        string Type { get; }
        
        /// <summary>Domain for scoping events (e.g., "Quest", "Dialogue", or empty for global)</summary>
        string Domain { get; }
        
        /// <summary>Sequence ID for idempotent event handling</summary>
        long SequenceId { get; }
        
        /// <summary>Event payload data</summary>
        object Payload { get; }
        
        /// <summary>Additional metadata for the event</summary>
        IReadOnlyDictionary<string, object> Metadata { get; }
    }
}

