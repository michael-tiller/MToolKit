#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Bootstrap;
using MToolKit.Runtime.VisualGraphs.Definitions;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Dialogue.Definitions;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace MToolKit.Runtime.VisualGraphs.Runtime.Loading
{
  /// <summary>
  ///   Loads graphs dynamically, supporting both direct references and Addressables.
  /// </summary>
  public sealed class GraphLoader : IGraphLoader
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GraphLoader>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly VisualGraphRegistry registry;
    private readonly GraphEventRouter router;
    private readonly NodeExecutorRegistry executorRegistry;
    private readonly IServiceProvider services;
    private readonly IEventEmitter eventEmitter;

    private readonly Dictionary<string, IGraphRunner> loadedRunners = new();
    private readonly Dictionary<string, object> loadedHandles = new(); // Addressables handles

    public GraphLoader(
      VisualGraphRegistry registry,
      GraphEventRouter router,
      NodeExecutorRegistry executorRegistry,
      IServiceProvider services,
      IEventEmitter eventEmitter)
    {
      this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
      this.router = router ?? throw new ArgumentNullException(nameof(router));
      this.executorRegistry = executorRegistry ?? throw new ArgumentNullException(nameof(executorRegistry));
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
    }

    public async UniTask<IGraphRunner?> LoadGraphAsync(string graphId, CancellationToken ct = default)
    {
      if (string.IsNullOrEmpty(graphId))
        throw new ArgumentException("Graph ID cannot be null or empty", nameof(graphId));

      // Return if already loaded
      if (loadedRunners.TryGetValue(graphId, out var existingRunner))
      {
        log.Debug("Graph '{GraphId}' already loaded, returning existing runner", graphId);
        return existingRunner;
      }

      // Try self-registered quest definitions first (preferred)
      var questDef = Runtime.GraphDefinitionRegistry.GetQuestDefinition(graphId);
      if (questDef != null)
      {
        var runner = await LoadQuestGraphAsync(questDef, ct);
        if (runner != null)
        {
          loadedRunners[graphId] = runner;
          router.RegisterRunner(runner);
          return runner;
        }
        // Quest has no graph asset (optional) - this is valid, but can't load a runner
        // Return null runner - caller should handle this gracefully
        log.Warning("Quest '{QuestId}' has no quest-level GraphAsset (optional). Returning null runner.",
          graphId);
        return null;
      }

      // Try self-registered dialogue definitions
      var dialogueDef = Runtime.GraphDefinitionRegistry.GetDialogueDefinition(graphId);
      if (dialogueDef != null)
      {
        var runner = await LoadDialogueGraphAsync(dialogueDef, ct);
        loadedRunners[graphId] = runner;
        router.RegisterRunner(runner);
        return runner;
      }

      // Fallback to registry asset (backward compatibility)
      questDef = registry.QuestDefinitions?.Find(q => q.Guid == graphId);
      if (questDef != null)
      {
        var runner = await LoadQuestGraphAsync(questDef, ct);
        if (runner != null)
        {
          loadedRunners[graphId] = runner;
          router.RegisterRunner(runner);
          return runner;
        }
        // Quest has no graph asset (optional) - this is valid, but can't load a runner
        // Return null runner - caller should handle this gracefully
        log.Warning("Quest '{QuestId}' has no quest-level GraphAsset (optional). Returning null runner.",
          graphId);
        return null;
      }

      dialogueDef = registry.DialogueDefinitions?.Find(d => d.DialogueId == graphId);
      if (dialogueDef != null)
      {
        var runner = await LoadDialogueGraphAsync(dialogueDef, ct);
        loadedRunners[graphId] = runner;
        router.RegisterRunner(runner);
        return runner;
      }

      throw new InvalidOperationException($"Graph '{graphId}' not found in registry or self-registered definitions");
    }

    public void UnloadGraph(string graphId)
    {
      if (!loadedRunners.Remove(graphId, out var runner))
      {
        log.Warning("Attempted to unload graph '{GraphId}' that is not loaded", graphId);
        return;
      }

      // Unload addressables handle if exists
#if UNITY_ADDRESSABLES
      if (loadedHandles.Remove(graphId, out var handle))
      {
        if (handle is AsyncOperationHandle opHandle)
        {
          Addressables.Release(opHandle);
          log.Debug("Released Addressables handle for graph '{GraphId}'", graphId);
        }
      }
#endif

      log.Information("Unloaded graph '{GraphId}'", graphId);
    }

    public bool IsLoaded(string graphId)
    {
      return loadedRunners.ContainsKey(graphId);
    }

    public IGraphRunner? GetRunner(string graphId)
    {
      return loadedRunners.TryGetValue(graphId, out var runner) ? runner : null;
    }

    private async UniTask<IGraphRunner?> LoadQuestGraphAsync(QuestDefinition questDef, CancellationToken ct)
    {
      await UniTask.WaitForEndOfFrame(ct);
      var graphAsset = questDef.GraphAsset;

#if UNITY_ADDRESSABLES
      // Load via Addressables if key is specified
      if (!string.IsNullOrEmpty(questDef.AddressableKey))
      {
        log.Debug("Loading quest graph '{QuestId}' via Addressables key '{Key}'",
          questDef.Guid, questDef.AddressableKey);

        var handle = Addressables.LoadAssetAsync<Authoring.Graphs.QuestGraphAsset>(questDef.AddressableKey);
        graphAsset = await handle.ToUniTask(cancellationToken: ct);
        loadedHandles[questDef.Guid] = handle;

        if (graphAsset == null)
        {
          log.Warning("Failed to load quest graph '{QuestId}' from Addressables key '{Key}' - quest-level graphs are optional, skipping",
            questDef.Guid, questDef.AddressableKey);
          return null;
        }
      }
#endif

      // Quest-level graphs are OPTIONAL - return null if not assigned
      if (graphAsset == null)
      {
        log.Debug("Quest '{QuestId}' has no quest-level GraphAsset (optional) - skipping graph load",
          questDef.Guid);
        return null;
      }

      // Export and initialize
      var exporter = new XNodeGraphExporter(executorRegistry);
      var runtimeDef = exporter.Export(graphAsset);
      runtimeDef.GraphId = questDef.Guid;

      var baseState = new InMemoryGraphState();
      var state = new DebuggableGraphState(baseState, runtimeDef.GraphId);

      // Apply variables
      if (registry.GlobalVariables != null)
      {
        var globalVars = registry.GlobalVariables.GetFor(questDef.Guid);
        globalVars?.ApplyTo(state);
      }
      questDef.InitialVariables?.ApplyTo(state);

      var runner = new GraphRunner(runtimeDef, state, executorRegistry, services, eventEmitter);
      log.Information("Loaded quest graph '{QuestId}': {NodeCount} nodes, {SubscriptionCount} subscriptions",
        questDef.Guid, runtimeDef.Nodes.Count, runtimeDef.Subscriptions.Count);

      return runner;
    }

    private async UniTask<IGraphRunner> LoadDialogueGraphAsync(DialogueDefinition dialogueDef, CancellationToken ct)
    {
      await UniTask.WaitForEndOfFrame(ct);
      var graphAsset = dialogueDef.GraphAsset;

#if UNITY_ADDRESSABLES
      // Load via Addressables if key is specified
      if (!string.IsNullOrEmpty(dialogueDef.AddressableKey))
      {
        log.Debug("Loading dialogue graph '{DialogueId}' via Addressables key '{Key}'",
          dialogueDef.DialogueId, dialogueDef.AddressableKey);

        var handle = Addressables.LoadAssetAsync<Authoring.Graphs.DialogueGraphAsset>(dialogueDef.AddressableKey);
        graphAsset = await handle.ToUniTask(cancellationToken: ct);
        loadedHandles[dialogueDef.DialogueId] = handle;

        if (graphAsset == null)
          throw new InvalidOperationException($"Failed to load dialogue graph '{dialogueDef.DialogueId}' from Addressables key '{dialogueDef.AddressableKey}'");
      }
#endif

      if (graphAsset == null)
        throw new InvalidOperationException($"Dialogue graph '{dialogueDef.DialogueId}' has no GraphAsset assigned");

      // Export and initialize
      var exporter = new XNodeGraphExporter(executorRegistry);
      var runtimeDef = exporter.Export(graphAsset);
      runtimeDef.GraphId = dialogueDef.DialogueId;

      var baseState = new InMemoryGraphState();
      var state = new DebuggableGraphState(baseState, runtimeDef.GraphId);

      // Apply variables
      if (registry.GlobalVariables != null)
      {
        var globalVars = registry.GlobalVariables.GetFor(dialogueDef.DialogueId);
        globalVars?.ApplyTo(state);
      }
      dialogueDef.InitialVariables?.ApplyTo(state);

      var runner = new GraphRunner(runtimeDef, state, executorRegistry, services, eventEmitter);
      log.Information("Loaded dialogue graph '{DialogueId}': {NodeCount} nodes, {SubscriptionCount} subscriptions",
        dialogueDef.DialogueId, runtimeDef.Nodes.Count, runtimeDef.Subscriptions.Count);

      return runner;
    }
  }
}

