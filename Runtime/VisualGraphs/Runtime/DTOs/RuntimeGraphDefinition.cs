using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Serializable runtime graph definition with lookup caches.
    /// </summary>
    [Serializable]
    public sealed class RuntimeGraphDefinition : IRuntimeGraphDefinition
    {
        [SerializeField] public string GraphId;
        [SerializeField] public string GraphDomain;
        [SerializeField] public List<RuntimeSubscriptionDefinition> Subscriptions = new();
        [SerializeField] public List<RuntimeNodeDefinition> Nodes = new();
        [SerializeField] public List<RuntimeConnectionDefinition> Connections = new();

        // Cached lookups (not serialized)
        private Dictionary<string, RuntimeNodeDefinition> _nodeById;
        private Dictionary<string, List<RuntimeConnectionDefinition>> _connectionsByFrom;

        string IRuntimeGraphDefinition.GraphId => GraphId;
        string IRuntimeGraphDefinition.GraphDomain => GraphDomain;
        IReadOnlyList<RuntimeSubscriptionDefinition> IRuntimeGraphDefinition.Subscriptions => Subscriptions;
        IReadOnlyList<RuntimeNodeDefinition> IRuntimeGraphDefinition.Nodes => Nodes;
        IReadOnlyList<RuntimeConnectionDefinition> IRuntimeGraphDefinition.Connections => Connections;

        /// <summary>
        /// Build or rebuild lookup caches for efficient queries.
        /// Call this after deserialization or after modifying nodes/connections.
        /// </summary>
        public void BuildLookupCaches()
        {
            _nodeById = new Dictionary<string, RuntimeNodeDefinition>(Nodes.Count);
            foreach (var n in Nodes)
            {
                if (!string.IsNullOrEmpty(n.NodeId))
                    _nodeById[n.NodeId] = n;
            }

            _connectionsByFrom = new Dictionary<string, List<RuntimeConnectionDefinition>>();
            foreach (var c in Connections)
            {
                if (string.IsNullOrEmpty(c.FromNodeId)) continue;
                
                if (!_connectionsByFrom.TryGetValue(c.FromNodeId, out var list))
                {
                    list = new List<RuntimeConnectionDefinition>();
                    _connectionsByFrom[c.FromNodeId] = list;
                }
                list.Add(c);
            }
        }

        public RuntimeNodeDefinition GetNodeById(string nodeId)
        {
            if (_nodeById == null) BuildLookupCaches();
            return _nodeById != null && _nodeById.TryGetValue(nodeId, out var n) ? n : null;
        }

        public IEnumerable<RuntimeConnectionDefinition> GetConnectionsFrom(string nodeId)
        {
            if (_connectionsByFrom == null) BuildLookupCaches();
            return _connectionsByFrom != null && _connectionsByFrom.TryGetValue(nodeId, out var list) 
                ? list 
                : Enumerable.Empty<RuntimeConnectionDefinition>();
        }
    }
}

