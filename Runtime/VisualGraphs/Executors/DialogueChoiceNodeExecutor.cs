using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for DialogueChoiceNode.
  ///   Presents choices and continues to selected branch.
  /// </summary>
  public sealed class DialogueChoiceNodeExecutor : IGraphNodeExecutor
  {
    #region IGraphNodeExecutor Members

    public string NodeType => nameof(DialogueChoiceNodeExecutor);

    public async UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IEventMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract choices parameter
      // Note: In real implementation, you'd need to extract the choices list properly
      // This is simplified for demonstration

      // TODO: Resolve IDialogueUIService and show choices
      // var dialogueUI = context.Resolve<IDialogueUIService>();
      // int selectedIndex = await dialogueUI.ShowChoicesAsync(choices, ct);

      // For now, simulate choice selection
      await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
      var selectedIndex = 0; // Simulate selecting first choice

      // Emit event
      context.Emit(new BasicEventMessage(
        "Dialogue.ChoiceSelected",
        "Dialogue",
        message.SequenceId,
        new { graph.GraphId, ChoiceIndex = selectedIndex }));

      // Continue to selected choice's output
      // In real implementation, you'd need to follow the dynamic port connection
      // For now, just continue to all connected nodes
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        // TODO: Filter by choice index
        context.EnqueueNext(connection.ToNodeId);
    }

    #endregion
  }
}