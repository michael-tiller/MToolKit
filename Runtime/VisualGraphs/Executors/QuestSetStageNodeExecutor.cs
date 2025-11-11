using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
    /// <summary>
    /// Executor for QuestSetStageNode.
    /// Sets a quest stage and continues to connected nodes.
    /// </summary>
    public sealed class QuestSetStageNodeExecutor : IGraphNodeExecutor
    {
        public string NodeType => "QuestSetStageNode";

        public UniTask ExecuteAsync(
            IRuntimeGraphDefinition graph,
            RuntimeNodeDefinition node,
            IGraphState state,
            IEventMessage message,
            GraphNodeExecutionContext context,
            CancellationToken ct = default)
        {
            // Extract parameters
            var stageKey = node.Parameters.TryGetValue("stageKey", out var sk) ? sk as string : "unknown";
            var stageValue = node.Parameters.TryGetValue("stageValue", out var sv) ? Convert.ToInt32(sv) : 0;

            // Set stage in state
            state.Set(stageKey, stageValue);

            // Emit event
            context.Emit(new BasicEventMessage(
                "Quest.StageSet",
                "Quest",
                message.SequenceId,
                new { QuestId = graph.GraphId, StageKey = stageKey, StageValue = stageValue },
                null));

            // Continue to connected nodes
            foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
            {
                context.EnqueueNext(connection.ToNodeId);
            }

            return UniTask.CompletedTask;
        }
    }
}

