using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors.Math
{
  /// <summary>
  ///   Executor for ClampNode. Clamps a float state value between two float bounds, writes the result.
  /// </summary>
  public sealed class ClampNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<ClampNodeExecutor>().ForFeature("VisualGraphs.Math"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "ClampNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      var valueKey = node.Parameters.TryGetValue("ValueKey", out var vk) ? vk as string : null;
      var minKey = node.Parameters.TryGetValue("MinKey", out var mnk) ? mnk as string : null;
      var maxKey = node.Parameters.TryGetValue("MaxKey", out var mxk) ? mxk as string : null;
      var resultKey = node.Parameters.TryGetValue("ResultKey", out var rk) ? rk as string : null;

      var result = Mathf.Clamp(ReadFloat(valueKey, state), ReadFloat(minKey, state), ReadFloat(maxKey, state));

      WriteResult(graph, node, state, context, resultKey, result);

      return UniTask.CompletedTask;
    }

    private static float ReadFloat(string key, IGraphState state)
    {
      if (!string.IsNullOrEmpty(key) && state.TryGet<float>(key, out var value))
        return value;

      log.ForMethod().Warning("Math: state key '{Key}' missing or not a float, using 0", key);
      return 0f;
    }

    private static void WriteResult(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      GraphNodeExecutionContext context,
      string resultKey,
      float result)
    {
      state.TryGet<object>(resultKey, out var old);
      state.Set(resultKey, result);
      context.Emit(new GraphStateChangedMessage(graph.GraphId, resultKey, old, result));

      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}
