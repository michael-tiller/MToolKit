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

namespace MToolKit.Runtime.VisualGraphs.Executors.Logic
{
  /// <summary>
  ///   Executor for NotNode. Negates a bool state value (missing/unresolvable treated as false), writes
  ///   the result.
  /// </summary>
  public sealed class NotNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<NotNodeExecutor>().ForFeature("VisualGraphs.Logic"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "NotNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      var key = node.Parameters.TryGetValue("Key", out var k) ? k as string : null;
      var resultKey = node.Parameters.TryGetValue("ResultKey", out var rk) ? rk as string : null;

      var result = !ReadBool(key, state);

      WriteResult(graph, node, state, context, resultKey, result);

      return UniTask.CompletedTask;
    }

    private static bool ReadBool(string key, IGraphState state)
    {
      if (!string.IsNullOrEmpty(key) && state.TryGet<bool>(key, out var value))
        return value;

      log.ForMethod().Warning("Logic: state key '{Key}' missing or not a bool, treated as false", key);
      return false;
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
