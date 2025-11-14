using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
  /// <summary>
  ///   Runtime graph definition with efficient lookup caches.
  /// </summary>
  public interface IRuntimeGraphDefinition
  {
    /// <summary>Unique graph identifier</summary>
    string GraphId { get; }

    /// <summary>Graph domain (Quest, Dialogue, etc.)</summary>
    string GraphDomain { get; }
    /// <summary>Maximum number of execution steps</summary>
    int MaxExecutionSteps { get; }

    /// <summary>Event subscriptions for this graph</summary>
    IReadOnlyList<RuntimeSubscriptionDefinition> Subscriptions { get; }

    /// <summary>All nodes in this graph</summary>
    IReadOnlyList<RuntimeNodeDefinition> Nodes { get; }

    /// <summary>All connections between nodes</summary>
    IReadOnlyList<RuntimeConnectionDefinition> Connections { get; }

    /// <summary>Fast node lookup by ID</summary>
    RuntimeNodeDefinition GetNodeById(string nodeId);

    /// <summary>Get all outgoing connections from a node</summary>
    IEnumerable<RuntimeConnectionDefinition> GetConnectionsFrom(string nodeId);
  }
}