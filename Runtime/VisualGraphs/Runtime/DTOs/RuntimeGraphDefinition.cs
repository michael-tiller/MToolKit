using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Runtime.DTOs
{
  /// <summary>
  ///   Serializable runtime graph definition with lookup caches.
  /// </summary>
  [Serializable]
  public sealed class RuntimeGraphDefinition : IRuntimeGraphDefinition
  {
    public string GraphId;
    public string GraphDomain;
    [field: SerializeField]
    [MinValue(1)]
    public int MaxExecutionSteps { get; set; } = 1024;
    public List<RuntimeSubscriptionDefinition> Subscriptions = new();
    public List<RuntimeNodeDefinition> Nodes = new();
    public List<RuntimeConnectionDefinition> Connections = new();
    private Dictionary<string, List<RuntimeConnectionDefinition>> connectionsByFrom;

    // Cached lookups (not serialized)
    private Dictionary<string, RuntimeNodeDefinition> nodeById;

    string IRuntimeGraphDefinition.GraphId => GraphId;
    string IRuntimeGraphDefinition.GraphDomain => GraphDomain;
    IReadOnlyList<RuntimeSubscriptionDefinition> IRuntimeGraphDefinition.Subscriptions => Subscriptions;
    IReadOnlyList<RuntimeNodeDefinition> IRuntimeGraphDefinition.Nodes => Nodes;
    IReadOnlyList<RuntimeConnectionDefinition> IRuntimeGraphDefinition.Connections => Connections;

    public RuntimeNodeDefinition GetNodeById(string nodeId)
    {
      if (nodeById == null) BuildLookupCaches();
      return nodeById != null && nodeById.TryGetValue(nodeId, out var n) ? n : null;
    }

    public IEnumerable<RuntimeConnectionDefinition> GetConnectionsFrom(string nodeId)
    {
      if (connectionsByFrom == null) BuildLookupCaches();
      if (connectionsByFrom != null && connectionsByFrom.TryGetValue(nodeId, out var list))
        return list;
      // Return empty array instead of Enumerable.Empty to avoid IL2CPP issues
      return Array.Empty<RuntimeConnectionDefinition>();
    }

    /// <summary>
    ///   Build or rebuild lookup caches for efficient queries.
    ///   Call this after deserialization or after modifying nodes/connections.
    /// </summary>
    public void BuildLookupCaches()
    {
      nodeById = new Dictionary<string, RuntimeNodeDefinition>(Nodes.Count);
      foreach (var n in Nodes)
        if (!string.IsNullOrEmpty(n.NodeId))
          nodeById[n.NodeId] = n;

      connectionsByFrom = new Dictionary<string, List<RuntimeConnectionDefinition>>();
      foreach (var c in Connections)
      {
        if (string.IsNullOrEmpty(c.FromNodeId)) continue;

        if (!connectionsByFrom.TryGetValue(c.FromNodeId, out var list))
        {
          list = new List<RuntimeConnectionDefinition>();
          connectionsByFrom[c.FromNodeId] = list;
        }
        list.Add(c);
      }
    }
  }
}