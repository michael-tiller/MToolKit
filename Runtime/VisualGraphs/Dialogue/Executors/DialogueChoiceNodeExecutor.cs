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
        // Handle List<object> (serialized list)
        if (choicesParam is List<object> choicesList)
        {
          foreach (var choiceObj in choicesList.Take(3)) // Limit to 3 choices
          {
            // Each choice might be serialized as a Dictionary<string, object>
            if (choiceObj is Dictionary<string, object> choiceDict)
            {
              // Field names match the Choice class: "Text" and "LocalizationKey"
              var text = choiceDict.TryGetValue("Text", out var txt) ? txt as string :
                        (choiceDict.TryGetValue("text", out var txt2) ? txt2 as string : "");
              var locKey = choiceDict.TryGetValue("LocalizationKey", out var lk) ? lk as string :
                          (choiceDict.TryGetValue("localizationKey", out var lk2) ? lk2 as string : null);
              choices.Add(new DialogueShowChoiceMessage.ChoiceData(text, locKey));
            }
            // Or it might be the actual Choice object (if serialization preserves type)
            else if (choiceObj != null)
            {
              // Try to extract via reflection as fallback
              var choiceType = choiceObj.GetType();
              var textField = choiceType.GetField("Text");
              var locField = choiceType.GetField("LocalizationKey");
              var text = textField?.GetValue(choiceObj) as string ?? "";
              var locKey = locField?.GetValue(choiceObj) as string;
              choices.Add(new DialogueShowChoiceMessage.ChoiceData(text, locKey));
            }
          }
        }
      }

      // Publish dialogue choice message to display choices
      if (choices.Count > 0)
      {
        context.Emit(new DialogueShowChoiceMessage(choices, graph.GraphId));
      }
      else
      {
        // No choices to show, continue immediately
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return;
      }

      // Wait for player choice selection
      var selectedChoiceIndex = await WaitForChoiceSelectionAsync(graph.GraphId, ct);

      // Continue to the selected choice's output port for branching
      // The Output port is embedded inside each Choice, so port names are nested like "Choices.0.Output"
      if (selectedChoiceIndex >= 0 && selectedChoiceIndex < choices.Count)
      {
        // Get all connections from this node
        var allConnections = graph.GetConnectionsFrom(node.NodeId).ToList();

        log.ForMethod().Verbose("Choice {ChoiceIndex} selected. Found {ConnectionCount} total connections from node {NodeId}. Port names: {PortNames}",
          selectedChoiceIndex, allConnections.Count, node.NodeId, string.Join(", ", allConnections.Select(c => c.PortName)));

        // Try matching nested port formats first (most likely for embedded ports)
        // XNode nested dynamic ports use formats like:
        // - "Choices.0.Output" (dot notation with index)
        // - "Choices[0].Output" (bracket notation with index)
        var nestedPortFormats = new[]
        {
          $"Choices.{selectedChoiceIndex}.Output",
          $"Choices[{selectedChoiceIndex}].Output",
          $"Choices.{selectedChoiceIndex}.Output 0" // In case there are multiple outputs per choice
        };

        bool found = false;
        foreach (var portFormat in nestedPortFormats)
        {
          var matchingConnections = allConnections
            .Where(c => c.PortName == portFormat || c.PortName.StartsWith(portFormat + " "))
            .ToList();

          if (matchingConnections.Count > 0)
          {
            log.ForMethod().Information("Branching to {ConnectionCount} node(s) via port '{PortName}' for choice {ChoiceIndex}",
              matchingConnections.Count, portFormat, selectedChoiceIndex);

            foreach (var connection in matchingConnections)
            {
              context.EnqueueNext(connection.ToNodeId);
            }
            found = true;
            break;
          }
        }

        if (!found)
        {
          // Fallback: try flat port formats (in case ports are flattened somehow)
          var flatPortFormats = new[]
          {
            $"Output {selectedChoiceIndex}",
            $"Output[{selectedChoiceIndex}]",
            $"Output.{selectedChoiceIndex}"
          };

          foreach (var flatFormat in flatPortFormats)
          {
            var flatConnections = allConnections
              .Where(c => c.PortName == flatFormat)
              .ToList();

            if (flatConnections.Count > 0)
            {
              log.ForMethod().Information("Branching to {ConnectionCount} node(s) via port '{PortName}' (flat format) for choice {ChoiceIndex}",
                flatConnections.Count, flatFormat, selectedChoiceIndex);

              foreach (var connection in flatConnections)
              {
                context.EnqueueNext(connection.ToNodeId);
              }
              found = true;
              break;
            }
          }
        }

        if (!found)
        {
          // Last resort: match by connection order (assumes connections are in choice order)
          // This is less reliable but works if port names aren't being exported correctly
          log.ForMethod().Warning("Could not match port name for choice {ChoiceIndex}. Using connection order fallback. Available ports: {PortNames}",
            selectedChoiceIndex, string.Join(", ", allConnections.Select(c => c.PortName)));

          if (selectedChoiceIndex < allConnections.Count)
          {
            context.EnqueueNext(allConnections[selectedChoiceIndex].ToNodeId);
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