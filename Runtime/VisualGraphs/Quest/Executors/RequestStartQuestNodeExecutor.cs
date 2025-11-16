using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime;
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
  ///   Executor for RequestStartQuestNode.
  ///   Loads a quest definition and publishes StartQuestRequestMessage via GameMessageBroker.
  /// </summary>
  public sealed class RequestStartQuestNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<RequestStartQuestNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "RequestStartQuestNode";

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
        log.ForMethod().Warning("Quest: RequestStartQuestNode has no 'Quest' parameter, continuing execution");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      QuestDefinition questDef = null;

      // Handle QuestAssetReference directly (may be serialized as the actual type)
      if (questParam is QuestAssetReference questAssetRef)
      {
        if (string.IsNullOrEmpty(questAssetRef.AssetGUID))
        {
          log.ForMethod().Warning("Quest: RequestStartQuestNode QuestAssetReference has no GUID, continuing execution");
          ContinueToNextNodes(graph, node, context);
          return;
        }

        // Try to get quest from registry first (may already be loaded)
        questDef = GraphDefinitionRegistry.GetQuestDefinition(questAssetRef.AssetGUID);
        if (questDef == null)
        {
          // Load quest via Addressables
          try
          {
            var handle = Addressables.LoadAssetAsync<QuestDefinition>(questAssetRef);
            questDef = await handle.ToUniTask(cancellationToken: ct);

            if (questDef != null)
            {
              GraphDefinitionRegistry.RegisterQuestDefinition(questDef);
              log.ForMethod().Information("Quest: Loaded and registered quest definition: {QuestName} ({QuestGuid})", questDef.DisplayName, questDef.Guid);
            }
            else
            {
              log.ForMethod().Error("Quest: Failed to load quest definition from Addressables: {QuestGuid}", questAssetRef.AssetGUID);
              ContinueToNextNodes(graph, node, context);
              return;
            }
          }
          catch (OperationCanceledException)
          {
            throw;
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Quest: Error loading quest definition from Addressables: {QuestGuid}, {Message}", questAssetRef.AssetGUID, ex.Message);
            ContinueToNextNodes(graph, node, context);
            return;
          }
        }
      }
      // Handle SerializableAssetReference (from QuestAssetReference field)
      else if (questParam is SerializableAssetReference assetRef)
      {
        if (string.IsNullOrEmpty(assetRef.AssetGuid))
        {
          log.ForMethod().Warning("Quest: RequestStartQuestNode SerializableAssetReference has no GUID, continuing execution");
          ContinueToNextNodes(graph, node, context);
          return;
        }

        // Try to get quest from registry first (may already be loaded)
        questDef = GraphDefinitionRegistry.GetQuestDefinition(assetRef.AssetGuid);
        if (questDef == null)
        {
          // Load quest via Addressables
          try
          {
            var handle = Addressables.LoadAssetAsync<QuestDefinition>(assetRef.AssetGuid);
            questDef = await handle.ToUniTask(cancellationToken: ct);

            if (questDef != null)
            {
              GraphDefinitionRegistry.RegisterQuestDefinition(questDef);
              log.ForMethod().Information("Quest: Loaded and registered quest definition: {QuestName} ({QuestGuid})", questDef.DisplayName, questDef.Guid);
            }
            else
            {
              log.ForMethod().Error("Quest: Failed to load quest definition from Addressables: {QuestGuid}", assetRef.AssetGuid);
              ContinueToNextNodes(graph, node, context);
              return;
            }
          }
          catch (OperationCanceledException)
          {
            throw;
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Quest: Error loading quest definition from Addressables: {QuestGuid}, {Message}", assetRef.AssetGuid, ex.Message);
            ContinueToNextNodes(graph, node, context);
            return;
          }
        }
      }
      else
      {
        log.ForMethod().Warning("Quest: RequestStartQuestNode 'Quest' parameter is not a QuestAssetReference or SerializableAssetReference (type: {Type}), continuing execution",
          questParam?.GetType().Name ?? "null");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      if (questDef == null)
      {
        log.ForMethod().Warning("Quest: RequestStartQuestNode has null quest definition, continuing execution");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      // Publish StartQuestRequestMessage via GameMessageBroker
      log.ForMethod().Information("Quest: Publishing StartQuestRequestMessage for quest: {QuestName} ({QuestGuid})", questDef.DisplayName, questDef.Guid);

      try
      {
        GameMessageBroker.Publish(new StartQuestRequestMessage(questDef));
        log.ForMethod().Information("Quest: Successfully published StartQuestRequestMessage for quest: {QuestName} ({QuestGuid})", questDef.DisplayName, questDef.Guid);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Quest: Error publishing StartQuestRequestMessage for quest: {QuestName} ({QuestGuid}), {Message}", questDef.DisplayName, questDef.Guid, ex.Message);
      }

      // Continue to connected nodes
      ContinueToNextNodes(graph, node, context);
    }

    private void ContinueToNextNodes(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, GraphNodeExecutionContext context)
    {
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

