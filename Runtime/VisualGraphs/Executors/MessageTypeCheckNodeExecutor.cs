using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for MessageTypeCheckNode.
  ///   Checks if the current message matches an expected type and branches execution.
  /// </summary>
  public sealed class MessageTypeCheckNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "MessageTypeCheckNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      if (message == null)
      {
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Extract expected type from parameters
      // MessageTypeReference is serialized as the Type object
      var expectedType = node.Parameters.TryGetValue("expectedType", out var et) ? et as Type : null;

      if (expectedType == null)
      {
        UnityEngine.Debug.LogWarning("[MessageTypeCheck] ExpectedType is null");
        EnqueueOutputPort(graph, node, "DoesntMatch", context);
        return UniTask.CompletedTask;
      }

      // Check if message type matches
      var actualType = message.GetType();
      bool matches = actualType == expectedType || actualType.IsSubclassOf(expectedType) || expectedType.IsAssignableFrom(actualType);

      // Branch based on result
      var targetPort = matches ? "Matches" : "DoesntMatch";
      EnqueueOutputPort(graph, node, targetPort, context);

      return UniTask.CompletedTask;
    }

    private void EnqueueOutputPort(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, string portName, GraphNodeExecutionContext context)
    {
      // Filter connections manually to avoid LINQ allocation in hot path
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
      {
        if (connection.PortName == portName)
          context.EnqueueNext(connection.ToNodeId);
      }
    }
  }
}

