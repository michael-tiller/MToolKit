using System;
using MToolKit.Runtime.Core.Types;

namespace MToolKit.Runtime.VisualGraphs.Runtime.DTOs
{
  /// <summary>
  ///   Defines a MessagePipe message subscription for a graph.
  /// </summary>
  [Serializable]
  public sealed class RuntimeSubscriptionDefinition
  {
    /// <summary>The IGameMessage type to subscribe to</summary>
    public MessageTypeReference MessageType;

    /// <summary>Optional domain/context filter (implementation-specific)</summary>
    public string DomainFilter;

    /// <summary>Whether an entry node MUST exist for this subscription</summary>
    public bool Required;

    public override string ToString()
    {
      var req = Required ? "[Required]" : "[Optional]";
      var domain = !string.IsNullOrEmpty(DomainFilter) ? $" ({DomainFilter})" : "";
      return $"{req} {MessageType?.Name ?? "(null)"}{domain}";
    }
  }
}