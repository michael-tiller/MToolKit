using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>
  ///   Executor for QuestAllObjectivesCompleteNode.
  ///   Checks if all required objectives are complete.
  /// </summary>
  public sealed class QuestAllObjectivesCompleteNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "QuestAllObjectivesCompleteNode";

    public UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters
      var emitEvent = node.Parameters.TryGetValue("emitCompleteEvent", out var emit) && Convert.ToBoolean(emit);

      // Check all objectives
      // NOTE: This requires knowing which objectives exist for this quest
      // For now, we check if a special "quest_all_complete" flag is set
      // The QuestDefinition.IsComplete() method should be used to properly check this

      // TODO: This executor needs access to the QuestDefinition to properly check objectives
      // For now, use a simple heuristic: check if "quest_complete" flag is set
      var allComplete = state.TryGet("quest_complete", out bool complete) && complete;

      // Branch based on completion
      var targetPort = allComplete ? "AllComplete" : "Incomplete";

      var matchingConnections = graph.GetConnectionsFrom(node.NodeId)
        .Where(c => c.PortName == targetPort)
        .ToList();

      foreach (var connection in matchingConnections)
        context.EnqueueNext(connection.ToNodeId);

      // TODO: Emit QuestCompleteMessage if emitEvent is true and all complete
      // context.Emitter.Emit(new QuestCompleteMessage { QuestId = graph.GraphId });

      return UniTask.CompletedTask;
    }
  }
}

