using System;

namespace MToolKit.Runtime.VisualGraphs.Runtime.DTOs
{
  /// <summary>
  ///   Defines an event subscription for a graph.
  /// </summary>
  [Serializable]
  public sealed class RuntimeSubscriptionDefinition
  {
    /// <summary>Event type to subscribe to</summary>
    public string EventType;

    /// <summary>Event domain filter (empty = match all domains)</summary>
    public string EventDomain;
  }
}