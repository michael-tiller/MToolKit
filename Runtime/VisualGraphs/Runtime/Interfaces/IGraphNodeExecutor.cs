using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
  /// <summary>
  ///   Executor for a specific node type. Controls node execution and continuation.
  /// </summary>
  public interface IGraphNodeExecutor
  {
    /// <summary>Node type this executor handles (must match node class name)</summary>
    string NodeType { get; }

    /// <summary>
    ///   Execute a node. Executor decides whether to continue to connected nodes.
    ///   Use context.EnqueueNext() to continue execution.
    /// </summary>
    UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default);
  }
}