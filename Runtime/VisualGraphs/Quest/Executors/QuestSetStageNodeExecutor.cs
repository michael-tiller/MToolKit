using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for QuestSetStageNode.
  ///   Sets a quest stage and continues to connected nodes.
  /// </summary>
  public sealed class QuestSetStageNodeExecutor : IGraphNodeExecutor
  {
    #region IGraphNodeExecutor Members

    public string NodeType => nameof(QuestSetStageNodeExecutor);

    public UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters
      var stageKey = node.Parameters.TryGetValue("stageKey", out var sk) ? sk as string : "unknown";
      var stageValue = node.Parameters.TryGetValue("stageValue", out var sv) ? Convert.ToInt32(sv) : 0;

      // Set stage in state
      state.Set(stageKey, stageValue);

      // TODO: Emit Quest.StageSet message to MessagePipe
      // Example: context.Emit(new QuestStageSetMessage(graph.GraphId, stageKey, stageValue));

      // Continue to connected nodes
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);

      return UniTask.CompletedTask;
    }

    #endregion
  }
}