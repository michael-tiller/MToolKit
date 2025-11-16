using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Config;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Game-agnostic quest lifecycle and state management service.
  /// Manages active quests, objective graph loading, and progress tracking.
  /// </summary>
  [Serializable]
  public sealed class QuestManager : IQuestManager, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<QuestManager>().ForFeature("VisualGraphs.Quest"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly GraphEventRouter eventRouter;
    private readonly NodeExecutorRegistry executorRegistry;
    private readonly IServiceProvider services;
    private readonly IEventEmitter eventEmitter;
    private readonly IPublisher<QuestStartedMessage> questStartedPublisher;
    private readonly IPublisher<QuestCompletedMessage> questCompletedPublisher;
    private readonly IPublisher<QuestAbandonedMessage> questAbandonedPublisher;
    private readonly IPublisher<QuestClaimedMessage> questClaimedPublisher;

    // Quest lifecycle tracking

    [ShowInInspector]
    [ReadOnly]
    private readonly Dictionary<string, QuestRuntimeState> activeQuests = new();
    [ShowInInspector]
    [ReadOnly]
    private readonly Dictionary<string, QuestRuntimeState> completedUnclaimedQuests = new();
    [ShowInInspector]
    [ReadOnly]
    private readonly HashSet<string> claimedQuestGuids = new();

    // Graph runner tracking
    private readonly Dictionary<string, IGraphRunner> loadedRunners = new();

    // Subscription tracking
    private CompositeDisposable subscriptions = new();

    public QuestManager(
        GraphEventRouter eventRouter,
        NodeExecutorRegistry executorRegistry,
        IServiceProvider services,
        IEventEmitter eventEmitter,
        IPublisher<QuestStartedMessage> questStartedPublisher,
        IPublisher<QuestCompletedMessage> questCompletedPublisher,
        IPublisher<QuestAbandonedMessage> questAbandonedPublisher,
        IPublisher<QuestClaimedMessage> questClaimedPublisher)
    {
      this.eventRouter = eventRouter ?? throw new ArgumentNullException(nameof(eventRouter));
      this.executorRegistry = executorRegistry ?? throw new ArgumentNullException(nameof(executorRegistry));
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
      this.questStartedPublisher = questStartedPublisher ?? throw new ArgumentNullException(nameof(questStartedPublisher));
      this.questCompletedPublisher = questCompletedPublisher ?? throw new ArgumentNullException(nameof(questCompletedPublisher));
      this.questAbandonedPublisher = questAbandonedPublisher ?? throw new ArgumentNullException(nameof(questAbandonedPublisher));
      this.questClaimedPublisher = questClaimedPublisher ?? throw new ArgumentNullException(nameof(questClaimedPublisher));

      // Subscribe to quest progress updates (lazy - will retry if broker not ready)
      EnsureProgressSubscription();
    }
    public void Dispose()
    {
      if (this == null)
        return;
      foreach (var subscription in subscriptions?.ToList() ?? new List<IDisposable>())
      {
        subscription?.Dispose();
      }
      subscriptions?.Clear();
      subscriptions = null;
    }

    private void EnsureProgressSubscription()
    {
      // Skip if already subscribed
      if (subscriptions.Count > 0)
      {
        log.ForMethod().Information("Quest: Already subscribed to QuestObjectiveProgressMessage");
        return;
      }

      // Subscribe to quest completed updates
      var completedSubscriber = GameMessageBroker.GetSubscriber<QuestCompletedMessage>();
      if (completedSubscriber != null)
      {
        subscriptions.Add(completedSubscriber.Subscribe(OnQuestCompleted));
        log.ForMethod().Information("Quest: Successfully subscribed to QuestCompletedMessage (subscriber type: {SubscriberType})",
          completedSubscriber.GetType().FullName);
      }
      else
      {
        log.ForMethod().Warning("Quest: GameMessageBroker.GetSubscriber returned null - broker may not be initialized yet");
      }

      // Try to subscribe to quest progress updates
      var progressSubscriber = GameMessageBroker.GetSubscriber<QuestObjectiveProgressMessage>();
      if (progressSubscriber != null)
      {
        subscriptions.Add(progressSubscriber.Subscribe(OnQuestObjectiveProgress));
        log.ForMethod().Information("Quest: Successfully subscribed to QuestObjectiveProgressMessage (subscriber type: {SubscriberType})",
          progressSubscriber.GetType().FullName);
      }
      else
      {
        log.ForMethod().Warning("Quest: GameMessageBroker.GetSubscriber returned null - broker may not be initialized yet");
      }

      var abandonedSubscriber = GameMessageBroker.GetSubscriber<QuestAbandonedMessage>();
      if (abandonedSubscriber != null)
      {
        subscriptions.Add(abandonedSubscriber.Subscribe(OnQuestAbandoned));
        log.ForMethod().Information("Quest: Successfully subscribed to QuestAbandonedMessage (subscriber type: {SubscriberType})",
          abandonedSubscriber.GetType().FullName);
      }
      else
      {
        log.ForMethod().Warning("Quest: GameMessageBroker.GetSubscriber returned null - broker may not be initialized yet");
      }

      var claimedSubscriber = GameMessageBroker.GetSubscriber<QuestClaimedMessage>();
      if (claimedSubscriber != null)
      {
        subscriptions.Add(claimedSubscriber.Subscribe(OnQuestClaimed));
        log.ForMethod().Information("Quest: Successfully subscribed to QuestClaimedMessage (subscriber type: {SubscriberType})",
          claimedSubscriber.GetType().FullName);
      }
      else
      {
        log.ForMethod().Warning("Quest: GameMessageBroker.GetSubscriber returned null - broker may not be initialized yet");
      }
      var startedSubscriber = GameMessageBroker.GetSubscriber<QuestStartedMessage>();
      if (startedSubscriber != null)
      {
        subscriptions.Add(startedSubscriber.Subscribe(OnQuestStarted));
        log.ForMethod().Information("Quest: Successfully subscribed to QuestStartedMessage (subscriber type: {SubscriberType})",
          startedSubscriber.GetType().FullName);
      }
      else
      {
        log.ForMethod().Warning("Quest: GameMessageBroker.GetSubscriber returned null - broker may not be initialized yet");
      }
    }

    // ==================== LIFECYCLE ====================

    public async UniTask<bool> StartQuestAsync(QuestDefinition quest, CancellationToken ct = default)
    {
      if (quest == null)
      {
        log.ForMethod().Error("Quest: Cannot start null quest");
        return false;
      }

      var questGuid = quest.Guid;
      log.ForMethod().Information("Quest: Starting quest '{QuestName}' (GUID: {QuestGuid})", quest.DisplayName, questGuid);

      // Ensure we're subscribed to progress updates (in case broker wasn't ready during construction)
      EnsureProgressSubscription();

      if (activeQuests.ContainsKey(questGuid))
      {
        log.ForMethod().Warning("Quest: Quest '{QuestName}' ({QuestGuid}) is already active", quest.DisplayName, questGuid);
        return false;
      }

      if (completedUnclaimedQuests.ContainsKey(questGuid))
      {
        log.ForMethod().Warning("Quest: Quest '{QuestName}' ({QuestGuid}) is completed but unclaimed", quest.DisplayName, questGuid);
        return false;
      }

      if (claimedQuestGuids.Contains(questGuid))
      {
        log.ForMethod().Warning("Quest: Quest '{QuestName}' ({QuestGuid}) is already claimed", quest.DisplayName, questGuid);
        return false;
      }

      // Create runtime state
      var graphState = new InMemoryGraphState();

      // Store quest context in graph state for executors to access
      graphState.Set("__quest_guid", questGuid);
      graphState.Set("__quest_definition", quest);
      log.ForMethod().Debug("Quest: Stored quest context in graph state (quest_guid: {QuestGuid})", questGuid);

      var runtimeState = new QuestRuntimeState(
          questGuid,
          quest,
          graphState,
          DateTime.UtcNow
      );

      activeQuests[questGuid] = runtimeState;

      // Load objective graphs
      log.ForMethod().Information("Quest: Loading {ObjectiveCount} objective graphs for quest '{QuestName}'", quest.Objectives.Count, quest.DisplayName);
      await LoadObjectiveGraphsAsync(quest, runtimeState, ct);

      // Optionally load quest-level graph if it exists
      if (quest.GraphAsset != null)
      {
        try
        {
          log.ForMethod().Debug("Quest: Loading quest-level graph for '{QuestName}'", quest.DisplayName);
          var runnerId = $"quest_{questGuid}";
          var runner = await LoadGraphAsync(quest.GraphAsset, runnerId, graphState, ct);
          loadedRunners[runnerId] = runner;
          eventRouter.RegisterRunner(runner);
          runtimeState.LoadedGraphInstanceIds.Add(runnerId);
          log.ForMethod().Information("Quest: Loaded quest graph for '{QuestName}' (runner: {RunnerId})", quest.DisplayName, runnerId);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Quest: Failed to load quest graph for '{QuestName}'", quest.DisplayName);
        }
      }

      // Emit message
      questStartedPublisher.Publish(new QuestStartedMessage(questGuid, quest));
      log.ForMethod().Debug("Quest: Published QuestStartedMessage for '{QuestName}'", quest.DisplayName);

      log.ForMethod().Information("Quest: Successfully started quest '{QuestName}' ({QuestGuid}) with {ObjectiveCount} objectives",
        quest.DisplayName, questGuid, quest.Objectives.Count);
      return true;
    }

    public bool CompleteQuest(string questGuid)
    {
      log.ForMethod().Information("Quest: Completing quest {QuestGuid}", questGuid);

      if (!activeQuests.TryGetValue(questGuid, out var runtimeState))
      {
        log.ForMethod().Warning("Quest: Cannot complete quest {QuestGuid} - not active", questGuid);
        return false;
      }

      var duration = DateTime.UtcNow - runtimeState.StartedAt;
      log.ForMethod().Debug("Quest: Quest '{QuestName}' duration: {Duration} minutes", runtimeState.Definition.DisplayName, duration.TotalMinutes);

      // Unload all objective graphs (but keep quest-level graph if it exists)
      UnloadQuestGraphs(runtimeState);

      // Move from active to completed-unclaimed
      activeQuests.Remove(questGuid);
      completedUnclaimedQuests[questGuid] = runtimeState;

      // Emit message (objectives done, but NOT claimed yet)
      questCompletedPublisher.Publish(new QuestCompletedMessage(
          questGuid,
          runtimeState.Definition,
          duration
      ));
      log.ForMethod().Debug("Quest: Published QuestCompletedMessage for '{QuestName}'", runtimeState.Definition.DisplayName);

      log.ForMethod().Information("Quest: Completed quest '{QuestName}' ({QuestGuid}) in {Duration:F1} minutes - ready to claim",
        runtimeState.Definition.DisplayName, questGuid, duration.TotalMinutes);
      return true;
    }

    public bool ClaimQuest(string questGuid)
    {
      log.ForMethod().Information("Quest: Claiming quest {QuestGuid}", questGuid);

      if (!completedUnclaimedQuests.TryGetValue(questGuid, out var runtimeState))
      {
        log.ForMethod().Warning("Quest: Cannot claim quest {QuestGuid} - not in completed state", questGuid);
        return false;
      }

      var totalDuration = DateTime.UtcNow - runtimeState.StartedAt;

      // Move from completed-unclaimed to claimed
      completedUnclaimedQuests.Remove(questGuid);
      claimedQuestGuids.Add(questGuid);

      // Emit message (THIS is when game should grant rewards!)
      questClaimedPublisher.Publish(new QuestClaimedMessage(
          questGuid,
          runtimeState.Definition,
          totalDuration
      ));
      log.ForMethod().Debug("Quest: Published QuestClaimedMessage for '{QuestName}'", runtimeState.Definition.DisplayName);

      log.ForMethod().Information("Quest: Claimed quest '{QuestName}' ({QuestGuid}) - rewards should be granted now",
        runtimeState.Definition.DisplayName, questGuid);
      return true;
    }

    public bool AbandonQuest(string questGuid)
    {
      log.ForMethod().Information("Quest: Abandoning quest {QuestGuid}", questGuid);

      if (!activeQuests.TryGetValue(questGuid, out var runtimeState))
      {
        log.ForMethod().Warning("Quest: Cannot abandon quest {QuestGuid} - not active", questGuid);
        return false;
      }

      // Check if quest can be abandoned
      if (!runtimeState.Definition.IsAbandonable)
      {
        log.ForMethod().Warning("Quest: Cannot abandon quest '{QuestName}' ({QuestGuid}) - quest is not abandonable",
          runtimeState.Definition.DisplayName, questGuid);
        return false;
      }

      var progressWhenAbandoned = runtimeState.GetCompletionPercentage();

      // Unload all objective graphs
      UnloadQuestGraphs(runtimeState);

      // Remove from active (does NOT add to completed)
      activeQuests.Remove(questGuid);

      // Emit message
      questAbandonedPublisher.Publish(new QuestAbandonedMessage(
          questGuid,
          runtimeState.Definition,
          progressWhenAbandoned
      ));
      log.ForMethod().Debug("Quest: Published QuestAbandonedMessage for '{QuestName}'", runtimeState.Definition.DisplayName);

      log.ForMethod().Information("Quest: Abandoned quest '{QuestName}' ({QuestGuid}) at {Progress:F0}% completion",
        runtimeState.Definition.DisplayName, questGuid, progressWhenAbandoned * 100);
      return true;
    }

    // ==================== QUERIES ====================

    public IReadOnlyList<QuestRuntimeState> GetActiveQuests()
    {
      return activeQuests.Values.ToList();
    }

    public IReadOnlyList<QuestRuntimeState> GetCompletedUnclaimedQuests()
    {
      return completedUnclaimedQuests.Values.ToList();
    }

    public IReadOnlyList<string> GetClaimedQuestGuids()
    {
      return claimedQuestGuids.ToList();
    }

    public bool IsQuestActive(string questGuid)
    {
      return activeQuests.ContainsKey(questGuid);
    }

    public bool IsQuestCompleted(string questGuid)
    {
      return completedUnclaimedQuests.ContainsKey(questGuid) || claimedQuestGuids.Contains(questGuid);
    }

    public bool IsQuestClaimed(string questGuid)
    {
      return claimedQuestGuids.Contains(questGuid);
    }

    public QuestRuntimeState GetQuestState(string questGuid)
    {
      // Check active first
      if (activeQuests.TryGetValue(questGuid, out var activeState))
      {
        return activeState;
      }

      // Then check completed-unclaimed
      if (completedUnclaimedQuests.TryGetValue(questGuid, out var completedState))
      {
        return completedState;
      }

      // Quest is either claimed or not started
      return null;
    }

    public float GetQuestCompletionPercentage(string questGuid)
    {
      // Check active quests
      if (activeQuests.TryGetValue(questGuid, out var activeState))
      {
        return activeState.GetCompletionPercentage();
      }

      // Check completed-unclaimed quests
      if (completedUnclaimedQuests.TryGetValue(questGuid, out var completedState))
      {
        return 1.0f; // Completed = 100%
      }

      // Claimed or not started
      return 0f;
    }

    // ==================== PERSISTENCE SUPPORT ====================

    public QuestManagerSaveData GetSaveData()
    {
      var saveData = new QuestManagerSaveData
      {
        ClaimedQuestGuids = claimedQuestGuids.ToList()
      };

      // Save active quests
      foreach (var kvp in activeQuests)
      {
        var questGuid = kvp.Key;
        var runtimeState = kvp.Value;

        // Serialize graph state (game can implement custom serialization)
        var serializedState = SerializeGraphState(runtimeState.GraphState);

        saveData.ActiveQuests.Add(new ActiveQuestSaveData
        {
          QuestGuid = questGuid,
          StartedAt = runtimeState.StartedAt,
          SerializedGraphState = serializedState
        });
      }

      // Save completed-unclaimed quests
      foreach (var kvp in completedUnclaimedQuests)
      {
        var questGuid = kvp.Key;
        var runtimeState = kvp.Value;

        var serializedState = SerializeGraphState(runtimeState.GraphState);

        saveData.CompletedUnclaimedQuests.Add(new ActiveQuestSaveData
        {
          QuestGuid = questGuid,
          StartedAt = runtimeState.StartedAt,
          SerializedGraphState = serializedState
        });
      }

      return saveData;
    }

    public async UniTask RestoreSaveDataAsync(QuestManagerSaveData saveData, VisualGraphRegistry registry = null, CancellationToken ct = default)
    {
      if (saveData == null)
      {
        Debug.LogWarning("[QuestManager] Cannot restore null save data");
        return;
      }

      // Clear current state
      foreach (var runtimeState in activeQuests.Values)
      {
        UnloadQuestGraphs(runtimeState);
      }
      foreach (var runtimeState in completedUnclaimedQuests.Values)
      {
        UnloadQuestGraphs(runtimeState);
      }
      activeQuests.Clear();
      completedUnclaimedQuests.Clear();

      // Restore claimed quests
      claimedQuestGuids.Clear();
      claimedQuestGuids.UnionWith(saveData.ClaimedQuestGuids);

      // Restore active quests
      foreach (var activeQuestData in saveData.ActiveQuests)
      {
        try
        {
          // Wait for quest definition to be available (it might still be loading from addressables)
          // Pass registry to enable dynamic loading if quest isn't found
          var questDef = await WaitForQuestDefinitionAsync(activeQuestData.QuestGuid, registry, ct);
          if (questDef != null)
          {
            // Start the quest (this will create runtime state and load graphs)
            var started = await StartQuestAsync(questDef, ct);
            if (started)
            {
              // Restore the started time
              if (activeQuests.TryGetValue(activeQuestData.QuestGuid, out var runtimeState))
              {
                // Note: StartedAt is readonly, so we can't restore it directly
                // The quest will have a new StartedAt time, which is acceptable
                log.ForMethod().Information("Restored active quest '{QuestName}' ({QuestGuid})",
                  questDef.DisplayName, activeQuestData.QuestGuid);
              }
            }
            else
            {
              log.ForMethod().Warning("Failed to start quest '{QuestName}' ({QuestGuid}) during restoration",
                questDef.DisplayName, activeQuestData.QuestGuid);
            }
          }
          else
          {
            log.ForMethod().Warning("QuestDefinition not found for GUID {QuestGuid} after waiting - cannot restore active quest",
              activeQuestData.QuestGuid);
          }
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Error restoring active quest {QuestGuid}: {Message}",
            activeQuestData.QuestGuid, ex.Message);
        }
      }

      // Restore completed-unclaimed quests
      // NOTE: We restore these directly without starting them, to avoid auto-completion
      // The graph state will be restored first, then we'll mark them as completed
      log.ForMethod().Information("Restoring {Count} completed-unclaimed quest(s) from save data", saveData.CompletedUnclaimedQuests.Count);
      var restoredCompletedCount = 0;
      var failedCompletedCount = 0;

      foreach (var completedQuestData in saveData.CompletedUnclaimedQuests)
      {
        try
        {
          log.ForMethod().Information("Attempting to restore completed quest with GUID {QuestGuid}", completedQuestData.QuestGuid);

          // Wait for quest definition to be available (it might still be loading from addressables)
          // Pass registry to enable dynamic loading if quest isn't found
          var questDef = await WaitForQuestDefinitionAsync(completedQuestData.QuestGuid, registry, ct);
          if (questDef != null)
          {
            log.ForMethod().Information("Found quest definition '{QuestName}' for GUID {QuestGuid} - restoring directly",
              questDef.DisplayName, completedQuestData.QuestGuid);

            // Restore the quest directly to completedUnclaimedQuests (skip active state entirely)
            // Graphs are loaded so that graph state can be restored
            await RestoreCompletedQuestDirectlyAsync(questDef, completedQuestData.StartedAt, ct);
            restoredCompletedCount++;
            log.ForMethod().Information("Successfully restored completed-unclaimed quest '{QuestName}' ({QuestGuid}) directly to completed state",
              questDef.DisplayName, completedQuestData.QuestGuid);
          }
          else
          {
            failedCompletedCount++;
            log.ForMethod().Error("QuestDefinition not found for GUID {QuestGuid} after waiting - cannot restore completed quest. " +
              "This quest will be lost. Check that the quest definition is registered in the GraphDefinitionRegistry or available in Resources/Addressables.",
              completedQuestData.QuestGuid);
          }
        }
        catch (Exception ex)
        {
          failedCompletedCount++;
          log.ForMethod().Error(ex, "Error restoring completed quest {QuestGuid}: {Message}. This quest will be lost.",
            completedQuestData.QuestGuid, ex.Message);
        }
      }

      log.ForMethod().Information("Completed quest restoration summary: {RestoredCount} restored, {FailedCount} failed, {TotalCount} total",
        restoredCompletedCount, failedCompletedCount, saveData.CompletedUnclaimedQuests.Count);

      log.ForMethod().Information("Restored save data: {ActiveQuestCount} active quests, {CompletedUnclaimedQuestCount} completed-unclaimed quests, {ClaimedQuestCount} claimed quests",
        activeQuests.Count, completedUnclaimedQuests.Count, claimedQuestGuids.Count);

      await UniTask.CompletedTask;
    }

    /// <summary>
    /// Marks quests as completed after graph state restoration.
    /// This is called by GraphStateSaveController after restoring graph states,
    /// so that completed quests don't unload their graphs before state is restored.
    /// </summary>
    public void FinalizeCompletedQuestRestoration(IEnumerable<string> completedQuestGuids)
    {
      foreach (var questGuid in completedQuestGuids)
      {
        if (activeQuests.TryGetValue(questGuid, out var runtimeState))
        {
          // Log completion percentage before finalizing
          var completionBefore = runtimeState.GetCompletionPercentage();
          log.ForMethod().Information("Finalizing restoration of completed quest {QuestGuid} - completion before: {Completion}%",
            questGuid, completionBefore * 100);

          // Only complete if it's still active (was restored but not yet completed)
          CompleteQuest(questGuid);
          log.ForMethod().Information("Finalized restoration of completed quest {QuestGuid}", questGuid);
        }
        else
        {
          log.ForMethod().Warning("Cannot finalize quest {QuestGuid} - not found in active quests", questGuid);
        }
      }
    }

    // ==================== PRIVATE HELPERS ====================

    /// <summary>
    /// Restores a completed quest directly to completedUnclaimedQuests without going through StartQuestAsync.
    /// This prevents auto-completion from triggering during restoration.
    /// Graphs are loaded so that graph state can be restored, but the quest is immediately marked as completed.
    /// </summary>
    private async UniTask RestoreCompletedQuestDirectlyAsync(QuestDefinition quest, DateTime startedAt, CancellationToken ct)
    {
      if (quest == null)
      {
        log.ForMethod().Error("Quest: Cannot restore null quest");
        return;
      }

      var questGuid = quest.Guid;
      log.ForMethod().Information("Quest: Restoring completed quest '{QuestName}' (GUID: {QuestGuid}) directly to completed state", quest.DisplayName, questGuid);

      // Create runtime state with the saved StartedAt time
      var graphState = new InMemoryGraphState();
      graphState.Set("__quest_guid", questGuid);
      graphState.Set("__quest_definition", quest);

      var runtimeState = new QuestRuntimeState(
          questGuid,
          quest,
          graphState,
          startedAt
      );

      // Load objective graphs (but don't publish QuestStartedMessage)
      // We need to load graphs so that graph state can be restored
      log.ForMethod().Information("Quest: Loading {ObjectiveCount} objective graphs for restored completed quest '{QuestName}'", quest.Objectives.Count, quest.DisplayName);
      await LoadObjectiveGraphsAsync(quest, runtimeState, ct);

      // Optionally load quest-level graph if it exists
      if (quest.GraphAsset != null)
      {
        try
        {
          log.ForMethod().Debug("Quest: Loading quest-level graph for restored completed quest '{QuestName}'", quest.DisplayName);
          var runnerId = $"quest_{questGuid}";
          var runner = await LoadGraphAsync(quest.GraphAsset, runnerId, graphState, ct);
          loadedRunners[runnerId] = runner;
          eventRouter.RegisterRunner(runner);
          runtimeState.LoadedGraphInstanceIds.Add(runnerId);
          log.ForMethod().Information("Quest: Loaded quest graph for restored completed quest '{QuestName}' (runner: {RunnerId})", quest.DisplayName, runnerId);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Quest: Failed to load quest graph for restored completed quest '{QuestName}'", quest.DisplayName);
        }
      }

      // Add directly to completedUnclaimedQuests (skip active state entirely)
      // Note: We keep the graphs loaded so that graph state can be restored
      completedUnclaimedQuests[questGuid] = runtimeState;

      log.ForMethod().Information("Quest: Successfully restored completed quest '{QuestName}' ({QuestGuid}) directly to completed state", quest.DisplayName, questGuid);
    }

    private async UniTask LoadObjectiveGraphsAsync(QuestDefinition quest, QuestRuntimeState runtimeState, CancellationToken ct)
    {
      if (quest.Objectives == null || quest.Objectives.Count == 0)
      {
        log.ForMethod().Warning("Quest: Quest '{QuestName}' has no objectives", quest.DisplayName);
        return;
      }

      log.ForMethod().Debug("Quest: Loading {ObjectiveCount} objective graphs for quest '{QuestName}'", quest.Objectives.Count, quest.DisplayName);

      for (var i = 0; i < quest.Objectives.Count; i++)
      {
        var objective = quest.Objectives[i];

        if (objective == null)
        {
          log.ForMethod().Error("Quest: Null objective at index {Index} in quest '{QuestName}'", i, quest.DisplayName);
          continue;
        }

        if (objective.ObjectiveGraph == null)
        {
          log.ForMethod().Error("Quest: Objective '{ObjectiveName}' ({ObjectiveGuid}) has no graph assigned!",
            objective.DisplayName, objective.Guid);
          continue;
        }

        try
        {
          var runnerId = $"objective_{objective.Guid}";
          log.ForMethod().Debug("Quest: Loading objective graph '{ObjectiveName}' (runner: {RunnerId})", objective.DisplayName, runnerId);

          var runner = await LoadGraphAsync(objective.ObjectiveGraph, runnerId, runtimeState.GraphState, ct);
          loadedRunners[runnerId] = runner;
          eventRouter.RegisterRunner(runner);
          runtimeState.LoadedGraphInstanceIds.Add(runnerId);

          log.ForMethod().Information("Quest: Loaded objective graph '{ObjectiveName}' ({ObjectiveGuid}) for quest '{QuestName}'",
            objective.DisplayName, objective.Guid, quest.DisplayName);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Quest: Failed to load objective graph '{ObjectiveName}' ({ObjectiveGuid})",
            objective.DisplayName, objective.Guid);
        }
      }

      log.ForMethod().Information("Quest: Finished loading objective graphs for quest '{QuestName}'", quest.DisplayName);
    }

    private void UnloadQuestGraphs(QuestRuntimeState runtimeState)
    {
      foreach (var runnerId in runtimeState.LoadedGraphInstanceIds)
      {
        try
        {
          if (loadedRunners.Remove(runnerId, out var runner))
          {
            // TODO: Unregister from event router (not currently supported by IGraphEventRouter)
            Debug.Log($"[QuestManager] Unloaded graph runner {runnerId}");
          }
        }
        catch (Exception ex)
        {
          Debug.LogError($"[QuestManager] Failed to unload graph {runnerId}: {ex.Message}");
        }
      }

      runtimeState.LoadedGraphInstanceIds.Clear();
    }

    private async UniTask<IGraphRunner> LoadGraphAsync(
        QuestGraphAsset graphAsset,
        string runnerId,
        IGraphState graphState,
        CancellationToken ct)
    {
      await UniTask.WaitForEndOfFrame(ct);

      if (graphAsset == null)
      {
        log.ForMethod().Error("Quest: Cannot load graph - graphAsset is null (runnerId: {RunnerId})", runnerId);
        throw new ArgumentNullException(nameof(graphAsset), $"Quest graph asset is null for runner {runnerId}");
      }

      try
      {
        log.ForMethod().Information("Quest: Exporting graph asset '{GraphName}' (runnerId: {RunnerId})", graphAsset.name, runnerId);

        // Export the graph to runtime definition (QuestGraphAsset inherits from NodeGraph)
        var exporter = new XNodeGraphExporter(executorRegistry);
        var runtimeDef = exporter.Export(graphAsset);
        runtimeDef.GraphId = runnerId;

        log.ForMethod().Information("Quest: Creating GraphRunner for '{GraphName}' (runnerId: {RunnerId}, nodes: {NodeCount})",
          graphAsset.name, runnerId, runtimeDef.Nodes.Count);

        // Create and return the runner
        var runner = new GraphRunner(runtimeDef, graphState, executorRegistry, services, eventEmitter);

        log.ForMethod().Information("Quest: Successfully loaded graph '{GraphName}' (runnerId: {RunnerId})", graphAsset.name, runnerId);
        return runner;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Quest: Exception loading graph '{GraphName}' (runnerId: {RunnerId}): {Message}",
          graphAsset.name, runnerId, ex.Message);
        throw;
      }
    }

    private string SerializeGraphState(IGraphState graphState)
    {
      // Graph state is already saved separately via GraphStateSaveController
      // We just need to store a marker here - the actual state is in the graph_states save data
      // Return empty string as a placeholder (the graph state will be restored from GraphStateSnapshot)
      return string.Empty;
    }

    /// <summary>
    /// Waits for a QuestDefinition to be available, retrying if it's not found immediately.
    /// This is useful during save restoration when quest definitions might still be loading from addressables.
    /// Also tries to dynamically load from the registry if provided.
    /// </summary>
    private async UniTask<QuestDefinition> WaitForQuestDefinitionAsync(string questGuid, VisualGraphRegistry registry = null, CancellationToken ct = default)
    {
      if (string.IsNullOrEmpty(questGuid))
        return null;

      // First try to find it immediately
      var quest = FindQuestDefinitionByGuid(questGuid);
      if (quest != null)
      {
        return quest;
      }

      // If not found and we have a registry, try to load it dynamically from addressables
      if (registry != null && registry.QuestDefinitions != null)
      {
        log.ForMethod().Information("QuestDefinition not found immediately for GUID {QuestGuid} - attempting to load from registry", questGuid);
        quest = await TryLoadQuestFromRegistryAsync(questGuid, registry, ct);
        if (quest != null)
        {
          return quest;
        }
      }

      // If still not found, wait and retry (quest definitions might still be loading from addressables)
      log.ForMethod().Information("QuestDefinition not found immediately for GUID {QuestGuid} - waiting for it to be loaded", questGuid);

      var maxWaitTime = TimeSpan.FromSeconds(5);
      var waitStart = DateTime.UtcNow;
      var retryCount = 0;

      while (DateTime.UtcNow - waitStart < maxWaitTime)
      {
        await UniTask.Delay(100, cancellationToken: ct); // Wait 100ms before retrying

        quest = FindQuestDefinitionByGuid(questGuid);
        if (quest != null)
        {
          log.ForMethod().Information("QuestDefinition found for GUID {QuestGuid} after {RetryCount} retries", questGuid, retryCount);
          return quest;
        }

        // Try loading from registry again on each retry
        if (registry != null && registry.QuestDefinitions != null && retryCount % 5 == 0) // Try every 500ms
        {
          quest = await TryLoadQuestFromRegistryAsync(questGuid, registry, ct);
          if (quest != null)
          {
            log.ForMethod().Information("QuestDefinition loaded from registry for GUID {QuestGuid} after {RetryCount} retries", questGuid, retryCount);
            return quest;
          }
        }

        retryCount++;

        if (ct.IsCancellationRequested)
        {
          log.ForMethod().Warning("Cancelled waiting for QuestDefinition with GUID {QuestGuid}", questGuid);
          return null;
        }
      }

      log.ForMethod().Warning("QuestDefinition not found for GUID {QuestGuid} after waiting {WaitTime} seconds", questGuid, maxWaitTime.TotalSeconds);
      return null;
    }

    /// <summary>
    /// Finds a QuestDefinition by GUID.
    /// First checks the GraphDefinitionRegistry, then attempts to load from addressables if needed.
    /// Falls back to Resources for backward compatibility.
    /// </summary>
    private QuestDefinition FindQuestDefinitionByGuid(string questGuid)
    {
      if (string.IsNullOrEmpty(questGuid))
        return null;

      // First, check the runtime registry (may have been loaded from addressables or Resources)
      var quest = GraphDefinitionRegistry.GetQuestDefinition(questGuid);
      if (quest != null)
      {
        log.ForMethod().Debug("Found QuestDefinition '{QuestName}' for GUID {QuestGuid} in GraphDefinitionRegistry", quest.DisplayName, questGuid);
        return quest;
      }


      // Fallback to Resources for backward compatibility
      var allQuests = Resources.LoadAll<QuestDefinition>("");
      quest = allQuests.FirstOrDefault(q => q.Guid == questGuid);

      if (quest != null)
      {
        // Register it for future lookups
        GraphDefinitionRegistry.RegisterQuestDefinition(quest);
        log.ForMethod().Debug("Found QuestDefinition '{QuestName}' for GUID {QuestGuid} in Resources", quest.DisplayName, questGuid);
      }
      else
      {
        // Log available quest GUIDs for debugging
        var availableGuids = GraphDefinitionRegistry.GetAllQuestDefinitions().Select(q => q.Guid).ToList();
        var resourcesGuids = allQuests.Select(q => q.Guid).ToList();
        log.ForMethod().Warning("QuestDefinition with GUID {QuestGuid} not found. " +
          "Available in registry: {RegistryGuids}. Available in Resources: {ResourcesGuids}",
          questGuid, string.Join(", ", availableGuids), string.Join(", ", resourcesGuids));
      }

      return quest;
    }

    /// <summary>
    /// Attempts to load a quest definition from the registry by searching through all QuestAssetReferences
    /// and loading them from addressables to find the one matching the GUID.
    /// </summary>
    private async UniTask<QuestDefinition> TryLoadQuestFromRegistryAsync(string questGuid, VisualGraphRegistry registry, CancellationToken ct = default)
    {
      if (registry == null || registry.QuestDefinitions == null || registry.QuestDefinitions.Count == 0)
      {
        return null;
      }

      log.ForMethod().Information("Searching registry for quest definition with GUID {QuestGuid} ({Count} quest definitions in registry)",
        questGuid, registry.QuestDefinitions.Count);

      foreach (var assetRef in registry.QuestDefinitions)
      {
        if (assetRef == null || !assetRef.HasValidGuid)
        {
          continue;
        }

        try
        {
          // Load the quest definition from addressables
          var handle = Addressables.LoadAssetAsync<QuestDefinition>(assetRef);
          var questDef = await handle;

          if (questDef != null)
          {
            // Register it for future lookups
            GraphDefinitionRegistry.RegisterQuestDefinition(questDef);

            // Check if this is the quest we're looking for
            if (questDef.Guid == questGuid)
            {
              log.ForMethod().Information("Found and loaded quest definition '{QuestName}' (GUID: {QuestGuid}) from registry",
                questDef.DisplayName, questGuid);
              return questDef;
            }
          }
        }
        catch (Exception ex)
        {
          log.ForMethod().Warning(ex, "Error loading quest definition from addressable (GUID: {AssetGuid}): {Message}",
            assetRef.AssetGUID, ex.Message);
          // Continue searching other quests
        }

        if (ct.IsCancellationRequested)
        {
          break;
        }
      }

      log.ForMethod().Warning("Quest definition with GUID {QuestGuid} not found in registry after searching {Count} quest definitions",
        questGuid, registry.QuestDefinitions.Count);
      return null;
    }

    // ==================== PROGRESS TRACKING ====================

    private void OnQuestStarted(QuestStartedMessage message)
    {
      log.ForMethod().Information("Quest: OnQuestStarted handler called for quest {QuestName} ({QuestGuid})", message.Quest.DisplayName, message.QuestGuid);
    }
    private void OnQuestCompleted(QuestCompletedMessage message)
    {
      log.ForMethod().Information("Quest: OnQuestCompleted handler called for quest {QuestName} ({QuestGuid})", message.Quest.DisplayName, message.QuestGuid);
    }
    private void OnQuestAbandoned(QuestAbandonedMessage message)
    {
      log.ForMethod().Information("Quest: OnQuestAbandoned handler called for quest {QuestName} ({QuestGuid})", message.Quest.DisplayName, message.QuestGuid);
    }
    private void OnQuestClaimed(QuestClaimedMessage message)
    {
      log.ForMethod().Information("Quest: OnQuestClaimed handler called for quest {QuestName} ({QuestGuid})", message.Quest.DisplayName, message.QuestGuid);
    }

    private void OnQuestObjectiveProgress(QuestObjectiveProgressMessage message)
    {
      log.ForMethod().Information("Quest: OnQuestObjectiveProgress handler called for quest {QuestGuid}, objective {ObjectiveGuid}",
        message.QuestGuid, message.ObjectiveGuid);

      // Get quest and objective names from the objective reference
      var questName = activeQuests.TryGetValue(message.QuestGuid, out var questState)
        ? questState.Definition.DisplayName
        : "Unknown Quest";
      var objectiveName = message.Objective?.DisplayName ?? "Unknown Objective";

      log.ForMethod().Information(
        "Quest Progress: Quest '{QuestName}' ({QuestGuid}) - Objective '{ObjectiveName}' ({ObjectiveGuid}): {Current}/{Required} ({Percentage:P0})",
        questName,
        message.QuestGuid,
        objectiveName,
        message.ObjectiveGuid,
        message.Current,
        message.Required,
        message.Percentage);

      // Update runtime state if quest is active
      if (activeQuests.TryGetValue(message.QuestGuid, out questState))
      {
        // Store progress in graph state for querying
        var progressKey = $"__objective_{message.ObjectiveGuid}_progress";
        questState.GraphState.Set(progressKey, message.Current);
        log.ForMethod().Information("Quest: Updated progress in graph state for objective {ObjectiveGuid}", message.ObjectiveGuid);
      }
    }
  }
}

