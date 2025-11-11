using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Queue for managing node execution order.
    /// </summary>
    public sealed class NodeExecutionQueue
    {
        private readonly Queue<string> _queue = new();

        /// <summary>Enqueue a node for execution</summary>
        public void Enqueue(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId))
                _queue.Enqueue(nodeId);
        }

        /// <summary>Try to dequeue next node</summary>
        public bool TryDequeue(out string nodeId)
        {
            return _queue.TryDequeue(out nodeId);
        }

        /// <summary>Get current queue size</summary>
        public int Count => _queue.Count;
    }
}

