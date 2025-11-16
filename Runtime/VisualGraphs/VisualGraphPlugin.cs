using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Bootstrap;
using MToolKit.Runtime.VisualGraphs.Config;
using MToolKit.Runtime.VisualGraphs.Executors;
using MToolKit.Runtime.VisualGraphs.Quest.Executors;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Loading;
using MToolKit.Runtime.VisualGraphs.Dialogue.Executors;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.ES3Integration;
using System.Linq;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
using MToolKit.Runtime.VisualGraphs.Quest;

namespace MToolKit.Runtime.VisualGraphs
{
  /// <summary>
  ///   Plugin that manages the Visual Graphs system lifecycle.
  ///   Handles initialization, graph loading, and event routing.
  /// </summary>
  public sealed class VisualGraphPlugin : DomainPlugin<GraphEventRouter, IGraphEventRouter>
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<VisualGraphPlugin>().ForFeature("VisualGraphs"));
    private new static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private VisualGraphConfig config;
    private IGraphLoader graphLoader;
    [SerializeField]
    [Required]
    private EventBusBridge eventBusBridge;
    private CancellationTokenSource cts;
    private GraphEventRouter graphEventRouter;
    private IQuestManager questManager;
    private IDisposable questStartedSubscription;

    [ShowInInspector]
    [ReadOnly]
    public QuestManager QuestManager => questManager as QuestManager;

    /// <summary>
    ///   Dependencies required for the Visual Graphs plugin.
    /// </summary>
    public override IEnumerable<Type> RequiredServices => new List<Type> { };

    /// <summary>
    ///   Optional services for enhanced functionality.
    /// </summary>
    public override IEnumerable<Type> OptionalServices => new List<Type> { };

    /// <summary>
    ///   Override Register to add all Visual Graphs services to the container.
    /// </summary>
    public override void Register(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).Debug("Registering VisualGraphPlugin services");

      // Register configuration
      if (config != null)
      {
        builder.RegisterInstance(config);

        // Register the registry from config
        if (config.DefaultRegistry != null)
        {
          builder.RegisterInstance(config.DefaultRegistry);
        }
        else
        {
          log.ForGameObject(gameObject).Error("VisualGraphConfig.DefaultRegistry is null - GraphLoader will fail to resolve!");
        }
      }
      else
      {
        log.ForGameObject(gameObject).Error("VisualGraphConfig is null - GraphLoader will fail to resolve!");
      }

      // Core services - register as both concrete and interface
      builder.Register<GraphEventRouter>(Lifetime.Singleton)
        .As<IGraphEventRouter>()
        .AsSelf();

      builder.Register<NodeExecutorRegistry>(Lifetime.Singleton);
      builder.Register<IGraphLoader, GraphLoader>(Lifetime.Singleton);

      // Quest Manager (must be registered before VisualGraphEventEmitter which depends on it)
      builder.Register<MToolKit.Runtime.VisualGraphs.Quest.QuestManager>(Lifetime.Singleton)
        .As<MToolKit.Runtime.VisualGraphs.Quest.IQuestManager>();

      // Event emitter adapter (registered first, QuestManager will be injected later)
      builder.Register<IEventEmitter, VisualGraphEventEmitter>(Lifetime.Singleton);

      // Save/load controller for graph state persistence
      // Note: Requires GraphEventRouter (registered above), IES3Service (from GlobalInstaller), and IQuestManager (registered above)
      // Registry is already registered above, so we can inject it directly
      if (config != null && config.DefaultRegistry != null)
      {
        builder.Register<Persistence.GraphStateSaveController>(Lifetime.Singleton)
          .WithParameter("registry", config.DefaultRegistry);
      }
      else
      {
        builder.Register<Persistence.GraphStateSaveController>(Lifetime.Singleton);
      }

      // Register EventBusBridge - try serialized field first, then find on GameObject
      if (eventBusBridge == null)
      {
        eventBusBridge = gameObject.GetComponent<Bootstrap.EventBusBridge>();
        if (eventBusBridge == null)
        {
          log.ForGameObject(gameObject).Warning("EventBusBridge not found on GameObject - graphs may not receive MessagePipe events");
        }
      }

      if (eventBusBridge != null)
      {
        builder.RegisterInstance(eventBusBridge);
        log.ForGameObject(gameObject).Debug("EventBusBridge registered in DI container");
      }

      // Node executors - register all IGraphNodeExecutor implementations

      // Quest executors
      builder.Register<QuestSetStageNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<QuestObjectiveIncrementNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<QuestObjectiveSetNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<QuestObjectiveCheckNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<QuestStateCheckNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<QuestAllObjectivesCompleteNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<StartQuestNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<RequestStartQuestNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<RequestStartCampaignNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<RequestClaimQuestNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();

      // Message executors (data flow from messages)
      builder.Register<MessageFieldCheckNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<MessageFieldGetNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<MessageTypeCheckNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();

      // State executors (generic state management)
      builder.Register<GenericStateSetNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<GenericStateCheckNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<GenericStateGetNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();

      // Dialogue executors
      builder.Register<DialogueLineNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<DialogueChoiceNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();

      // Build callback to register all executors in the registry
      builder.RegisterBuildCallback(container =>
      {
        var registry = container.Resolve<NodeExecutorRegistry>();
        var executors = container.Resolve<IEnumerable<IGraphNodeExecutor>>();

        foreach (var executor in executors)
        {
          registry.Register(executor);
        }
      });

      // Register the plugin instance
      builder.RegisterInstance(this).AsSelf();

      log.ForGameObject(gameObject).Debug("VisualGraphPlugin registration completed");
    }

    protected override GraphEventRouter CreateService(IObjectResolver resolver)
    {
      log.ForGameObject(gameObject).Debug("Creating GraphEventRouter service");
      return new GraphEventRouter();
    }

    public override void PerformSetup(IObjectResolver resolver)
    {
      base.PerformSetup(resolver);

      cts = new CancellationTokenSource();

      // Resolve config (should be injected via field injection or constructor)
      if (config == null)
      {
        log.ForGameObject(gameObject).Warning("VisualGraphConfig not assigned - using defaults");
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Information(
          "Visual Graphs configured: VerboseLogging={VerboseLogging}, LoadAllOnStartup={LoadAllOnStartup}",
          config.EnableVerboseLogging, config.LoadAllOnStartup);
      }

      log.ForGameObject(gameObject).Debug("Visual Graphs setup phase completed");
    }

    public override bool AreDependenciesReady(IObjectResolver resolver)
    {
      bool canResolveRouter = resolver.TryResolve(out IGraphEventRouter _);
      bool canResolveLoader = resolver.TryResolve(out IGraphLoader _);
      bool canResolveEmitter = resolver.TryResolve(out IEventEmitter _);

      bool ready = canResolveRouter && canResolveLoader && canResolveEmitter;

      log.ForGameObject(gameObject).Information(
        "Dependency check: Router={Router}, Loader={Loader}, Emitter={Emitter} => Ready={Ready}",
        canResolveRouter, canResolveLoader, canResolveEmitter, ready);

      return ready;
    }

    public override void PerformRuntimeInitialization(IObjectResolver resolver)
    {
      // Base class handles the isRuntimeInitialized guard
      base.PerformRuntimeInitialization(resolver);

      try
      {
        // Resolve dependencies
        graphLoader = resolver.Resolve<IGraphLoader>();
        graphEventRouter = resolver.Resolve<IGraphEventRouter>() as Runtime.GraphEventRouter;
        questManager = resolver.Resolve<Quest.IQuestManager>();

        // Inject QuestManager into VisualGraphEventEmitter (break circular dependency)
        var eventEmitter = resolver.Resolve<IEventEmitter>() as VisualGraphEventEmitter;
        if (eventEmitter != null)
        {
          eventEmitter.SetQuestManager(questManager);
        }

        // Resolve EventBusBridge from DI (should be registered if found on GameObject)
        if (!resolver.TryResolve(out eventBusBridge))
        {
          // Try to find it on the GameObject as fallback
          eventBusBridge = gameObject.GetComponent<Bootstrap.EventBusBridge>();
          if (eventBusBridge == null)
          {
            log.ForGameObject(gameObject).Warning("EventBusBridge not found in DI or on GameObject - auto-creating");
            eventBusBridge = gameObject.AddComponent<Bootstrap.EventBusBridge>();
          }
        }

        // Always ensure router is injected into the bridge
        if (eventBusBridge != null && graphEventRouter != null)
        {
          eventBusBridge.Construct(graphEventRouter);
        }

        // Subscribe to QuestStartedMessage to re-subscribe EventBusBridge when quests start
        // This ensures objective graph subscriptions are registered even for manually started quests
        try
        {
          var questStartedSubscriber = GameMessageBroker.GetSubscriber<Quest.Messages.QuestStartedMessage>();
          if (questStartedSubscriber != null)
          {
            var handler = new QuestStartedMessageHandler(OnQuestStarted);
            questStartedSubscription = questStartedSubscriber.Subscribe(handler);
            log.ForGameObject(gameObject).Debug("Subscribed to QuestStartedMessage for EventBusBridge re-subscription");
          }
          else
          {
            log.ForGameObject(gameObject).Warning("QuestStartedMessage subscriber not available - EventBusBridge may not re-subscribe when quests start manually");
          }
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).Warning(ex, "Failed to subscribe to QuestStartedMessage - EventBusBridge may not re-subscribe when quests start manually");
        }

        log.ForGameObject(gameObject).Information("Visual Graphs plugin runtime initialization started");

        // Auto-initialize graphs if configured
        if (config != null && config.AutoInitializeFromRegistry)
        {
          if (config.DefaultRegistry == null)
          {
            log.ForGameObject(gameObject).Error(
              "AutoInitializeFromRegistry is true but DefaultRegistry is null!");
          }
          else if (config.LoadAllOnStartup)
          {
            InitializeGraphsAsync(config.DefaultRegistry).Forget();
          }
          else
          {
            log.ForGameObject(gameObject).Information(
              "Visual graph system in lazy-load mode - call LoadGraphAsync() to load graphs on demand");
          }
        }

        // Register save/load controller with SaveSystemCoordinator FIRST (before auto-start)
        // This ensures the save controller is registered before any save/load operations
        // Note: If save system loaded before plugin initialization, the controller won't have been
        // included in the load, so we need to manually trigger a load after registration
        var saveControllerRegistered = RegisterSaveController(resolver);

        // If save system already loaded before we registered, manually trigger a load
        // This ensures quest data is restored even if the save system loaded before plugin initialization
        if (saveControllerRegistered && questManager != null)
        {
          try
          {
            if (resolver.TryResolve<SaveSystemCoordinator>(out var saveCoordinator))
            {
              // Check if save system has already loaded (by checking if there's a last load time)
              // If so, manually trigger a load of our controller to restore quest data
              var saveController = resolver.Resolve<Persistence.GraphStateSaveController>();
              log.ForGameObject(gameObject).Information("Save controller registered - checking if manual load is needed");

              // Manually trigger load to ensure quest data is restored if save system loaded before registration
              LoadQuestDataIfNeededAsync(saveController, saveCoordinator).Forget();
            }
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).Warning(ex, "Failed to manually load quest data after registration: {Message}", ex.Message);
          }
        }

        // Auto-start quest if configured (after save controller is registered)
        // This allows the save system to restore quests before auto-start runs
        log.ForGameObject(gameObject).Debug("Quest: Checking auto-start conditions - config: {Config}, AutoStartFirstQuest: {AutoStart}, QuestDatabase: {Database}",
          config != null ? "Present" : "NULL",
          config?.AutoStartFirstQuest ?? false,
          config?.QuestDatabase != null ? "Present" : "NULL");

        if (config != null && config.AutoStartFirstQuest && config.QuestDatabase != null)
        {
          log.ForGameObject(gameObject).Information("Quest: Auto-start conditions met, invoking AutoStartQuestAsync");
          AutoStartQuestAsync(resolver).Forget();
        }
        else
        {
          log.ForGameObject(gameObject).Debug("Quest: Auto-start conditions NOT met, skipping quest auto-start");
        }

        log.ForGameObject(gameObject).Information("Visual Graphs plugin runtime initialization completed");
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).Error(ex, "Error during Visual Graphs runtime initialization");
      }
    }

    private bool RegisterSaveController(IObjectResolver resolver)
    {
      try
      {
        // Try to resolve SaveSystemCoordinator
        if (!resolver.TryResolve<SaveSystemCoordinator>(out var saveCoordinator))
        {
          log.ForGameObject(gameObject).Debug("SaveSystemCoordinator not found in DI - graph state persistence will not be available");
          return false;
        }

        // Resolve the save controller
        if (!resolver.TryResolve<Persistence.GraphStateSaveController>(out var saveController))
        {
          log.ForGameObject(gameObject).Warning("GraphStateSaveController not found in DI - cannot register with save system");
          return false;
        }

        // Register with the save coordinator
        saveCoordinator.RegisterLocalController(saveController);
        log.ForGameObject(gameObject).Information("Registered GraphStateSaveController with SaveSystemCoordinator");
        return true;
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).Warning(ex, "Failed to register GraphStateSaveController with save system: {Message}", ex.Message);
        // Don't throw - save system is optional
        return false;
      }
    }

    private async UniTask LoadQuestDataIfNeededAsync(Persistence.GraphStateSaveController saveController, SaveSystemCoordinator saveCoordinator)
    {
      try
      {
        // Check if save data exists for graphs domain
        // Use reflection to access private es3Service field
        var es3ServiceField = saveController.GetType().GetField("es3Service",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (es3ServiceField != null)
        {
          var es3Service = es3ServiceField.GetValue(saveController) as IES3Service;

          if (es3Service != null)
          {
            var hasQuestData = es3Service.KeyExists("graphs_quest_manager_state");
            var hasGraphData = es3Service.KeyExists("graphs_graph_states");

            if (hasQuestData || hasGraphData)
            {
              log.ForGameObject(gameObject).Information("Save data exists for graphs domain - ensuring quest definitions are loaded before restoration");

              // Always ensure quest definitions are loaded before restoring
              // Even if LoadAllOnStartup is true, the save system might load before InitializeGraphsAsync completes
              if (config != null && config.DefaultRegistry != null)
              {
                // Check if quest definitions are already loaded
                var existingQuestDefs = Runtime.GraphDefinitionRegistry.GetAllQuestDefinitions().ToList();
                log.ForGameObject(gameObject).Information("Found {Count} quest definitions already loaded in registry", existingQuestDefs.Count);

                // Load quest definitions if not already loaded (or if LoadAllOnStartup is false)
                if (existingQuestDefs.Count == 0 || !config.LoadAllOnStartup)
                {
                  log.ForGameObject(gameObject).Information("Loading quest definitions from registry before restoration");
                  await LoadQuestDefinitionsFromRegistryAsync(config.DefaultRegistry);
                  var loadedQuestDefs = Runtime.GraphDefinitionRegistry.GetAllQuestDefinitions().ToList();
                  log.ForGameObject(gameObject).Information("Now have {Count} quest definitions loaded in registry", loadedQuestDefs.Count);
                }
              }

              // Manually trigger load on the controller
              log.ForGameObject(gameObject).Information("Calling LoadAsync on GraphStateSaveController to restore quest data");
              await saveController.LoadAsync(default);
              log.ForGameObject(gameObject).Information("Manually loaded quest data after late registration");
            }
            else
            {
              log.ForGameObject(gameObject).Debug("No save data found for graphs domain - skipping manual load");
            }
          }
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).Warning(ex, "Error during manual quest data load: {Message}", ex.Message);
      }
    }

    public override void Shutdown()
    {
      log.ForGameObject(gameObject).Information("Shutting down Visual Graphs plugin");

      cts?.Cancel();
      cts?.Dispose();
      cts = null;

      if (service != null)
      {
        service.Clear();
      }
      (questManager as IDisposable)?.Dispose();
      questManager = null;

      base.Shutdown();

      log.ForGameObject(gameObject).Information("Visual Graphs plugin shutdown completed");
    }

    /// <summary>
    ///   Load quest definitions from the registry via addressables.
    /// </summary>
    private async UniTask<List<QuestDefinition>> LoadQuestDefinitionsFromRegistryAsync(VisualGraphRegistry registry)
    {
      var questDefs = new List<QuestDefinition>();

      if (registry == null || registry.QuestDefinitions == null)
        return questDefs;

      log.ForGameObject(gameObject).Information("Loading {Count} quest definitions from addressables...", registry.QuestDefinitions.Count);

      foreach (QuestAssetReference assetRef in registry.QuestDefinitions)
      {
        if (assetRef == null || !assetRef.HasValidGuid)
        {
          log.ForGameObject(gameObject).Warning("Skipping invalid quest asset reference (null or missing GUID)");
          continue;
        }

        try
        {
          var handle = Addressables.LoadAssetAsync<QuestDefinition>(assetRef);
          var questDef = await handle;

          if (questDef != null)
          {
            questDefs.Add(questDef);
            // Register in the runtime registry for lookup by GUID
            Runtime.GraphDefinitionRegistry.RegisterQuestDefinition(questDef);
            log.ForGameObject(gameObject).Debug("Loaded quest definition '{QuestName}' ({QuestGuid}) from addressables",
              questDef.DisplayName, questDef.Guid);
          }
          else
          {
            log.ForGameObject(gameObject).Warning("Failed to load quest definition from addressable (GUID: {Guid})", assetRef.AssetGUID);
          }
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).Error(ex, "Error loading quest definition from addressable (GUID: {Guid}): {Message}",
            assetRef.AssetGUID, ex.Message);
        }
      }

      await UniTask.WaitForEndOfFrame();

      return questDefs;
    }

    /// <summary>
    ///   Initialize all graphs from the registry.
    /// </summary>
    private async UniTask InitializeGraphsAsync(VisualGraphRegistry registry)
    {
      if (registry == null)
      {
        log.ForGameObject(gameObject).Error("Cannot initialize graphs: registry is null");
        return;
      }

      log.ForGameObject(gameObject).Information("Initializing visual graph system (load all on startup)...");

      var loadTasks = new List<UniTask>();

      // Load quest definitions from registry (may be asset references for addressables)
      var questDefs = await LoadQuestDefinitionsFromRegistryAsync(registry);

      // Use self-registered definitions (preferred) or fallback to loaded registry quests
      var registeredQuestDefs = Runtime.GraphDefinitionRegistry.GetAllQuestDefinitions().ToList();
      if (registeredQuestDefs.Count > 0)
      {
        questDefs = registeredQuestDefs;
      }

      foreach (var questDef in questDefs)
      {
        // Quest-level graphs are OPTIONAL - skip if no GraphAsset
        // Objective graphs are loaded separately when quest starts (they're mandatory per objective)
        if (questDef.GraphAsset == null)
        {
          log.ForGameObject(gameObject).Debug(
            "Skipping quest '{QuestName}' ({QuestId}) - no quest-level GraphAsset (quest-level graphs are optional)",
            questDef.DisplayName, questDef.Guid);
          continue;
        }

        try
        {
          var loadTask = graphLoader.LoadGraphAsync(questDef.Guid, cts.Token);
          loadTasks.Add(loadTask.ContinueWith(runner =>
          {
            if (runner == null)
            {
              log.ForGameObject(gameObject).Debug(
                "Quest '{QuestId}' has no quest-level graph (optional) - skipped",
                questDef.Guid);
            }
            return runner;
          }).AsUniTask());
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).Error(ex,
            "Failed to initialize quest graph '{QuestId}': {Message}",
            questDef.Guid, ex.Message);
        }
      }

      // Use self-registered definitions (preferred) or fallback to registry
      var dialogueDefs = Runtime.GraphDefinitionRegistry.GetAllDialogueDefinitions().ToList();
      if (dialogueDefs.Count == 0 && registry != null && registry.DialogueDefinitions != null)
      {
        // Fallback to registry asset for backward compatibility
        dialogueDefs = registry.DialogueDefinitions.Where(d => d != null).ToList();
      }

      foreach (var dialogueDef in dialogueDefs)
      {
        try
        {
          loadTasks.Add(graphLoader.LoadGraphAsync(dialogueDef.DialogueId, cts.Token).AsUniTask());
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).Error(ex,
            "Failed to initialize dialogue graph '{DialogueId}': {Message}",
            dialogueDef.DialogueId, ex.Message);
        }
      }

      log.ForGameObject(gameObject).Information("Loading {Count} graphs...", loadTasks.Count);

      // Wait for all graphs to load
      try
      {
        await UniTask.WhenAll(loadTasks);
        log.ForGameObject(gameObject).Information(
          "Visual graph system initialized: {Count} graphs loaded", loadTasks.Count);

        // Now that graphs are loaded, subscribe to MessagePipe events
        if (eventBusBridge != null && graphEventRouter != null)
        {
          // Ensure router is injected before subscribing
          eventBusBridge.Construct(graphEventRouter);
          eventBusBridge.SubscribeToGraphMessages();
        }
        else
        {
          log.ForGameObject(gameObject).Warning(
            "EventBusBridge or GraphEventRouter not found - graphs will not receive MessagePipe events");
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).Error(ex, "Error during graph initialization: {Message}", ex.Message);
      }
    }

    private void OnQuestStarted(Quest.Messages.QuestStartedMessage message)
    {
      // Re-subscribe EventBusBridge to include new objective graph subscriptions
      if (eventBusBridge != null && graphEventRouter != null)
      {
        log.ForGameObject(gameObject).Information("Quest '{QuestName}' started - re-subscribing EventBusBridge to include objective graph subscriptions",
          message.Quest?.DisplayName ?? message.QuestGuid);
        eventBusBridge.Construct(graphEventRouter);
        eventBusBridge.SubscribeToGraphMessages();
      }
    }

    private sealed class QuestStartedMessageHandler : MessagePipe.IMessageHandler<Quest.Messages.QuestStartedMessage>
    {
      private readonly Action<Quest.Messages.QuestStartedMessage> action;

      public QuestStartedMessageHandler(Action<Quest.Messages.QuestStartedMessage> action)
      {
        this.action = action ?? throw new ArgumentNullException(nameof(action));
      }

      public void Handle(Quest.Messages.QuestStartedMessage message)
      {
        action(message);
      }
    }

    private async UniTask AutoStartQuestAsync(IObjectResolver resolver)
    {
      log.ForGameObject(gameObject).ForMethod().Information("Quest: AutoStartQuestAsync invoked");

      try
      {
        log.ForGameObject(gameObject).ForMethod().Debug("Quest: Resolving IQuestManager from DI container");
        var questManager = resolver.Resolve<Quest.IQuestManager>();
        log.ForGameObject(gameObject).ForMethod().Information("Quest: IQuestManager resolved successfully: {Type}", questManager.GetType().Name);

        var database = config.QuestDatabase;
        log.ForGameObject(gameObject).ForMethod().Debug("Quest: QuestDatabase: {Database}", database != null ? "Found" : "NULL");

        var quest = database.GetFirstQuest();
        if (quest == null)
        {
          log.ForGameObject(gameObject).Warning(
            "QuestDatabase has no quests to auto-start");
          return;
        }

        // Check if quest is already active (might have been restored from save)
        if (questManager.IsQuestActive(quest.Guid))
        {
          log.ForGameObject(gameObject).Information(
            "Quest '{QuestName}' is already active (likely restored from save), skipping auto-start", quest.DisplayName);
          return;
        }

        // Check if quest is already completed or claimed (might have been restored from save)
        if (questManager.IsQuestCompleted(quest.Guid) || questManager.IsQuestClaimed(quest.Guid))
        {
          log.ForGameObject(gameObject).Information(
            "Quest '{QuestName}' is already completed or claimed (likely restored from save), skipping auto-start", quest.DisplayName);
          return;
        }

        log.ForGameObject(gameObject).Information(
          "Auto-starting first quest: {QuestName}", quest.DisplayName);

        var success = await questManager.StartQuestAsync(quest, cts.Token);

        if (success)
        {
          log.ForGameObject(gameObject).Information(
            "Successfully auto-started quest: {QuestName}", quest.DisplayName);

          // Re-subscribe to MessagePipe events now that quest graphs are loaded
          if (eventBusBridge != null && graphEventRouter != null)
          {
            log.ForGameObject(gameObject).Information("Re-subscribing EventBusBridge to include quest graph subscriptions");
            // Ensure router is injected before subscribing
            eventBusBridge.Construct(graphEventRouter);
            eventBusBridge.SubscribeToGraphMessages();
          }
        }
        else
        {
          log.ForGameObject(gameObject).Error(
            "Failed to auto-start quest: {QuestName}", quest.DisplayName);
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).Error(ex, "Error during quest auto-start: {Message}", ex.Message);
      }
    }

    /// <summary>
    ///   Inject configuration and other dependencies.
    /// </summary>
    [Inject]
    public void Construct()
    {
    }

    // ==================== Public API ====================

    /// <summary>
    ///   Load a graph dynamically by ID.
    ///   Supports both Addressables (if key specified) and direct references.
    /// </summary>
    public async UniTask<IGraphRunner> LoadGraphAsync(string graphId)
    {
      if (graphLoader == null)
      {
        log.ForGameObject(gameObject).Error("Cannot load graph: IGraphLoader not initialized");
        return null;
      }

      if (cts == null || cts.IsCancellationRequested)
      {
        log.ForGameObject(gameObject).Error("Cannot load graph: plugin is shutting down");
        return null;
      }

      return await graphLoader.LoadGraphAsync(graphId, cts.Token);
    }

    /// <summary>Unload a graph and free resources</summary>
    public void UnloadGraph(string graphId)
    {
      if (graphLoader == null)
      {
        log.ForGameObject(gameObject).Error("Cannot unload graph: IGraphLoader not initialized");
        return;
      }

      graphLoader.UnloadGraph(graphId);
    }

    /// <summary>Check if a graph is loaded</summary>
    public bool IsGraphLoaded(string graphId)
    {
      if (graphLoader == null)
      {
        log.ForGameObject(gameObject).Error("Cannot check graph status: IGraphLoader not initialized");
        return false;
      }

      return graphLoader.IsLoaded(graphId);
    }

    private void OnDestroy()
    {
      questStartedSubscription?.Dispose();
      questStartedSubscription = null;
      cts?.Cancel();
      cts?.Dispose();
    }
  }

  /// <summary>
  ///   Event emitter that publishes graph events to MessagePipe.
  ///   Uses reflection to invoke the generic Publish method for the concrete message type.
  /// </summary>
  internal sealed class VisualGraphEventEmitter : IEventEmitter
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<VisualGraphEventEmitter>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private Quest.IQuestManager questManager;

    public VisualGraphEventEmitter()
    {
    }

    public void SetQuestManager(Quest.IQuestManager questManager)
    {
      this.questManager = questManager;
    }

    public void Emit(IGameMessage message, string domain = null)
    {
      if (message == null)
      {
        log.Warning("Attempted to emit null message");
        return;
      }

      var messageType = message.GetType();

      // Use GameMessageBroker to publish the concrete message type (VisualGraphs is a game system)
      try
      {
        var publishMethod = typeof(MessageBus.GameMessageBroker)
          .GetMethod(nameof(MessageBus.GameMessageBroker.Publish))
          ?.MakeGenericMethod(messageType);

        if (publishMethod == null)
        {
          log.Error("Failed to get Publish method for type {MessageType}", messageType.Name);
          return;
        }

        publishMethod.Invoke(null, new object[] { message });
        log.Debug("Emitted {MessageType} to GameMessageBroker (domain: {Domain})",
          messageType.Name, domain ?? "none");

        // Extra logging for quest progress messages
        if (messageType == typeof(Quest.Messages.QuestObjectiveProgressMessage))
        {
          log.ForMethod().Information("Quest: Published QuestObjectiveProgressMessage to GameMessageBroker (domain: {Domain})", domain ?? "none");
          var progressMsg = (Quest.Messages.QuestObjectiveProgressMessage)message;
          if (questManager != null)
          {
            var objectiveComplete = progressMsg.Current >= progressMsg.Required;
            log.ForMethod().Information("Quest: Message: {Message}. ObjectiveCompleted: {ObjectiveCompleted}", message.ToString(), objectiveComplete ? "Yes" : "No");

            // Check if ALL objectives are complete before completing the quest
            if (objectiveComplete)
            {
              var questState = questManager.GetQuestState(progressMsg.QuestGuid);
              if (questState != null && questState.Definition != null)
              {
                var allObjectivesComplete = questState.Definition.IsComplete(questState.GraphState);
                log.ForMethod().Information("Quest: Objective {ObjectiveGuid} complete. All objectives complete: {AllComplete} (Quest: {QuestGuid})",
                  progressMsg.ObjectiveGuid, allObjectivesComplete, progressMsg.QuestGuid);

                if (allObjectivesComplete)
                {
                  log.ForMethod().Information("Quest: All objectives complete - completing quest: {QuestGuid}", progressMsg.QuestGuid);
                  questManager.CompleteQuest(progressMsg.QuestGuid);
                }
                else
                {
                  log.ForMethod().Information("Quest: Not all objectives complete yet - quest remains active: {QuestGuid}", progressMsg.QuestGuid);
                }
              }
              else
              {
                log.ForMethod().Warning("Quest: Cannot check quest completion - quest state or definition not found for {QuestGuid}", progressMsg.QuestGuid);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        log.Error(ex, "Failed to emit {MessageType} to MessagePipe", messageType.Name);
      }
    }
  }
}

