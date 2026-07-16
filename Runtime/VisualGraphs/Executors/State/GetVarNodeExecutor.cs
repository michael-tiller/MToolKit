using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors.State
{
  /// <summary>
  ///   Executor for GetVarNode. Resolves Key through <see cref="ScopedKeyResolver" /> (bare keys read
  ///   directly via <see cref="IGraphState" />, the resolver is never called for them — scoped keys go
  ///   through the resolver with a null local context) and writes the resolved value — or
  ///   <c>Fallback.GetDefaultValue()</c> when Key doesn't resolve — to ResultKey.
  /// </summary>
  public sealed class GetVarNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GetVarNodeExecutor>().ForFeature("VisualGraphs.State"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly ScopedKeyResolver resolver;

    public GetVarNodeExecutor(ScopedKeyResolver resolver)
    {
      this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public string NodeType => "GetVarNode";

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
      var fallback = node.Parameters.TryGetValue("Fallback", out var fb)
        ? (fb as GraphVariableDeclaration)?.GetDefaultValue()
        : null;

      var resolved = Resolve(key, state, fallback);

      WriteResult(graph, node, state, context, resultKey, resolved);

      return UniTask.CompletedTask;
    }

    private object Resolve(string key, IGraphState state, object fallback)
    {
      if (string.IsNullOrEmpty(key))
      {
        log.ForMethod().Warning("GetVar: Key is empty, using Fallback");
        return fallback;
      }

      var parsed = ScopedKeyResolver.Parse(key);
      if (parsed.IsLocal)
        return state.TryGet<object>(key, out var local) ? local : fallback;

      return resolver.Get(key, null, fallback);
    }

    private static void WriteResult(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      GraphNodeExecutionContext context,
      string resultKey,
      object result)
    {
      state.TryGet<object>(resultKey, out var old);
      state.Set(resultKey, result);
      context.Emit(new GraphStateChangedMessage(graph.GraphId, resultKey, old, result));

      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}
