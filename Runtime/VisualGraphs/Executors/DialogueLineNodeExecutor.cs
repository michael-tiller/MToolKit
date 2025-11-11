using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
    /// <summary>
    /// Executor for DialogueLineNode.
    /// Displays dialogue and waits for acknowledgment before continuing.
    /// </summary>
    public sealed class DialogueLineNodeExecutor : IGraphNodeExecutor
    {
        public string NodeType => "DialogueLineNode";

        public async UniTask ExecuteAsync(
            IRuntimeGraphDefinition graph,
            RuntimeNodeDefinition node,
            IGraphState state,
            IEventMessage message,
            GraphNodeExecutionContext context,
            CancellationToken ct = default)
        {
            // Extract parameters
            var speakerId = node.Parameters.TryGetValue("speakerId", out var sid) ? sid as string : "Unknown";
            var text = node.Parameters.TryGetValue("text", out var txt) ? txt as string : "";
            var localizationKey = node.Parameters.TryGetValue("localizationKey", out var lk) ? lk as string : null;

            // TODO: Resolve IDialogueUIService from context and show line
            // var dialogueUI = context.Resolve<IDialogueUIService>();
            // await dialogueUI.ShowLineAsync(speakerId, text, ct);

            // For now, simulate delay
            await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken: ct);

            // Emit event
            context.Emit(new BasicEventMessage(
                "Dialogue.LineShown",
                "Dialogue",
                message.SequenceId,
                new { GraphId = graph.GraphId, Speaker = speakerId, Text = text },
                null));

            // Continue to connected nodes
            foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
            {
                context.EnqueueNext(connection.ToNodeId);
            }
        }
    }
}

