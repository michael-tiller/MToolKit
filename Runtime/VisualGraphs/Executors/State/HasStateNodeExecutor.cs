using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors.State
{
    /// <summary>Checks existence through the same scoped resolver as all variable reads.</summary>
    public sealed class HasStateNodeExecutor : IGraphNodeExecutor
    {
        /// <summary>Authoring node name.</summary>
        public string NodeType => "HasStateNode";
        /// <summary>Writes a Boolean result and continues execution.</summary>
        /// <param name="graph">The executing graph definition.</param>
        /// <param name="node">The authored existence check.</param>
        /// <param name="state">The runner's local state.</param>
        /// <param name="message">The triggering message.</param>
        /// <param name="context">Continuation, scope services and event emission.</param>
        /// <param name="ct">Execution cancellation.</param>
        /// <returns>A completed synchronous operation.</returns>
        public UniTask Execute(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, IGraphState state,
            IGameMessage message, GraphNodeExecutionContext context, CancellationToken ct = default)
        {
            var values = new GraphNodeValues(graph, state, context);
            var key = node.Parameters.TryGetValue("Key", out var source) ? source as string : null;
            var result = node.Parameters.TryGetValue("ResultKey", out var target) ? target as string : null;
            values.Set(result, values.Contains(key));
            values.Continue(node);
            return UniTask.CompletedTask;
        }
    }
}
