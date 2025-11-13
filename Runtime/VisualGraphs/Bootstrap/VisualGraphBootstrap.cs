using System;
using MToolKit.Runtime.VisualGraphs.Definitions;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Bootstrap
{
  /// <summary>
  ///   MonoBehaviour that bootstraps the visual graph system.
  ///   Exports definitions, initializes runners, and registers them with the router.
  /// </summary>
  public sealed class VisualGraphBootstrap : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<VisualGraphBootstrap>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [Required]
    [Tooltip("Visual graph registry containing all definitions")]
    public VisualGraphRegistry Registry;

    [Tooltip("Enable verbose logging for graph initialization")]
    public bool VerboseLogging;

    private IEventEmitter eventEmitter;
    private NodeExecutorRegistry executorRegistry;

    private GraphEventRouter router;
    private IServiceProvider services;

    private void Awake()
    {
      if (Registry == null)
      {
        log.Error("VisualGraphRegistry is not assigned to VisualGraphBootstrapMB!");
        return;
      }

      InitializeGraphs();
    }

    [Inject]
    public void Construct(GraphEventRouter router, NodeExecutorRegistry executorRegistry, IServiceProvider services, IEventEmitter eventEmitter)
    {
      this.router = router;
      this.executorRegistry = executorRegistry;
      this.services = services;
      this.eventEmitter = eventEmitter;
    }

    private void InitializeGraphs()
    {
      log.Information("Initializing visual graph system...");

      var exporter = new XNodeGraphExporter(executorRegistry);
      var initializedCount = 0;

      // Initialize quest graphs
      foreach (var questDef in Registry.QuestDefinitions)
      {
        if (questDef == null || questDef.GraphAsset == null)
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
            questDef.QuestId, ex.Message);
        }
      }

      // Initialize dialogue graphs
      foreach (var dialogueDef in Registry.DialogueDefinitions)
      {
        if (dialogueDef == null || dialogueDef.GraphAsset == null)
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
            dialogueDef.DialogueId, ex.Message);
        }
      }

      log.Information("Visual graph system initialized: {Count} graphs ready", initializedCount);
    }

    private void InitializeQuestGraph(QuestDefinition questDef, XNodeGraphExporter exporter)
    {
      // Export graph
      var runtimeDef = exporter.Export(questDef.GraphAsset);
      runtimeDef.GraphId = questDef.QuestId;

      // Create state
      var state = new InMemoryGraphState();

      // Apply variables in order: global → definition → (save will be restored later)
      if (Registry.GlobalVariables != null)
      {
        var globalVars = Registry.GlobalVariables.GetFor(questDef.QuestId);
        globalVars?.ApplyTo(state);
      }

      questDef.InitialVariables?.ApplyTo(state);

      // Create and register runner
      var runner = new GraphRunner(runtimeDef, state, executorRegistry, services, eventEmitter);
      router.RegisterRunner(runner);

      if (VerboseLogging)
        log.Debug("Initialized quest graph '{QuestId}': {NodeCount} nodes, {SubscriptionCount} subscriptions",
          questDef.QuestId, runtimeDef.Nodes.Count, runtimeDef.Subscriptions.Count);
    }

    private void InitializeDialogueGraph(DialogueDefinition dialogueDef, XNodeGraphExporter exporter)
    {
      // Export graph
      var runtimeDef = exporter.Export(dialogueDef.GraphAsset);
      runtimeDef.GraphId = dialogueDef.DialogueId;

      // Create state
      var state = new InMemoryGraphState();


      // Apply variables in order: global → definition → (save will be restored later)
      if (Registry.GlobalVariables != null)
      {
        var globalVars = Registry.GlobalVariables.GetFor(dialogueDef.DialogueId);
        globalVars?.ApplyTo(state);
      }

      dialogueDef.InitialVariables?.ApplyTo(state);

      // Create and register runner
      var runner = new GraphRunner(runtimeDef, state, executorRegistry, services, eventEmitter);
      router.RegisterRunner(runner);

      if (VerboseLogging)
        log.Debug("Initialized dialogue graph '{DialogueId}': {NodeCount} nodes, {SubscriptionCount} subscriptions",
          dialogueDef.DialogueId, runtimeDef.Nodes.Count, runtimeDef.Subscriptions.Count);
    }
  }
}