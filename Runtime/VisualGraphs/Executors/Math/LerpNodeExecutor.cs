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
  ///   Executor for LerpNode. Interpolates between two float state values by a float t state value
  ///   (Mathf.Lerp semantics — t implicitly clamped [0,1]), writes the result.
  /// </summary>
  public sealed class LerpNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<LerpNodeExecutor>().ForFeature("VisualGraphs.Math"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "LerpNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      var aKey = node.Parameters.TryGetValue("AKey", out var ak) ? ak as string : null;
      var bKey = node.Parameters.TryGetValue("BKey", out var bk) ? bk as string : null;
      var tKey = node.Parameters.TryGetValue("TKey", out var tk) ? tk as string : null;
      var resultKey = node.Parameters.TryGetValue("ResultKey", out var rk) ? rk as string : null;

      var result = Mathf.Lerp(ReadFloat(aKey, state), ReadFloat(bKey, state), ReadFloat(tKey, state));

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
