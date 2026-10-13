using MToolKit.Runtime.Core.Types;

namespace MToolKit.Runtime.VisualGraphs.Authoring
{
    /// <summary>Exposes an authored entry point without coupling the toolkit to a game's node assembly.</summary>
    public interface IEventSubscriptionSource
    {
        /// <summary>Message type received by this entry point.</summary>
        MessageTypeReference MessageType { get; }
        /// <summary>Optional exact domain; empty receives every domain of the message type.</summary>
        string DomainFilter { get; }
    }
}
