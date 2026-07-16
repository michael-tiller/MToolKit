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

namespace MToolKit.Runtime.VisualGraphs.Executors.Transform
{
  /// <summary>
  ///   Executor for PositionNode. Reads a Vector3 state key (missing/wrong-type → Vector3.zero + warning),
  ///   writes it to another state key.
  /// </summary>
  public sealed class PositionNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<PositionNodeExecutor>().ForFeature("VisualGraphs.Transform"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "PositionNode";

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      var sourceKey = node.Parameters.TryGetValue("SourceKey", out var sk) ? sk as string : null;
      var destinationKey = node.Parameters.TryGetValue("DestinationKey", out var dk) ? dk as string : null;

      var value = ReadVector3(sourceKey, state);

      WriteResult(graph, node, state, context, destinationKey, value);

      return UniTask.CompletedTask;
    }

    private static Vector3 ReadVector3(string key, IGraphState state)
    {
      if (!string.IsNullOrEmpty(key) && state.TryGet<Vector3>(key, out var value))
        return value;

      log.ForMethod().Warning("Transform: state key '{Key}' missing or not a Vector3, using Vector3.zero", key);
      return Vector3.zero;
    }

    private static void WriteResult(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      GraphNodeExecutionContext context,
      string destinationKey,
      Vector3 result)
    {
      state.TryGet<object>(destinationKey, out var old);
      state.Set(destinationKey, result);
      context.Emit(new GraphStateChangedMessage(graph.GraphId, destinationKey, old, result));

      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}
