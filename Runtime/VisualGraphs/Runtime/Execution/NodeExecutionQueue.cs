using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Execution
{
  /// <summary>
  ///   Queue for managing node execution order.
  /// </summary>
  public sealed class NodeExecutionQueue
  {
    private readonly Queue<string> queue = new();

    /// <summary>Get current queue size</summary>
    public int Count => queue.Count;

    /// <summary>Enqueue a node for execution</summary>
    public void Enqueue(string nodeId)
    {
      if (!string.IsNullOrEmpty(nodeId))
        queue.Enqueue(nodeId);
    }

    /// <summary>Try to dequeue next node</summary>
    public bool TryDequeue(out string nodeId)
    {
      return queue.TryDequeue(out nodeId);
    }
  }
}