using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for GenericStateSetNode.
  ///   Sets an arbitrary state key to a value, converting the value string to the appropriate type.
  /// </summary>
  public sealed class GenericStateSetNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GenericStateSetNodeExecutor>().ForFeature("VisualGraphs.State"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "GenericStateSetNode";

    public UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters
      var stateKey = node.Parameters.TryGetValue("stateKey", out var sk) ? sk as string : null;
      var value = node.Parameters.TryGetValue("value", out var val) ? val as string : null;
      var valueType = node.Parameters.TryGetValue("valueType", out var vt) ? vt as string : "bool";
      var debugLog = node.Parameters.TryGetValue("debugLog", out var dl) && Convert.ToBoolean(dl);

      if (string.IsNullOrEmpty(stateKey))
      {
        log.ForMethod().Warning("GenericStateSet: StateKey is empty, continuing execution");
        ContinueExecution(graph, node, context);
        return UniTask.CompletedTask;
      }

      // Parse value based on type
      object parsedValue = null;
      try
      {
        parsedValue = ParseValue(value, valueType);
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "GenericStateSet: Failed to parse value '{Value}' as type '{ValueType}' for key '{StateKey}'",
          value, valueType, stateKey);
        ContinueExecution(graph, node, context);
        return UniTask.CompletedTask;
      }

      // Get old value before setting (for state change message)
      object oldValue = null;
      state.TryGet<object>(stateKey, out oldValue);

      // Set state
      state.Set(stateKey, parsedValue);

      // Emit state changed message
      var stateChangedMsg = new GraphStateChangedMessage(
        graph.GraphId,
        stateKey,
        oldValue,
        parsedValue);
      context.Emit(stateChangedMsg);

      if (debugLog)
      {
        log.ForMethod().Information("GenericStateSet: Set '{StateKey}' = {Value} (type: {ValueType}, old: {OldValue})",
          stateKey, parsedValue, valueType, oldValue ?? "null");
      }

      // Continue execution
      ContinueExecution(graph, node, context);

      return UniTask.CompletedTask;
    }

    private object ParseValue(string value, string valueType)
    {
      if (string.IsNullOrEmpty(value))
        value = "0"; // Default to 0 if empty

      return valueType?.ToLowerInvariant() switch
      {
        "bool" => Convert.ToBoolean(value),
        "int" => Convert.ToInt32(value),
        "float" => Convert.ToSingle(value),
        "string" => value,
        _ => value // Default to string if unknown type
      };
    }

    private void ContinueExecution(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, GraphNodeExecutionContext context)
    {
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

