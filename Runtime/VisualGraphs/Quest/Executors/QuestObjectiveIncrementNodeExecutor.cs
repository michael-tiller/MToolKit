using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>
  ///   Executor for QuestObjectiveIncrementNode.
  ///   Increments objective progress and continues execution.
  /// </summary>
  public sealed class QuestObjectiveIncrementNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<QuestObjectiveIncrementNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "QuestObjectiveIncrementNode";

    public UniTask ExecuteAsync(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract parameters (try both camelCase and PascalCase for compatibility)
      // Objective field is a QuestObjective (GuidScriptableObject) serialized as GUID string
      var objectiveGuid = node.Parameters.TryGetValue("objective", out var obj) ? obj as string : null;
      if (string.IsNullOrEmpty(objectiveGuid))
        objectiveGuid = node.Parameters.TryGetValue("Objective", out obj) ? obj as string : null;

      var amount = node.Parameters.TryGetValue("amount", out var amt) ? Convert.ToInt32(amt) : 1;
      if (amount == 1) // Default, try PascalCase
        amount = node.Parameters.TryGetValue("Amount", out amt) ? Convert.ToInt32(amt) : 1;

      var emitEvent = node.Parameters.TryGetValue("emitProgressEvent", out var emit) && Convert.ToBoolean(emit);
      if (!emitEvent) // Try PascalCase
        emitEvent = node.Parameters.TryGetValue("EmitProgressEvent", out emit) && Convert.ToBoolean(emit);

      log.ForMethod().Information("Quest: Executing QuestObjectiveIncrementNode (objective: {ObjectiveGuid}, amount: {Amount}, emitEvent: {EmitEvent})",
        objectiveGuid, amount, emitEvent);

      if (string.IsNullOrEmpty(objectiveGuid))
      {
        log.ForMethod().Warning("Quest: QuestObjectiveIncrementNode has null objective GUID, continuing execution");
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return UniTask.CompletedTask;
      }

      // Get or create objective progress
      var key = $"objective_{objectiveGuid}";
      QuestObjectiveProgress progress;

      if (state.TryGet(key, out QuestObjectiveProgress existing))
      {
        progress = existing;
        log.ForMethod().Debug("Quest: Found existing progress for objective {ObjectiveGuid}: {Current}/{Required}",
          objectiveGuid, existing.Current, existing.Required);
      }
      else
      {
        // Initialize with unknown required value (will be set by quest definition)
        progress = new QuestObjectiveProgress
        {
          ObjectiveGuid = objectiveGuid,
          Current = 0,
          Required = 1 // Default, should be overridden by quest definition
        };
        log.ForMethod().Debug("Quest: Initialized new progress for objective {ObjectiveGuid}", objectiveGuid);
      }

      // Increment progress
      var previousValue = progress.Current;
      progress.Current += amount;

      // Clamp to required value (don't overshoot)
      if (progress.Current > progress.Required)
        progress.Current = progress.Required;

      // Store updated progress
      state.Set(key, progress);

      // Log progress
      var wasCompleted = progress.IsComplete;
      log.ForMethod().Information("Quest: Objective {ObjectiveGuid} progress: {Previous} → {Current}/{Required} ({Percentage:F0}%){Complete}",
        objectiveGuid, previousValue, progress.Current, progress.Required, progress.Percentage * 100, wasCompleted ? " ✅ COMPLETE" : "");

      // Emit progress event if enabled
      if (emitEvent)
      {
        log.ForMethod().Information("Quest: Emitting QuestObjectiveProgressMessage for objective {ObjectiveGuid}", objectiveGuid);

        // Get quest context from state
        var questGuid = state.TryGet<string>("__quest_guid", out var qg) ? qg : null;
        var questDef = state.TryGet<QuestDefinition>("__quest_definition", out var qd) ? qd : null;

        // Find the objective definition
        QuestObjective objectiveDef = null;
        if (questDef != null && questDef.Objectives != null)
        {
          objectiveDef = questDef.Objectives.Find(o => o.Guid == objectiveGuid);
        }

        var progressMsg = new QuestObjectiveProgressMessage(
          questGuid,
          objectiveGuid,
          objectiveDef,
          progress.Current,
          progress.Required,
          progress.Percentage,
          progress.IsComplete
        );

        context.Emit(progressMsg);
        log.ForMethod().Information("Quest: Called context.Emit() for QuestObjectiveProgressMessage (objective: {ObjectiveGuid}, quest: {QuestGuid})",
          objectiveGuid, questGuid);
      }
      else
      {
        log.ForMethod().Information("Quest: Skipping QuestObjectiveProgressMessage emission - emitProgressEvent is false for objective {ObjectiveGuid}",
          objectiveGuid);
      }

      // Continue to connected nodes
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);

      return UniTask.CompletedTask;
    }
  }
}

