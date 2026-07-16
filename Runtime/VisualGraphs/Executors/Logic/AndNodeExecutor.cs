using System;
using System.Collections.Generic;
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

namespace MToolKit.Runtime.VisualGraphs.Executors.Logic
{
  /// <summary>
  ///   Executor for AndNode. Folds a list of bool state keys with AND, left-to-right (empty list → true;
  ///   missing/unresolvable keys are skipped from the fold), writes the result.
  /// </summary>
  public sealed class AndNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<AndNodeExecutor>().ForFeature("VisualGraphs.Logic"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "AndNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      var keys = node.Parameters.TryGetValue("Keys", out var k) ? k as List<string> : null;
      var resultKey = node.Parameters.TryGetValue("ResultKey", out var rk) ? rk as string : null;

      var result = true;
      if (keys != null)
        foreach (var key in keys)
        {
          if (!string.IsNullOrEmpty(key) && state.TryGet<bool>(key, out var value))
            result = result && value;
          else
            log.ForMethod().Warning("Logic: state key '{Key}' missing or not a bool, skipped from AND fold", key);
        }

      WriteResult(graph, node, state, context, resultKey, result);

      return UniTask.CompletedTask;
    }

    private static void WriteResult(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      GraphNodeExecutionContext context,
      string resultKey,
      bool result)
    {
      state.TryGet<object>(resultKey, out var old);
      state.Set(resultKey, result);
      context.Emit(new GraphStateChangedMessage(graph.GraphId, resultKey, old, result));

      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}
