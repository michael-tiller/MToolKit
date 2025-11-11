using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Basic implementation of IEventMessage.
    /// </summary>
    public sealed class BasicEventMessage : IEventMessage
    {
        public string Type { get; }
        public string Domain { get; }
        public long SequenceId { get; }
        public object Payload { get; }
        public IReadOnlyDictionary<string, object> Metadata { get; }

        public BasicEventMessage(
            string type,
            string domain,
            long sequenceId,
            object payload = null,
            IReadOnlyDictionary<string, object> metadata = null)
        {
            Type = type;
            Domain = domain ?? string.Empty;
            SequenceId = sequenceId;
            Payload = payload;
            Metadata = metadata ?? new Dictionary<string, object>();
        }
    }
}

