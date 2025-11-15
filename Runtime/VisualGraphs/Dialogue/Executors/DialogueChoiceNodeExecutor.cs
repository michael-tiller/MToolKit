using System;
using System.Collections.Generic;
using System.Linq;
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
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Executors
{
  /// <summary>
  ///   Executor for DialogueChoiceNode.
  ///   Presents choices and continues to selected branch.
  /// </summary>
  public sealed class DialogueChoiceNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<DialogueChoiceNodeExecutor>().ForFeature("VisualGraphs.Dialogue"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    #region IGraphNodeExecutor Members

    public string NodeType => "DialogueChoiceNode";

    public async UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract choices from parameters
      // Choices are stored as a list in the "Choices" parameter (field name matches node class)
      var choices = new List<DialogueShowChoiceMessage.ChoiceData>();

      if (node.Parameters.TryGetValue("Choices", out var choicesParam))
      {
        log.ForMethod().Verbose("Found Choices parameter of type {Type}", choicesParam?.GetType().Name ?? "null");

        // Handle List<Dictionary<string, object>> (strongly-typed list from our exporter)
        if (choicesParam is System.Collections.IEnumerable choicesEnumerable)
        {
          var count = 0;
          foreach (var choiceObj in choicesEnumerable)
          {
            if (count >= 3) break; // Limit to 3 choices

            // Each choice should be a Dictionary<string, object>
            if (choiceObj is Dictionary<string, object> choiceDict)
            {
              // Field names match the Choice class: "Text" and "LocalizationKey"
              var text = choiceDict.TryGetValue("Text", out var txt) ? txt as string :
                        (choiceDict.TryGetValue("text", out var txt2) ? txt2 as string : "");
              choices.Add(new DialogueShowChoiceMessage.ChoiceData(text));
              log.ForMethod().Verbose("Extracted choice: Text='{Text}'", text);
              count++;
            }
            // Or it might be the actual Choice object (if serialization preserves type)
            else if (choiceObj != null)
            {
              log.ForMethod().Verbose("Choice object is of type {Type}, attempting reflection extraction", choiceObj.GetType().Name);

              // Try to extract via reflection as fallback
              var choiceType = choiceObj.GetType();
              var textField = choiceType.GetField("Text");
              var locField = choiceType.GetField("LocalizationKey");
              var text = textField?.GetValue(choiceObj) as string ?? "";
              var locKey = locField?.GetValue(choiceObj) as string;
              choices.Add(new DialogueShowChoiceMessage.ChoiceData(text));
              log.ForMethod().Verbose("Extracted choice via reflection: Text='{Text}'", text);
              count++;
            }
          }

          log.ForMethod().Information("Processed {Count} choices from parameter", count);
        }
        else
        {
          log.ForMethod().Warning("Choices parameter is not enumerable, it's {Type}. Available parameter keys: {Keys}",
            choicesParam?.GetType().Name ?? "null",
            string.Join(", ", node.Parameters.Keys));
        }
      }
      else
      {
        log.ForMethod().Warning("No 'Choices' parameter found in node. Available parameter keys: {Keys}",
          string.Join(", ", node.Parameters.Keys));
      }

      // Publish dialogue choice message to display choices
      if (choices.Count > 0)
      {
        context.Emit(new DialogueShowChoiceMessage(choices, graph.GraphId));
      }
      else
      {
        // No choices to show, continue immediately by storing next node IDs in state
        var nextNodeIds = new List<string>();
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        {
          nextNodeIds.Add(connection.ToNodeId);
        }
        state.Set("Dialogue.NextNodeIds", nextNodeIds);
        log.ForMethod().Information("No choices to show - stored {Count} next node ID(s) in state for graph {GraphId}.",
          nextNodeIds.Count, graph.GraphId);
        return;
      }

      // Wait for player choice selection
      var selectedChoiceIndex = await WaitForChoiceSelectionAsync(graph.GraphId, ct);

      // Continue to the selected choice's output port for branching
      // The exporter now stores connections with predictable port names: "Choice_{index}"
      if (selectedChoiceIndex >= 0 && selectedChoiceIndex < choices.Count)
      {
        // Get all connections from this node
        var allConnections = graph.GetConnectionsFrom(node.NodeId).ToList();

        log.ForMethod().Information("Choice {ChoiceIndex} selected. Found {ConnectionCount} total connections from node {NodeId}. Port names: {PortNames}",
          selectedChoiceIndex, allConnections.Count, node.NodeId, string.Join(", ", allConnections.Select(c => c.PortName)));

        // Try multiple port name formats:
        // 1. "Choice_{index}" - format used by exporter in RuntimeConnectionDefinition
        // 2. "ChoiceOutputs {index}" - actual xNode dynamic port name format
        var portNameFormats = new[]
        {
          $"Choice_{selectedChoiceIndex}",
          $"ChoiceOutputs {selectedChoiceIndex}"
        };

        var matchingConnections = new List<RuntimeConnectionDefinition>();
        string matchedPortName = null;

        foreach (var portNameFormat in portNameFormats)
        {
          var matches = allConnections
            .Where(c => c.PortName == portNameFormat || c.PortName.StartsWith(portNameFormat + " "))
            .ToList();

          if (matches.Count > 0)
          {
            matchingConnections = matches;
            matchedPortName = portNameFormat;
            break;
          }
        }

        log.ForMethod().Information("Looking for ports matching choice {ChoiceIndex}. Tried formats: {Formats}. Found {MatchCount} matching connection(s) via '{MatchedPort}'.",
          selectedChoiceIndex, string.Join(", ", portNameFormats), matchingConnections.Count, matchedPortName ?? "none");

        if (matchingConnections.Count > 0)
        {
          log.ForMethod().Information("Branching to {ConnectionCount} node(s) via port '{PortName}' for choice {ChoiceIndex}",
            matchingConnections.Count, matchedPortName, selectedChoiceIndex);

          // Store next node IDs in state instead of enqueueing immediately
          // This allows the dialogue to pause and resume properly
          var nextNodeIds = new List<string>();
          foreach (var connection in matchingConnections)
          {
            nextNodeIds.Add(connection.ToNodeId);
          }
          state.Set("Dialogue.NextNodeIds", nextNodeIds);
          log.ForMethod().Information("Stored {Count} next node ID(s) in state for graph {GraphId} after choice selection.",
            nextNodeIds.Count, graph.GraphId);
        }
        else
        {
          // Fallback: Try legacy port name formats (for backwards compatibility with old exports)
          var legacyPortFormats = new[]
          {
            $"Choices.{selectedChoiceIndex}.Output",
            $"Choices[{selectedChoiceIndex}].Output",
            $"Choices.{selectedChoiceIndex}.Output 0",
            $"Output {selectedChoiceIndex}",
            $"Output[{selectedChoiceIndex}]",
            $"Output.{selectedChoiceIndex}"
          };

          bool found = false;
          foreach (var portFormat in legacyPortFormats)
          {
            var legacyConnections = allConnections
              .Where(c => c.PortName == portFormat || c.PortName.StartsWith(portFormat + " "))
              .ToList();

            if (legacyConnections.Count > 0)
            {
              log.ForMethod().Information("Branching to {ConnectionCount} node(s) via legacy port '{PortName}' for choice {ChoiceIndex}",
                legacyConnections.Count, portFormat, selectedChoiceIndex);

              // Store next node IDs in state instead of enqueueing immediately
              var nextNodeIds = new List<string>();
              foreach (var connection in legacyConnections)
              {
                nextNodeIds.Add(connection.ToNodeId);
              }
              state.Set("Dialogue.NextNodeIds", nextNodeIds);
              log.ForMethod().Information("Stored {Count} next node ID(s) in state for graph {GraphId} after choice selection (legacy port).",
                nextNodeIds.Count, graph.GraphId);
              found = true;
              break;
            }
          }

          if (!found)
          {
            // Last resort: match by connection order (assumes connections are in choice order)
            log.ForMethod().Warning("Could not match port name for choice {ChoiceIndex}. Using connection order fallback. Available ports: {PortNames}",
              selectedChoiceIndex, string.Join(", ", allConnections.Select(c => c.PortName)));

            if (selectedChoiceIndex < allConnections.Count)
            {
              // Store next node ID in state instead of enqueueing immediately
              var nextNodeIds = new List<string> { allConnections[selectedChoiceIndex].ToNodeId };
              state.Set("Dialogue.NextNodeIds", nextNodeIds);
              log.ForMethod().Information("Stored next node ID in state for graph {GraphId} after choice selection (fallback).",
                graph.GraphId);
            }
          }
        }
      }
      else
      {
        // Fallback: if no valid selection (cancelled/timeout), don't continue
        // This prevents unintended dialogue progression
        log.ForMethod().Warning("Invalid choice index {ChoiceIndex} or selection cancelled. Not continuing dialogue.",
          selectedChoiceIndex);
      }
    }

    private async UniTask<int> WaitForChoiceSelectionAsync(string graphId, CancellationToken ct)
    {
      var tcs = new UniTaskCompletionSource<int>();
      IDisposable subscription = null;

      try
      {
        var subscriber = GameMessageBroker.GetSubscriber<DialogueChoiceSelectedMessage>();
        if (subscriber != null)
        {
          subscription = subscriber.Subscribe(message =>
          {
            // Only continue if this message is for our graph (or no graph ID specified, meaning any)
            if (string.IsNullOrEmpty(message.GraphId) || message.GraphId == graphId)
            {
              tcs.TrySetResult(message.ChoiceIndex);
            }
          });
        }

        // Wait for the choice selected message or cancellation
        // Use AttachExternalCancellation to handle cancellation
        try
        {
          return await tcs.Task.AttachExternalCancellation(ct);
        }
        catch (OperationCanceledException)
        {
          // Cancellation handled - subscription will be disposed in finally
          // Return -1 to indicate no selection
          return -1;
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