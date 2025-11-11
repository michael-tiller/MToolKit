using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Executor for a specific node type. Controls node execution and continuation.
    /// </summary>
    public interface IGraphNodeExecutor
    {
        /// <summary>Node type this executor handles (must match node class name)</summary>
        string NodeType { get; }
        
        /// <summary>
        /// Execute a node. Executor decides whether to continue to connected nodes.
        /// Use context.EnqueueNext() to continue execution.
        /// </summary>
        UniTask ExecuteAsync(
            IRuntimeGraphDefinition graph,
            RuntimeNodeDefinition node,
            IGraphState state,
            IEventMessage message,
            GraphNodeExecutionContext context,
            CancellationToken ct = default);
    }
}

