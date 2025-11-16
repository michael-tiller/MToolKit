using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using UnityEngine.AddressableAssets;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>
  ///   Executor for QuestObjectiveSetNode.
  ///   Sets objective progress to an exact value.
  /// </summary>
  public sealed class QuestObjectiveSetNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<QuestObjectiveSetNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "QuestObjectiveSetNode";

    public async UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract Objective parameter
      if (!node.Parameters.TryGetValue("Objective", out var objectiveParam))
      {
        log.ForMethod().Warning("Quest: QuestObjectiveSetNode has no 'Objective' parameter, continuing execution");
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return;
      }

      QuestObjective objectiveDef = null;
      string objectiveGuid = null;

      // Handle ObjectiveAssetReference directly
      if (objectiveParam is ObjectiveAssetReference objectiveAssetRef)
      {
        var handle = Addressables.LoadAssetAsync<QuestObjective>(objectiveAssetRef);
        objectiveDef = await handle.ToUniTask(cancellationToken: ct);
        objectiveGuid = objectiveDef.Guid;
      }
      // Handle SerializableAssetReference
      else if (objectiveParam is SerializableAssetReference assetRef)
      {
        var handle = Addressables.LoadAssetAsync<QuestObjective>(assetRef.AssetGuid);
        objectiveDef = await handle.ToUniTask(cancellationToken: ct);
        objectiveGuid = objectiveDef.Guid;
      }
      else
      {
        log.ForMethod().Warning("Quest: QuestObjectiveSetNode 'Objective' parameter is not an ObjectiveAssetReference or SerializableAssetReference (type: {Type}), continuing execution",
          objectiveParam?.GetType().Name ?? "null");
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return;
      }

      if (objectiveDef == null || string.IsNullOrEmpty(objectiveGuid))
      {
        log.ForMethod().Warning("Quest: QuestObjectiveSetNode has null objective, continuing execution");
        foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
          context.EnqueueNext(connection.ToNodeId);
        return;
      }

      // Extract other parameters
      var value = node.Parameters.TryGetValue("Value", out var val) ? Convert.ToInt32(val) : 0;
      var emitEvent = node.Parameters.TryGetValue("EmitProgressEvent", out var emit) && Convert.ToBoolean(emit);

      log.ForMethod().Debug("Quest: Executing QuestObjectiveSetNode (objective: {ObjectiveGuid}, value: {Value}, emitEvent: {EmitEvent})",
        objectiveGuid, value, emitEvent);

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
        progress = new QuestObjectiveProgress
        {
          ObjectiveGuid = objectiveGuid,
          Current = 0,
          Required = 1
        };
        log.ForMethod().Debug("Quest: Initialized new progress for objective {ObjectiveGuid}", objectiveGuid);
      }

      // Set progress to exact value
      var previousValue = progress.Current;
      progress.Current = value;

      // Clamp to required value
      if (progress.Current > progress.Required)
        progress.Current = progress.Required;

      // Store updated progress
      state.Set(key, progress);

      // Log progress
      log.ForMethod().Information("Quest: Objective {ObjectiveGuid} set: {Previous} → {Current}/{Required} ({Percentage:F0}%){Complete}",
        objectiveGuid, previousValue, progress.Current, progress.Required, progress.Percentage * 100, progress.IsComplete ? " ✅ COMPLETE" : "");

      // Emit progress event if enabled
      if (emitEvent)
      {
        log.ForMethod().Debug("Quest: Emitting QuestObjectiveProgressMessage for objective {ObjectiveGuid}", objectiveGuid);

        // Get quest context from state
        var questGuid = state.TryGet<string>("__quest_guid", out var qg) ? qg : null;
        var questDef = state.TryGet<QuestDefinition>("__quest_definition", out var qd) ? qd : null;

        // Find the objective definition
        QuestObjective objectiveDefForMessage = null;
        if (questDef != null && questDef.Objectives != null)
        {
          objectiveDefForMessage = questDef.Objectives.Find(o => o.Guid == objectiveGuid);
        }

        var progressMsg = new QuestObjectiveProgressMessage(
          questGuid ?? string.Empty,
          objectiveGuid ?? string.Empty,
          objectiveDefForMessage,
          progress.Current,
          progress.Required,
          progress.Percentage,
          progress.IsComplete
        );

        context.Emit(progressMsg);
        log.ForMethod().Information("Quest: Emitted QuestObjectiveProgressMessage for objective {ObjectiveGuid} (quest: {QuestGuid})",
          objectiveGuid, questGuid);
      }

      // Continue to connected nodes
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

