#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ES3Internal;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.VisualGraphs.Config;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Persistence
{
  /// <summary>
  ///   Save/load controller for graph state persistence.
  ///   Integrates with the save system to automatically save and restore all graph states and quest manager state.
  /// </summary>
  public sealed class GraphStateSaveController : ISaveDomainController
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GraphStateSaveController>().ForFeature("VisualGraphs.Persistence"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IGraphEventRouter router;
    private readonly IES3Service es3Service;
    private readonly IQuestManager? questManager;
    private readonly VisualGraphRegistry? registry;
    private readonly string domainPrefix;
    private const string GraphStatesSaveKey = "graph_states";
    private const string QuestManagerSaveKey = "quest_manager_state";

    public GraphStateSaveController(IGraphEventRouter router, IES3Service es3Service, IQuestManager? questManager = null, VisualGraphRegistry? registry = null)
    {
      this.router = router ?? throw new ArgumentNullException(nameof(router));
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));
      this.questManager = questManager; // Optional - QuestManager may not be available
      this.registry = registry; // Optional - Registry for dynamic quest loading
      domainPrefix = $"{Domain.ToString().ToLower()}_"; // e.g., "graphs_"
    }

    public ESaveDomain Domain => ESaveDomain.Graphs;

    public async UniTask SaveAsync(CancellationToken ct = default)
    {
      log.ForMethod().Information("Saving graph states");

      if (ct.IsCancellationRequested)
      {
        log.ForMethod().Debug("Save cancelled before starting");
        return;
      }

      try
      {
        // Capture all graph states
        var stateMap = new Dictionary<string, GraphStateSnapshot>();
        var runners = router.GetRunners().ToList();

        log.ForMethod().Debug("Capturing state from {GraphCount} graph(s)", runners.Count);

        foreach (var runner in runners)
        {
          try
          {
            var snapshot = runner.ExportState();
            if (snapshot != null)
            {
              stateMap[runner.GraphId] = snapshot;
              log.ForMethod().Debug("Captured state for graph '{GraphId}' ({StateKeyCount} keys)",
                runner.GraphId, snapshot.Data?.Count ?? 0);
            }
            else
            {
              log.ForMethod().Warning("Graph '{GraphId}' returned null snapshot", runner.GraphId);
            }
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Failed to export state for graph '{GraphId}': {Message}",
              runner.GraphId, ex.Message);
            // Continue with other graphs even if one fails
          }

          if (ct.IsCancellationRequested)
          {
            log.ForMethod().Debug("Save cancelled during state capture");
            return;
          }
        }

        // Save graph states to ES3 with domain prefix
        var graphStatesKey = $"{domainPrefix}{GraphStatesSaveKey}";
        await es3Service.SaveAsync(graphStatesKey, stateMap, ct);

        // Save QuestManager state if available with domain prefix
        if (questManager != null)
        {
          try
          {
            var questSaveData = questManager.GetSaveData();
            var questManagerKey = $"{domainPrefix}{QuestManagerSaveKey}";
            await es3Service.SaveAsync(questManagerKey, questSaveData, ct);
            log.ForMethod().Information("Saved QuestManager state: {ActiveCount} active, {CompletedCount} completed, {ClaimedCount} claimed",
              questSaveData.ActiveQuests.Count, questSaveData.CompletedUnclaimedQuests.Count, questSaveData.ClaimedQuestGuids.Count);
          }
          catch (Exception ex)
          {
            log.ForMethod().Warning(ex, "Failed to save QuestManager state: {Message}", ex.Message);
            // Don't fail the entire save if quest manager save fails
          }
        }

        log.ForMethod().Information("Successfully saved {GraphCount} graph state(s)", stateMap.Count);
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Debug("Save operation cancelled");
        throw;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to save graph states: {Message}", ex.Message);
        throw;
      }
    }

    public async UniTask LoadAsync(CancellationToken ct = default)
    {
      log.ForMethod().Information("Loading graph states");

      if (ct.IsCancellationRequested)
      {
        log.ForMethod().Debug("Load cancelled before starting");
        return;
      }

      try
      {
        // Check if save data exists (with domain prefix)
        var graphStatesKey = $"{domainPrefix}{GraphStatesSaveKey}";
        var questManagerKey = $"{domainPrefix}{QuestManagerSaveKey}";
        var hasGraphStates = es3Service.KeyExists(graphStatesKey);
        var hasQuestManagerState = es3Service.KeyExists(questManagerKey);

        if (!hasGraphStates && !hasQuestManagerState)
        {
          log.ForMethod().Information("No saved graph or quest state data found - starting fresh");
          return;
        }

        // Load graph states from ES3 (with domain prefix)
        Dictionary<string, GraphStateSnapshot> stateMap;
        if (hasGraphStates)
        {
          try
          {
            // Ensure ES3Type for GraphStateSnapshot is available
            // ES3 will auto-discover ES3Type_GraphStateSnapshot, but we ensure it's loaded
            var graphStateSnapshotType = ES3TypeMgr.GetOrCreateES3Type(typeof(GraphStateSnapshot));
            var dictionaryType = ES3TypeMgr.GetOrCreateES3Type(typeof(Dictionary<string, GraphStateSnapshot>));

            log.ForMethod().Debug("Loading graph states with ES3Type support (GraphStateSnapshot type: {Type}, Dictionary type: {DictType})",
              graphStateSnapshotType?.GetType().Name ?? "null", dictionaryType?.GetType().Name ?? "null");

            // Try loading with explicit type handling
            stateMap = await es3Service.LoadAsync<Dictionary<string, GraphStateSnapshot>>(
              graphStatesKey,
              new Dictionary<string, GraphStateSnapshot>(),
              ct);

            if (stateMap == null)
            {
              log.ForMethod().Warning("Loaded null graph states dictionary - using empty dictionary");
              stateMap = new Dictionary<string, GraphStateSnapshot>();
            }
            else
            {
              log.ForMethod().Information("Successfully loaded {Count} graph states", stateMap.Count);
            }
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Failed to load graph states from save data. Error: {Message}", ex.Message);
            stateMap = new Dictionary<string, GraphStateSnapshot>();
          }
        }
        else
        {
          stateMap = new Dictionary<string, GraphStateSnapshot>();
          log.ForMethod().Information("No saved graph state data found - starting fresh");
        }

        // Load QuestManager state FIRST (before graph states) so quest graphs are loaded
        // This ensures graph state restoration can find the quest graph runners
        QuestManagerSaveData? questSaveData = null;
        if (questManager != null && hasQuestManagerState)
        {
          try
          {
            questSaveData = await es3Service.LoadAsync<QuestManagerSaveData>(
              questManagerKey,
              new QuestManagerSaveData(),
              ct);

            if (questSaveData != null)
            {
              log.ForMethod().Information("Loaded QuestManager save data: {ActiveCount} active, {CompletedCount} completed, {ClaimedCount} claimed",
                questSaveData.ActiveQuests.Count, questSaveData.CompletedUnclaimedQuests.Count, questSaveData.ClaimedQuestGuids.Count);

              if (questSaveData.CompletedUnclaimedQuests.Count > 0)
              {
                log.ForMethod().Information("Completed quest GUIDs in save data: {Guids}",
                  string.Join(", ", questSaveData.CompletedUnclaimedQuests.Select(q => q.QuestGuid)));
              }

              await questManager.RestoreSaveDataAsync(questSaveData, registry, ct);
              log.ForMethod().Information("Restored QuestManager state: {ActiveCount} active, {CompletedCount} completed, {ClaimedCount} claimed",
                questSaveData.ActiveQuests.Count, questSaveData.CompletedUnclaimedQuests.Count, questSaveData.ClaimedQuestGuids.Count);

              // Note: Claimed quests don't have graph state (they're just GUIDs), so we only wait for
              // runners from active and completed-unclaimed quests

              // Wait for quest graph runners to be registered
              // Only active and completed-unclaimed quests should have runners (claimed quests are done)
              var totalExpectedQuests = questSaveData.ActiveQuests.Count + questSaveData.CompletedUnclaimedQuests.Count;

              if (totalExpectedQuests > 0)
              {
                log.ForMethod().Information("Waiting for {ExpectedCount} quest runner(s) to be registered (active: {ActiveCount}, completed: {CompletedCount}, claimed: {ClaimedCount})",
                  totalExpectedQuests, questSaveData.ActiveQuests.Count, questSaveData.CompletedUnclaimedQuests.Count, questSaveData.ClaimedQuestGuids.Count);

                // Wait up to 2 seconds for runners to be registered (they should be registered immediately after StartQuestAsync completes)
                var maxWaitTime = TimeSpan.FromSeconds(2);
                var waitStart = DateTime.UtcNow;
                while (DateTime.UtcNow - waitStart < maxWaitTime)
                {
                  var currentRunners = router.GetRunners().ToList();

                  // Check if we have the expected number of runners
                  if (currentRunners.Count >= totalExpectedQuests)
                  {
                    log.ForMethod().Information("Quest runners registered: {RunnerCount} runner(s) found (expected: {ExpectedCount})",
                      currentRunners.Count, totalExpectedQuests);
                    break;
                  }

                  await UniTask.Delay(50, cancellationToken: ct); // Wait 50ms before checking again
                }
              }

              // Final check - log what we have
              var finalRunners = router.GetRunners().ToList();
              log.ForMethod().Information("After quest restoration: {RunnerCount} runner(s) registered, {ActiveCount} active quests, {CompletedCount} completed quests, {ClaimedCount} claimed quests. Runner IDs: {RunnerIds}",
                finalRunners.Count, questSaveData.ActiveQuests.Count, questSaveData.CompletedUnclaimedQuests.Count, questSaveData.ClaimedQuestGuids.Count,
                string.Join(", ", finalRunners.Select(r => r.GraphId)));
            }
          }
          catch (Exception ex)
          {
            log.ForMethod().Warning(ex, "Failed to load QuestManager state: {Message}", ex.Message);
            // Don't fail the entire load if quest manager load fails
          }
        }

        // Now restore graph states (after quests are restored so quest graph runners exist)
        if (stateMap == null || stateMap.Count == 0)
        {
          log.ForMethod().Information("Loaded empty graph state data - starting fresh");
          return;
        }

        log.ForMethod().Debug("Loaded {GraphCount} graph state(s) from save data", stateMap.Count);

        // Restore states to runners (re-fetch to include any quest graphs that were just loaded)
        var runners = router.GetRunners().ToList();
        log.ForMethod().Information("Available runners for state restoration: {RunnerIds}",
          string.Join(", ", runners.Select(r => r.GraphId)));
        log.ForMethod().Information("Graph IDs in save data: {GraphIds}",
          string.Join(", ", stateMap.Keys));

        var restoredCount = 0;
        var missingCount = 0;

        foreach (var kv in stateMap)
        {
          var graphId = kv.Key;
          var snapshot = kv.Value;

          if (ct.IsCancellationRequested)
          {
            log.ForMethod().Debug("Load cancelled during state restoration");
            return;
          }

          try
          {
            var runner = runners.FirstOrDefault(r => r.GraphId == graphId);
            if (runner != null)
            {
              runner.ImportState(snapshot);
              restoredCount++;
              log.ForMethod().Information("Restored state for graph '{GraphId}' ({StateKeyCount} keys): {Keys}",
                graphId, snapshot.Data?.Count ?? 0, string.Join(", ", snapshot.Data?.Keys ?? Enumerable.Empty<string>()));
            }
            else
            {
              missingCount++;
              log.ForMethod().Warning(
                "Save data contains state for graph '{GraphId}' but no runner found. Available runners: {AvailableRunners}",
                graphId, string.Join(", ", runners.Select(r => r.GraphId)));
            }
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Failed to import state for graph '{GraphId}': {Message}",
              graphId, ex.Message);
            // Continue with other graphs even if one fails
          }
        }

        log.ForMethod().Information(
          "Graph state load completed: {RestoredCount} restored, {MissingCount} missing runners, {TotalInSave} total in save data",
          restoredCount, missingCount, stateMap.Count);

        // Note: Completed quests are already restored directly to completedUnclaimedQuests
        // (they were never in activeQuests), so we don't need to call FinalizeCompletedQuestRestoration.
        // The graphs remain loaded so that graph state can be restored, which has already happened above.
        if (questManager != null && questSaveData != null && questSaveData.CompletedUnclaimedQuests.Count > 0)
        {
          var completedGuids = questSaveData.CompletedUnclaimedQuests.Select(q => q.QuestGuid).ToList();
          log.ForMethod().Information("Completed quests ({Count}) were restored directly to completed state - no finalization needed", completedGuids.Count);
        }
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Debug("Load operation cancelled");
        throw;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load graph states: {Message}", ex.Message);
        throw;
      }
    }
  }
}

