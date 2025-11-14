using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for QuestObjectiveCheckNode.
  ///   Branches execution based on objective completion status.
  /// </summary>

  public sealed class QuestObjectiveCheckNodeExecutor : IGraphNodeExecutor
  {
    public string NodeType => "QuestObjectiveCheckNode";

    public UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters - Objective field will be serialized as GUID string
      var objectiveGuid = node.Parameters.TryGetValue("objective", out var obj) ? obj as string : null;

      if (string.IsNullOrEmpty(objectiveGuid))
      {
        // If no objective GUID, continue to all outputs (safeguard)
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return UniTask.CompletedTask;
      }

      // Check objective progress
      var key = $"objective_{objectiveGuid}";
      var isComplete = false;

      if (state.TryGet(key, out QuestObjectiveProgress progress))
      {
        isComplete = progress.IsComplete;
      }

      // Branch based on completion
      var targetPort = isComplete ? "Complete" : "Incomplete";

      var matchingConnections = graph.GetConnectionsFrom(node.NodeId)
        .Where(c => c.PortName == targetPort)
        .ToList();

      foreach (var connection in matchingConnections)
        context.EnqueueNext(connection.ToNodeId);

      return UniTask.CompletedTask;
    }
  }
}

