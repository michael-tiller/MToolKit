using System;
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
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>
  ///   Executor for QuestStateCheckNode.
  ///   Branches execution based on quest state (NotStarted, Active, Complete, Claimed).
  ///   Note: "NotStarted" includes both never-started quests and abandoned quests,
  ///   as QuestManager does not track abandoned quests separately.
  /// </summary>
  public sealed class QuestStateCheckNodeExecutor : IGraphNodeExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<QuestStateCheckNodeExecutor>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public string NodeType => "QuestStateCheckNode";

    public UniTask Execute(
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
        log.ForMethod().Warning("Quest: QuestStateCheckNode has no 'Quest' parameter, continuing to all outputs");
        ContinueToAllOutputs(graph, node, context);
        return UniTask.CompletedTask;
      }

      string questGuid = null;

      // Handle QuestAssetReference directly (may be serialized as the actual type)
      if (questParam is QuestAssetReference questAssetRef)
      {
        questGuid = questAssetRef.AssetGUID;
        log.ForMethod().Debug("Quest: QuestStateCheckNode found QuestAssetReference with GUID: {QuestGuid}", questGuid);

        if (string.IsNullOrEmpty(questGuid))
        {
          log.ForMethod().Warning("Quest: QuestStateCheckNode QuestAssetReference has no GUID, treating as NotStarted");
          BranchToOutput(graph, node, context, "NotStarted");
          return UniTask.CompletedTask;
        }
      }
      // Handle SerializableAssetReference (from QuestAssetReference field)
      else if (questParam is SerializableAssetReference assetRef)
      {
        questGuid = assetRef.AssetGuid;
        log.ForMethod().Debug("Quest: QuestStateCheckNode found QuestAssetReference with GUID: {QuestGuid}", questGuid);

        if (string.IsNullOrEmpty(questGuid))
        {
          log.ForMethod().Warning("Quest: QuestStateCheckNode QuestAssetReference has no GUID, treating as NotStarted");
          BranchToOutput(graph, node, context, "NotStarted");
          return UniTask.CompletedTask;
        }
      }
      // Fallback: handle direct QuestDefinition reference
      else if (questParam is QuestDefinition directQuest)
      {
        questGuid = directQuest.Guid;
        log.ForMethod().Debug("Quest: QuestStateCheckNode found direct QuestDefinition: {QuestName} ({QuestGuid})", directQuest.DisplayName, questGuid);
        return UniTask.CompletedTask;
      }
      else
      {
        log.ForMethod().Warning("Quest: QuestStateCheckNode 'Quest' parameter is not a SerializableAssetReference or QuestDefinition (type: {Type}), treating as NotStarted",
          questParam?.GetType().Name ?? "null");
        BranchToOutput(graph, node, context, "NotStarted");
        return UniTask.CompletedTask;
      }

      if (string.IsNullOrEmpty(questGuid))
      {
        log.ForMethod().Warning("Quest: QuestStateCheckNode has null or invalid quest GUID, treating as NotStarted");
        BranchToOutput(graph, node, context, "NotStarted");
        return UniTask.CompletedTask;
      }

      // Resolve QuestManager from context
      var questManager = context.Resolve<IQuestManager>();
      if (questManager == null)
      {
        log.ForMethod().Error("Quest: IQuestManager not found in DI container. Cannot check quest state for: {QuestGuid}", questGuid);
        BranchToOutput(graph, node, context, "NotStarted");
        return UniTask.CompletedTask;
      }

      // Determine quest state
      string targetPort;

      if (questManager.IsQuestClaimed(questGuid))
      {
        targetPort = "Claimed";
        log.ForMethod().Debug("Quest: Quest {QuestGuid} is Claimed", questGuid);
      }
      else if (questManager.IsQuestCompleted(questGuid))
      {
        targetPort = "Complete";
        log.ForMethod().Debug("Quest: Quest {QuestGuid} is Complete (unclaimed)", questGuid);
      }
      else if (questManager.IsQuestActive(questGuid))
      {
        targetPort = "Active";
        log.ForMethod().Debug("Quest: Quest {QuestGuid} is Active", questGuid);
      }
      else
      {
        // Quest is either never started or abandoned (QuestManager doesn't distinguish)
        targetPort = "NotStarted";
        log.ForMethod().Debug("Quest: Quest {QuestGuid} is NotStarted (or abandoned)", questGuid);
      }

      log.ForMethod().Information("Quest: QuestStateCheckNode branching to '{TargetPort}' port for quest {QuestGuid}", targetPort, questGuid);

      // Enqueue next nodes directly - this node should not pause dialogue execution
      // The GraphRunner will continue processing these nodes in the same execution loop
      // since QuestStateCheckNode is not a DialogueLineNode or DialogueChoiceNode
      BranchToOutput(graph, node, context, targetPort);
      return UniTask.CompletedTask;
    }

    private void BranchToOutput(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, GraphNodeExecutionContext context, string portName)
    {
      // Filter connections manually to avoid LINQ allocation in hot path
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
      {
        if (connection.PortName == portName)
          context.EnqueueNext(connection.ToNodeId);
      }
    }

    private void ContinueToAllOutputs(IRuntimeGraphDefinition graph, RuntimeNodeDefinition node, GraphNodeExecutionContext context)
    {
      foreach (var connection in graph.GetConnectionsFrom(node.NodeId))
        context.EnqueueNext(connection.ToNodeId);
    }
  }
}

