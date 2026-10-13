using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for GenericStateGetNode.
  ///   Reads a state value and stores it in another state key for use by other nodes.
  /// </summary>
  public sealed class GenericStateGetNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GenericStateGetNodeExecutor>().ForFeature("VisualGraphs.State"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "GenericStateGetNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters
      var sourceStateKey = GraphNodeValues.Parameter(node, "SourceStateKey", "sourceStateKey") as string;
      var destinationStateKey = GraphNodeValues.Parameter(node, "DestinationStateKey", "destinationStateKey") as string;
      var defaultValue = GraphNodeValues.Parameter(node, "DefaultValue", "defaultValue", "0") as string;
      var debugLog = Convert.ToBoolean(GraphNodeValues.Parameter(node, "DebugLog", "debugLog", false));

      if (string.IsNullOrEmpty(sourceStateKey) || string.IsNullOrEmpty(destinationStateKey))
      {
        log.ForMethod().Warning("GenericStateGet: SourceStateKey or DestinationStateKey is empty, continuing execution");
        ContinueExecution(graph, node, context);
        return UniTask.CompletedTask;
      }

      // Try to get source value
      var values = new GraphNodeValues(graph, state, context);
      object value = null;
      if (values.TryGet<object>(sourceStateKey, out var sourceValue))
      {
        value = sourceValue;
      }
      else
      {
        // Use default value if source doesn't exist
        // Try to infer type from default value
        if (bool.TryParse(defaultValue, out var boolVal))
          value = boolVal;
        else if (int.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
          value = intVal;
        else if (float.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
          value = floatVal;
        else
          value = defaultValue; // Default to string

        log.ForMethod().Debug("GenericStateGet: Source key '{SourceStateKey}' not found, using default value '{DefaultValue}'",
          sourceStateKey, defaultValue);
      }

      // Store in destination
      values.Set(destinationStateKey, value);

      if (debugLog)
      {
        log.ForMethod().Information("GenericStateGet: Copied '{SourceStateKey}' = {Value} → '{DestinationStateKey}'",
          sourceStateKey, value, destinationStateKey);
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

