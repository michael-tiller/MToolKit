using System;
using System.Globalization;
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

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters
      var stateKey = GraphNodeValues.Parameter(node, "StateKey", "stateKey") as string;
      var value = GraphNodeValues.Parameter(node, "Value", "value") as string;
      var valueType = GraphNodeValues.Parameter(node, "ValueType", "valueType", "bool") as string;
      var debugLog = Convert.ToBoolean(GraphNodeValues.Parameter(node, "DebugLog", "debugLog", false));

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
      var values = new GraphNodeValues(graph, state, context);
      oldValue = values.GetObject(stateKey);

      // Set state
      values.Set(stateKey, parsedValue);

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
      // For string type, preserve empty strings as-is
      if (valueType?.ToLowerInvariant() == "string")
        return value ?? string.Empty;

      // For other types, default empty to "0"
      if (string.IsNullOrEmpty(value))
        value = "0";

      return valueType?.ToLowerInvariant() switch
      {
        "bool" => value == "1" || value.Equals("true", System.StringComparison.OrdinalIgnoreCase) ? true :
                  value == "0" || value.Equals("false", System.StringComparison.OrdinalIgnoreCase) ? false :
                  Convert.ToBoolean(value),
        "int" => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
        "float" => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
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

