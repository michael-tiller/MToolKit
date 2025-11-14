using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Bootstrap;
using MToolKit.Runtime.VisualGraphs.Config;
using MToolKit.Runtime.VisualGraphs.Definitions;
using MToolKit.Runtime.VisualGraphs.Executors;
using MToolKit.Runtime.VisualGraphs.Quest.Executors;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Loading;
using MToolKit.Runtime.VisualGraphs.Dialogue.Executors;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

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
    private Runtime.GraphEventRouter graphEventRouter;
    private Quest.IQuestManager questManager;

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
      builder.Register<QuestAllObjectivesCompleteNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();

      // Message executors (data flow from messages)
      builder.Register<MessageFieldCheckNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<MessageFieldGetNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<MessageTypeCheckNodeExecutor>(Lifetime.Singleton)
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

        // Auto-start quest if configured
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

      // Load quest graphs
      foreach (var questDef in registry.QuestDefinitions)
      {
        if (questDef == null)
        {
          log.ForGameObject(gameObject).Warning("Skipping null quest definition");
          continue;
        }

        try
        {
          loadTasks.Add(graphLoader.LoadGraphAsync(questDef.Guid, cts.Token).AsUniTask());
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).Error(ex,
            "Failed to initialize quest graph '{QuestId}': {Message}",
            questDef.Guid, ex.Message);
        }
      }

      // Load dialogue graphs
      foreach (var dialogueDef in registry.DialogueDefinitions)
      {
        if (dialogueDef == null)
        {
          log.ForGameObject(gameObject).Warning("Skipping null dialogue definition");
          continue;
        }

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
            log.ForMethod().Information("Quest: Message: {Message}. QuestCompleted: {QuestCompleted}", message.ToString(), progressMsg.Current >= progressMsg.Required ? "Yes" : "No");

            if (progressMsg.Current >= progressMsg.Required)
            {
              log.ForMethod().Information("Quest: Quest completed: {QuestGuid}", progressMsg.QuestGuid);
              questManager.CompleteQuest(progressMsg.QuestGuid);
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

