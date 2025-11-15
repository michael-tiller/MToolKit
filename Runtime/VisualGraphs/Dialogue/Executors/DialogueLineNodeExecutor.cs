using System;
using System.Collections.Generic;
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
using UnityEngine;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Executors
{
  /// <summary>
  ///   Executor for DialogueLineNode.
  ///   Displays dialogue and waits for acknowledgment before continuing.
  /// </summary>
  public sealed class DialogueLineNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<DialogueLineNodeExecutor>().ForFeature("VisualGraphs.Dialogue"));
    private static ILogger log => logLazy.Value ?? Logger.None;

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

      // Extract timing parameters
      var minDisplaySeconds = node.Parameters.TryGetValue("MinDisplaySeconds", out var minTime) && minTime is float min ? min : 0f;
      var autoAdvance = node.Parameters.TryGetValue("AutoAdvance", out var auto) && auto is bool autoVal ? autoVal : false;
      var autoAdvanceDelaySeconds = node.Parameters.TryGetValue("AutoAdvanceDelaySeconds", out var delay) && delay is float delayVal ? delayVal : 0f;
      var skippable = node.Parameters.TryGetValue("Skippable", out var skip) && skip is bool skipVal ? skipVal : true;

      // Store next node IDs in state FIRST, before emitting the message
      // This allows GraphRunner to pause execution immediately after this node completes
      var nextNodeIds = new List<string>();
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
      {
        nextNodeIds.Add(connection.ToNodeId);
      }
      state.Set("Dialogue.NextNodeIds", nextNodeIds);
      log.ForMethod().Information("DialogueLineNode {NodeId}: Text='{Text}', Speaker='{Speaker}', Stored {Count} next node ID(s) in state: {NodeIds}",
        node.NodeId, text, speakerId, nextNodeIds.Count, string.Join(", ", nextNodeIds));

      // Publish dialogue show message to display the dialogue line
      context.Emit(new DialogueShowMessage(text, speakerId, graph.GraphId));
      log.ForMethod().Information("Emitted DialogueShowMessage for graph {GraphId} with text '{Text}'. Waiting for user input...", graph.GraphId, text);

      // Handle timing and progression
      if (autoAdvance)
      {
        // Auto-advance mode: wait for min display time + auto-advance delay
        var totalDelay = minDisplaySeconds + autoAdvanceDelaySeconds;
        if (totalDelay > 0f)
        {
          await UniTask.Delay(TimeSpan.FromSeconds(totalDelay), cancellationToken: ct);
        }
        // Auto-advance - no need to wait for player input
      }
      else
      {
        // Manual advance mode
        if (minDisplaySeconds > 0f)
        {
          if (skippable)
          {
            // Player can skip before min time - race between min time and player click
            var minTimeTask = UniTask.Delay(TimeSpan.FromSeconds(minDisplaySeconds), cancellationToken: ct);
            var playerClickTask = WaitForDialogueProgressAsync(graph.GraphId, ct);
            var completedTask = await UniTask.WhenAny(minTimeTask, playerClickTask);

            // If min time completed first, still need to wait for player click
            if (completedTask == 0) // minTimeTask completed first
            {
              await playerClickTask;
            }
            // If playerClickTask completed first, we're done (player skipped)
          }
          else
          {
            // Must wait for minimum display time before allowing progress
            await UniTask.Delay(TimeSpan.FromSeconds(minDisplaySeconds), cancellationToken: ct);
            // Then wait for player click
            await WaitForDialogueProgressAsync(graph.GraphId, ct);
          }
        }
        else
        {
          // No minimum time - just wait for player click
          await WaitForDialogueProgressAsync(graph.GraphId, ct);
        }
      }

      // Next node IDs are already stored in state (done before emitting message)
      // GraphRunner will pause execution when it detects stored next node IDs
      log.ForMethod().Information("Finished waiting for user input. GraphRunner will pause execution and wait for DialogueContinueMessage.");
    }

    private async UniTask WaitForDialogueProgressAsync(string graphId, CancellationToken ct)
    {
      var tcs = new UniTaskCompletionSource();
      IDisposable subscription = null;

      try
      {
        var subscriber = GameMessageBroker.GetSubscriber<DialogueProgressMessage>();
        if (subscriber == null)
        {
          log.ForMethod().Error("GameMessageBroker.GetSubscriber<DialogueProgressMessage>() returned null. Broker may not be initialized. Dialogue will not progress.");
          throw new InvalidOperationException("GameMessageBroker subscriber is not available. Ensure MessageBroker is initialized before starting dialogue.");
        }

        log.ForMethod().Verbose("Subscribing to DialogueProgressMessage for graph {GraphId}", graphId);
        subscription = subscriber.Subscribe(message =>
        {
          log.ForMethod().Verbose("Received DialogueProgressMessage: GraphId={MessageGraphId}, ShouldClose={ShouldClose} (waiting for {ExpectedGraphId})",
            message.GraphId, message.ShouldClose, graphId);

          // Only continue if this message is for our graph (or no graph ID specified, meaning any)
          if (string.IsNullOrEmpty(message.GraphId) || message.GraphId == graphId)
          {
            log.ForMethod().Information("DialogueProgressMessage matched graph {GraphId} - continuing dialogue", graphId);
            tcs.TrySetResult();
          }
          else
          {
            log.ForMethod().Verbose("DialogueProgressMessage GraphId mismatch: expected {ExpectedGraphId}, got {ActualGraphId} - ignoring",
              graphId, message.GraphId);
          }
        });

        log.ForMethod().Verbose("Waiting for DialogueProgressMessage for graph {GraphId}", graphId);

        // Wait for the progress message or cancellation
        // Use AttachExternalCancellation to handle cancellation
        try
        {
          await tcs.Task.AttachExternalCancellation(ct);
          log.ForMethod().Verbose("DialogueProgressMessage received for graph {GraphId} - continuing", graphId);
        }
        catch (OperationCanceledException)
        {
          log.ForMethod().Warning("Wait for DialogueProgressMessage was cancelled for graph {GraphId}", graphId);
          // Cancellation handled - subscription will be disposed in finally
          return;
        }
      }
      finally
      {
        subscription?.Dispose();
        log.ForMethod().Verbose("Disposed DialogueProgressMessage subscription for graph {GraphId}", graphId);
      }
    }

    #endregion
  }
}