using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Executors
{
  /// <summary>
  ///   Executor for QuestObjectiveCheckNode.
  ///   Branches execution based on objective completion status.
  /// </summary>

  public sealed class QuestObjectiveCheckNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<QuestObjectiveCheckNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

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
      // Try both camelCase and PascalCase for compatibility
      var objectiveGuid = node.Parameters.TryGetValue("objective", out var obj) ? obj as string : null;
      if (string.IsNullOrEmpty(objectiveGuid))
        objectiveGuid = node.Parameters.TryGetValue("Objective", out obj) ? obj as string : null;

      if (string.IsNullOrEmpty(objectiveGuid))
      {
        log.ForMethod().Warning("Quest: QuestObjectiveCheckNode has null objective GUID, continuing to all outputs");
        // If no objective GUID, continue to all outputs (safeguard)
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return UniTask.CompletedTask;
      }

      // Check objective progress
      var key = $"objective_{objectiveGuid}";
      QuestObjectiveProgress progress = null;
      var isComplete = false;

      if (state.TryGet(key, out QuestObjectiveProgress existingProgress))
      {
        progress = existingProgress;
        isComplete = progress.IsComplete;
        log.ForMethod().Debug("Quest: Found progress for objective {ObjectiveGuid}: {Current}/{Required} (Complete: {IsComplete})",
          objectiveGuid, progress.Current, progress.Required, isComplete);
      }
      else
      {
        // Progress not found in state - try to get it from quest definition
        var questDef = state.TryGet<QuestDefinition>("__quest_definition", out var qd) ? qd : null;
        if (questDef != null && questDef.Objectives != null)
        {
          var objective = questDef.Objectives.Find(o => o.Guid == objectiveGuid);
          if (objective != null)
          {
            // Use QuestDefinition's helper to get progress (returns default if not found)
            progress = questDef.GetObjectiveProgress(state, objective);
            isComplete = progress.IsComplete;
            log.ForMethod().Debug("Quest: Initialized progress from quest definition for objective {ObjectiveGuid}: {Current}/{Required} (Complete: {IsComplete})",
              objectiveGuid, progress.Current, progress.Required, isComplete);
          }
          else
          {
            log.ForMethod().Warning("Quest: Objective {ObjectiveGuid} not found in quest definition, treating as incomplete", objectiveGuid);
            isComplete = false;
          }
        }
        else
        {
          log.ForMethod().Warning("Quest: Quest definition not found in state for objective {ObjectiveGuid}, treating as incomplete", objectiveGuid);
          isComplete = false;
        }
      }

      // Branch based on completion
      var targetPort = isComplete ? "Complete" : "Incomplete";

      var matchingConnections = graph.GetConnectionsFrom(node.NodeId)
        .Where(c => c.PortName == targetPort)
        .ToList();

      log.ForMethod().Information("Quest: QuestObjectiveCheckNode branching to '{TargetPort}' port ({ConnectionCount} connection(s)) for objective {ObjectiveGuid}",
        targetPort, matchingConnections.Count, objectiveGuid);

      foreach (var connection in matchingConnections)
        context.EnqueueNext(connection.ToNodeId);

      return UniTask.CompletedTask;
    }
  }
}

