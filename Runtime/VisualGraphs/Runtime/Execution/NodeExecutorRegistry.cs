using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Registry for all node executors. Used for validation and runtime execution.
    /// </summary>
    public sealed class NodeExecutorRegistry
    {
        private readonly Dictionary<string, IGraphNodeExecutor> _byType = new();

        /// <summary>All known node type names</summary>
        public IReadOnlyCollection<string> KnownTypes => _byType.Keys;

        /// <summary>Register an executor</summary>
        public void Register(IGraphNodeExecutor executor)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));
            
            if (string.IsNullOrEmpty(executor.NodeType))
                throw new ArgumentException("Executor NodeType cannot be null or empty", nameof(executor));

            _byType[executor.NodeType] = executor;
        }

        /// <summary>Get executor by node type name</summary>
        public IGraphNodeExecutor Get(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType))
                throw new ArgumentException("NodeType cannot be null or empty", nameof(nodeType));

            if (!_byType.TryGetValue(nodeType, out var executor))
                throw new KeyNotFoundException($"No executor registered for node type: {nodeType}");

            return executor;
        }

        /// <summary>Check if executor exists for node type</summary>
        public bool HasExecutor(string nodeType)
        {
            return !string.IsNullOrEmpty(nodeType) && _byType.ContainsKey(nodeType);
        }
    }
}

