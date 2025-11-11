using System;
using MToolKit.Runtime.VisualGraphs.Definitions;
using MToolKit.Runtime.VisualGraphs.Export;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.VisualGraphs.Bootstrap
{
    /// <summary>
    /// MonoBehaviour that bootstraps the visual graph system.
    /// Exports definitions, initializes runners, and registers them with the router.
    /// </summary>
    public sealed class VisualGraphBootstrapMB : MonoBehaviour
    {
        private static readonly Lazy<ILogger> _logLazy = new(() => 
            Log.Logger.ForContext<VisualGraphBootstrapMB>().ForFeature("VisualGraphs"));
        private static ILogger log => _logLazy.Value ?? Serilog.Core.Logger.None;

        [Required]
        [Tooltip("Visual graph registry containing all definitions")]
        public VisualGraphRegistry registry;

        [Tooltip("Enable verbose logging for graph initialization")]
        public bool verboseLogging = false;

        private GraphEventRouter _router;
        private NodeExecutorRegistry _executorRegistry;
        private IServiceProvider _services;
        private IEventEmitter _eventEmitter;

        [Inject]
        public void Construct(
            GraphEventRouter router,
            NodeExecutorRegistry executorRegistry,
            IServiceProvider services,
            IEventEmitter eventEmitter)
        {
            _router = router;
            _executorRegistry = executorRegistry;
            _services = services;
            _eventEmitter = eventEmitter;
        }

        private void Awake()
        {
            if (registry == null)
            {
                log.Error("VisualGraphRegistry is not assigned to VisualGraphBootstrapMB!");
                return;
            }

            InitializeGraphs();
        }

        private void InitializeGraphs()
        {
            log.Information("Initializing visual graph system...");

            var exporter = new XNodeGraphExporter(_executorRegistry);
            int initializedCount = 0;

            // Initialize quest graphs
            foreach (var questDef in registry.questDefinitions)
            {
                if (questDef == null || questDef.graphAsset == null)
                {
                    log.Warning("Skipping null quest definition or graph asset");
                    continue;
                }

                try
                {
                    InitializeQuestGraph(questDef, exporter);
                    initializedCount++;
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to initialize quest graph '{QuestId}': {Message}", 
                        questDef.questId, ex.Message);
                }
            }

            // Initialize dialogue graphs
            foreach (var dialogueDef in registry.dialogueDefinitions)
            {
                if (dialogueDef == null || dialogueDef.graphAsset == null)
                {
                    log.Warning("Skipping null dialogue definition or graph asset");
                    continue;
                }

                try
                {
                    InitializeDialogueGraph(dialogueDef, exporter);
                    initializedCount++;
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Failed to initialize dialogue graph '{DialogueId}': {Message}", 
                        dialogueDef.dialogueId, ex.Message);
                }
            }

            log.Information("Visual graph system initialized: {Count} graphs ready", initializedCount);
        }

        private void InitializeQuestGraph(QuestDefinition questDef, XNodeGraphExporter exporter)
        {
            // Export graph
            var runtimeDef = exporter.Export(questDef.graphAsset);
            runtimeDef.GraphId = questDef.questId;

            // Create state
            var state = new InMemoryGraphState();

            // Apply variables in order: global → definition → (save will be restored later)
            if (registry.globalVariables != null)
            {
                var globalVars = registry.globalVariables.GetFor(questDef.questId);
                globalVars?.ApplyTo(state);
            }

            questDef.initialVariables?.ApplyTo(state);

            // Create and register runner
            var runner = new GraphRunner(runtimeDef, state, _executorRegistry, _services, _eventEmitter);
            _router.RegisterRunner(runner);

            if (verboseLogging)
            {
                log.Debug("Initialized quest graph '{QuestId}': {NodeCount} nodes, {SubscriptionCount} subscriptions",
                    questDef.questId, runtimeDef.Nodes.Count, runtimeDef.Subscriptions.Count);
            }
        }

        private void InitializeDialogueGraph(DialogueDefinition dialogueDef, XNodeGraphExporter exporter)
        {
            // Export graph
            var runtimeDef = exporter.Export(dialogueDef.graphAsset);
            runtimeDef.GraphId = dialogueDef.dialogueId;

            // Create state
            var state = new InMemoryGraphState();

            // Apply variables in order: global → definition → (save will be restored later)
            if (registry.globalVariables != null)
            {
                var globalVars = registry.globalVariables.GetFor(dialogueDef.dialogueId);
                globalVars?.ApplyTo(state);
            }

            dialogueDef.initialVariables?.ApplyTo(state);

            // Create and register runner
            var runner = new GraphRunner(runtimeDef, state, _executorRegistry, _services, _eventEmitter);
            _router.RegisterRunner(runner);

            if (verboseLogging)
            {
                log.Debug("Initialized dialogue graph '{DialogueId}': {NodeCount} nodes, {SubscriptionCount} subscriptions",
                    dialogueDef.dialogueId, runtimeDef.Nodes.Count, runtimeDef.Subscriptions.Count);
            }
        }
    }
}

