using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for MessageFieldGetNode.
  ///   Uses reflection to extract message field values and store them in state.
  /// </summary>
  public sealed class MessageFieldGetNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "MessageFieldGetNode";

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
        UnityEngine.Debug.LogWarning("[MessageFieldGet] No message available");
        ContinueExecution(graph, node, context);
        return UniTask.CompletedTask;
      }

      // Extract parameters
      var fieldName = node.Parameters.TryGetValue("fieldName", out var fn) ? fn as string : null;
      var stateKey = node.Parameters.TryGetValue("stateKey", out var sk) ? sk as string : null;
      var debugLog = node.Parameters.TryGetValue("debugLog", out var dl) && Convert.ToBoolean(dl);

      if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(stateKey))
      {
        UnityEngine.Debug.LogWarning("[MessageFieldGet] FieldName or StateKey is empty");
        ContinueExecution(graph, node, context);
        return UniTask.CompletedTask;
      }

      // Use reflection to get field/property value
      var messageType = message.GetType();
      var field = messageType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
      var property = messageType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);

      object value = null;
      if (field != null)
      {
        value = field.GetValue(message);
      }
      else if (property != null)
      {
        value = property.GetValue(message);
      }
      else
      {
        UnityEngine.Debug.LogWarning($"[MessageFieldGet] Field/Property '{fieldName}' not found on message type '{messageType.Name}'");
        ContinueExecution(graph, node, context);
        return UniTask.CompletedTask;
      }

      // Store in state
      state.Set(stateKey, value);

      if (debugLog)
      {
        UnityEngine.Debug.Log($"[MessageFieldGet] Extracted '{fieldName}' = {value} → stored as '{stateKey}'");
      }

      // Continue execution
      ContinueExecution(graph, node, context);

      return UniTask.CompletedTask;
    }

    private void ContinueExecution(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, GraphNodeExecutionContext context)
    {
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

