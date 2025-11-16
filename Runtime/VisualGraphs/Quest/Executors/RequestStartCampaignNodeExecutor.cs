using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.MessageBus.Interfaces;
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
  ///   Executor for RequestStartCampaignNode.
  ///   Loads a campaign definition and publishes StartCampaignRequestMessage via GameMessageBroker.
  /// </summary>
  public sealed class RequestStartCampaignNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<RequestStartCampaignNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "RequestStartCampaignNode";

    public async UniTask Execute(
      IRuntimeGraphDefinition graph,
      RuntimeNodeDefinition node,
      IGraphState state,
      IGameMessage message,
      GraphNodeExecutionContext context,
      CancellationToken ct = default)
    {
      // Extract CampaignAssetReference from parameters
      if (!node.Parameters.TryGetValue("Campaign", out var campaignParam))
      {
        log.ForMethod().Warning("Quest: RequestStartCampaignNode has no 'Campaign' parameter, continuing execution");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      QuestCampaign campaignDef = null;

      // Handle CampaignAssetReference directly (may be serialized as the actual type)
      if (campaignParam is CampaignAssetReference campaignAssetRef)
      {
        if (string.IsNullOrEmpty(campaignAssetRef.AssetGUID))
        {
          log.ForMethod().Warning("Quest: RequestStartCampaignNode CampaignAssetReference has no GUID, continuing execution");
          ContinueToNextNodes(graph, node, context);
          return;
        }

        try
        {
          var handle = Addressables.LoadAssetAsync<QuestCampaign>(campaignAssetRef);
          campaignDef = await handle.ToUniTask(cancellationToken: ct);

          if (campaignDef == null)
          {
            log.ForMethod().Error("Quest: Failed to load campaign definition from Addressables: {CampaignGuid}", campaignAssetRef.AssetGUID);
            ContinueToNextNodes(graph, node, context);
            return;
          }

          log.ForMethod().Information("Quest: Loaded campaign definition: {CampaignName} ({CampaignGuid})", campaignDef.DisplayName, campaignDef.Guid);
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Quest: Error loading campaign definition from Addressables: {CampaignGuid}, {Message}", campaignAssetRef.AssetGUID, ex.Message);
          ContinueToNextNodes(graph, node, context);
          return;
        }
      }
      // Handle SerializableAssetReference (from CampaignAssetReference field)
      else if (campaignParam is SerializableAssetReference assetRef)
      {
        if (string.IsNullOrEmpty(assetRef.AssetGuid))
        {
          log.ForMethod().Warning("Quest: RequestStartCampaignNode SerializableAssetReference has no GUID, continuing execution");
          ContinueToNextNodes(graph, node, context);
          return;
        }

        try
        {
          var handle = Addressables.LoadAssetAsync<QuestCampaign>(assetRef.AssetGuid);
          campaignDef = await handle.ToUniTask(cancellationToken: ct);

          if (campaignDef == null)
          {
            log.ForMethod().Error("Quest: Failed to load campaign definition from Addressables: {CampaignGuid}", assetRef.AssetGuid);
            ContinueToNextNodes(graph, node, context);
            return;
          }

          log.ForMethod().Information("Quest: Loaded campaign definition: {CampaignName} ({CampaignGuid})", campaignDef.DisplayName, campaignDef.Guid);
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Quest: Error loading campaign definition from Addressables: {CampaignGuid}, {Message}", assetRef.AssetGuid, ex.Message);
          ContinueToNextNodes(graph, node, context);
          return;
        }
      }
      else
      {
        log.ForMethod().Warning("Quest: RequestStartCampaignNode 'Campaign' parameter is not a CampaignAssetReference or SerializableAssetReference (type: {Type}), continuing execution",
          campaignParam?.GetType().Name ?? "null");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      if (campaignDef == null)
      {
        log.ForMethod().Warning("Quest: RequestStartCampaignNode has null campaign definition, continuing execution");
        ContinueToNextNodes(graph, node, context);
        return;
      }

      // Publish StartCampaignRequestMessage via GameMessageBroker
      log.ForMethod().Information("Quest: Publishing StartCampaignRequestMessage for campaign: {CampaignName} ({CampaignGuid})", campaignDef.DisplayName, campaignDef.Guid);

      try
      {
        GameMessageBroker.Publish(new StartCampaignRequestMessage(campaignDef));
        log.ForMethod().Information("Quest: Successfully published StartCampaignRequestMessage for campaign: {CampaignName} ({CampaignGuid})", campaignDef.DisplayName, campaignDef.Guid);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Quest: Error publishing StartCampaignRequestMessage for campaign: {CampaignName} ({CampaignGuid}), {Message}", campaignDef.DisplayName, campaignDef.Guid, ex.Message);
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

