using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.AssetLoading;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>
  ///   Executor for StartQuestNode.
  ///   Loads a quest definition and starts it via QuestManager.
  /// </summary>
  public sealed class StartQuestNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<StartQuestNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "StartQuestNode";

    public async UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract QuestAssetReference from parameters
      if (!node.Parameters.TryGetValue("Quest", out var questParam))
      {
        log.ForMethod().Warning("Quest: StartQuestNode has no 'Quest' parameter, continuing execution");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      QuestDefinition questDef = null;
      string questGuid = null;

      // Handle QuestAssetReference directly (may be serialized as the actual type)
      if (questParam is QuestAssetReference questAssetRef)
      {
        questGuid = questAssetRef.AssetGUID;
        log.ForMethod().Debug("Quest: StartQuestNode found QuestAssetReference with GUID: {QuestGuid}", questGuid);

        if (string.IsNullOrEmpty(questGuid))
        {
          log.ForMethod().Warning("Quest: StartQuestNode QuestAssetReference has no GUID, continuing execution");
          ContinueToNextNodes(graph, node, context);
          return;
        }

        // Try to get quest from registry first (may already be loaded)
        questDef = GraphDefinitionRegistry.GetQuestDefinition(questGuid);
        if (questDef != null)
        {
          log.ForMethod().Debug("Quest: Found quest definition in registry: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
        }
        else
        {
          // Load quest via Addressables
          log.ForMethod().Debug("Quest: Quest definition not in registry, loading via Addressables: {QuestGuid}", questGuid);

          try
          {
            // Load directly via Addressables using the QuestAssetReference
            var handle = Addressables.LoadAssetAsync<QuestDefinition>(questAssetRef);
            questDef = await handle.ToUniTask(cancellationToken: ct);

            if (questDef != null)
            {
              // Register in registry for future lookups
              GraphDefinitionRegistry.RegisterQuestDefinition(questDef);
              log.ForMethod().Information("Quest: Loaded and registered quest definition: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
            }
            else
            {
              log.ForMethod().Error("Quest: Failed to load quest definition from Addressables: {QuestGuid}", questGuid);
              ContinueToNextNodes(graph, node, context);
              return;
            }
          }
          catch (OperationCanceledException)
          {
            log.ForMethod().Debug("Quest: Quest load cancelled for {QuestGuid}", questGuid);
            throw;
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Quest: Error loading quest definition from Addressables: {QuestGuid}, {Message}", questGuid, ex.Message);
            ContinueToNextNodes(graph, node, context);
            return;
          }
        }
      }
      // Handle SerializableAssetReference (from QuestAssetReference field)
      else if (questParam is SerializableAssetReference assetRef)
      {
        questGuid = assetRef.AssetGuid;
        log.ForMethod().Debug("Quest: StartQuestNode found QuestAssetReference with GUID: {QuestGuid}", questGuid);

        if (string.IsNullOrEmpty(questGuid))
        {
          log.ForMethod().Warning("Quest: StartQuestNode QuestAssetReference has no GUID, continuing execution");
          ContinueToNextNodes(graph, node, context);
          return;
        }

        // Try to get quest from registry first (may already be loaded)
        questDef = GraphDefinitionRegistry.GetQuestDefinition(questGuid);
        if (questDef != null)
        {
          log.ForMethod().Debug("Quest: Found quest definition in registry: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
        }
        else
        {
          // Load quest via Addressables
          log.ForMethod().Debug("Quest: Quest definition not in registry, loading via Addressables: {QuestGuid}", questGuid);

          try
          {
            // Try to resolve IGraphAssetLoader from context first
            var assetLoader = context.Resolve<IGraphAssetLoader>();
            if (assetLoader != null)
            {
              questDef = await assetLoader.LoadAssetAsync<QuestDefinition>(assetRef, ct);
            }
            else
            {
              // Fallback: load directly via Addressables
              var handle = Addressables.LoadAssetAsync<QuestDefinition>(questGuid);
              questDef = await handle.ToUniTask(cancellationToken: ct);
            }

            if (questDef != null)
            {
              // Register in registry for future lookups
              GraphDefinitionRegistry.RegisterQuestDefinition(questDef);
              log.ForMethod().Information("Quest: Loaded and registered quest definition: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
            }
            else
            {
              log.ForMethod().Error("Quest: Failed to load quest definition from Addressables: {QuestGuid}", questGuid);
              ContinueToNextNodes(graph, node, context);
              return;
            }
          }
          catch (OperationCanceledException)
          {
            log.ForMethod().Debug("Quest: Quest load cancelled for {QuestGuid}", questGuid);
            throw;
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Quest: Error loading quest definition from Addressables: {QuestGuid}, {Message}", questGuid, ex.Message);
            ContinueToNextNodes(graph, node, context);
            return;
          }
        }
      }
      // Fallback: handle direct QuestDefinition reference (for non-Addressables scenarios)
      else if (questParam is QuestDefinition directQuest)
      {
        questDef = directQuest;
        questGuid = questDef.Guid;
        log.ForMethod().Debug("Quest: StartQuestNode found direct QuestDefinition: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
      }
      else
      {
        log.ForMethod().Warning("Quest: StartQuestNode 'Quest' parameter is not a SerializableAssetReference or QuestDefinition (type: {Type}), continuing execution",
          questParam?.GetType().Name ?? "null");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      if (questDef == null || string.IsNullOrEmpty(questGuid))
      {
        log.ForMethod().Warning("Quest: StartQuestNode has null or invalid quest definition, continuing execution");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      // Resolve QuestManager from context
      var questManager = context.Resolve<IQuestManager>();
      if (questManager == null)
      {
        log.ForMethod().Error("Quest: IQuestManager not found in DI container. Cannot start quest: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
        ContinueToNextNodes(graph, node, context);
        return;
      }

      // Start the quest
      log.ForMethod().Information("Quest: Starting quest: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);

      try
      {
        var success = await questManager.StartQuestAsync(questDef, ct);

        if (success)
        {
          log.ForMethod().Information("Quest: Successfully started quest: {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
        }
        else
        {
          log.ForMethod().Warning("Quest: Failed to start quest (may already be active): {QuestName} ({QuestGuid})", questDef.DisplayName, questGuid);
        }
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Debug("Quest: Quest start cancelled for {QuestGuid}", questGuid);
        throw;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Quest: Error starting quest: {QuestName} ({QuestGuid}), {Message}", questDef.DisplayName, questGuid, ex.Message);
      }

      // Clear any existing Dialogue.NextNodeIds from state to ensure automatic continuation
      // This node should not pause dialogue execution - it's a quest action, not a dialogue node
      if (graph.GraphDomain == "Dialogue")
      {
        if (state.TryGet<List<string>>("Dialogue.NextNodeIds", out var existingNextNodes) &&
            existingNextNodes != null && existingNextNodes.Count > 0)
        {
          log.ForMethod().Debug("Quest: Clearing existing Dialogue.NextNodeIds from state to ensure automatic continuation");
          state.Set<List<string>>("Dialogue.NextNodeIds", new List<string>());
        }
      }

      // Continue to connected nodes - enqueue directly so execution continues immediately
      ContinueToNextNodes(graph, node, context);
    }

    private void ContinueToNextNodes(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, GraphNodeExecutionContext context)
    {
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

