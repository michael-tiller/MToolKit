using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.MessageBus;
using GameMessageBroker = MToolKit.Runtime.MessageBus.GameMessageBroker;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Dialogue.Messages;
using R3;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Executors
{
  /// <summary>
  ///   Executor for DialogueLineNode.
  ///   Displays dialogue and waits for acknowledgment before continuing.
  /// </summary>
  public sealed class DialogueLineNodeExecutor : IGraphNodeExecutor
  {
    #region IGraphNodeExecutor Members

    public string NodeType => "DialogueLineNode";

    public async UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters (field names match the node class exactly)
      var speakerId = node.Parameters.TryGetValue("SpeakerId", out var sid) ? sid as string : "Unknown";
      var text = node.Parameters.TryGetValue("Text", out var txt) ? txt as string : "";
      var localizationKey = node.Parameters.TryGetValue("LocalizationKey", out var lk) ? lk as string : null;

      // Publish dialogue show message to display the dialogue line
      context.Emit(new DialogueShowMessage(text, speakerId, localizationKey, graph.GraphId));

      // Wait for player to progress (dialogue view will publish DialogueProgressMessage when Next is clicked)
      await WaitForDialogueProgressAsync(graph.GraphId, ct);

      // Continue to connected nodes after player has progressed
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }

    private async UniTask WaitForDialogueProgressAsync(string graphId, CancellationToken ct)
    {
      var tcs = new UniTaskCompletionSource();
      IDisposable subscription = null;

      try
      {
        var subscriber = GameMessageBroker.GetSubscriber<DialogueProgressMessage>();
        if (subscriber != null)
        {
          subscription = subscriber.Subscribe(message =>
          {
            // Only continue if this message is for our graph (or no graph ID specified, meaning any)
            if (string.IsNullOrEmpty(message.GraphId) || message.GraphId == graphId)
            {
              tcs.TrySetResult();
            }
          });
        }

        // Wait for the progress message or cancellation
        // Use AttachExternalCancellation to handle cancellation
        try
        {
          await tcs.Task.AttachExternalCancellation(ct);
        }
        catch (OperationCanceledException)
        {
          // Cancellation handled - subscription will be disposed in finally
          return;
        }
      }
      finally
      {
        subscription?.Dispose();
      }
    }

    #endregion
  }
}