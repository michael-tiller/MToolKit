using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Export;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Rules
{
  /// <summary>
  /// Owns GameRule runners over the existing event-graph engine. Their shared World state uses
  /// the existing scope persistence; each runner keeps a distinct rule identity for diagnostics.
  /// </summary>
  public sealed class GameRuleRegistry : IDisposable
  {
    private static ILogger log => Log.Logger.ForContext<GameRuleRegistry>().ForFeature("VisualGraphs.Rules") ?? Logger.None;
    private sealed class RuleEntry
    {
      public GameRuleDefinition Definition;
      public GraphRunner Runner;
      public bool SessionDisabled;
      public int Depth;
      public Action<string, string, Exception> FailureHandler;
    }

    private readonly GraphEventRouter router;
    private readonly GraphContextRegistry contexts;
    private readonly NodeExecutorRegistry executors;
    private readonly IServiceProvider services;
    private readonly IEventEmitter emitter;
    private readonly Dictionary<string, RuleEntry> entries = new(StringComparer.Ordinal);
    private bool disposed;

    /// <summary>Create a registry using the same routing, contexts and executor services as domain graphs.</summary>
    /// <param name="router">Router that orders rule hooks and normal graph delivery.</param>
    /// <param name="contexts">Shared context registry providing persistent World state.</param>
    /// <param name="executors">Registered node executors used by the event graph engine.</param>
    /// <param name="services">Service provider forwarded to node execution contexts.</param>
    /// <param name="emitter">Outbound graph message emitter.</param>
    public GameRuleRegistry(GraphEventRouter router, GraphContextRegistry contexts, NodeExecutorRegistry executors,
      IServiceProvider services, IEventEmitter emitter)
    {
      this.router = router ?? throw new ArgumentNullException(nameof(router));
      this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
      this.executors = executors ?? throw new ArgumentNullException(nameof(executors));
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary>
    /// Export and register configured EventGraphAssets. Invalid rules fail independently, so a bad
    /// authored rule cannot stop other rules or normal graph delivery from being initialized.
    /// </summary>
    /// <param name="definitions">Configured rules to export; null means no active rules.</param>
    public void LoadRules(IEnumerable<GameRuleDefinition> definitions)
    {
      ThrowIfDisposed();
      ClearRules();
      if (definitions == null) return;
      var exporter = new XNodeGraphExporter(executors);
      foreach (var definition in definitions)
      {
        if (definition == null) continue;
        try
        {
          if (definition.Graph == null)
            throw new InvalidOperationException($"GameRule '{definition.RuleName}' has no EventGraphAsset.");
          RegisterRule(definition, exporter.Export(definition.Graph));
        }
        catch (Exception exception)
        {
          log.Error(exception, "GameRule '{RuleName}' could not be loaded and is disabled for this session", definition.RuleName);
        }
      }
    }

    /// <summary>
    /// Register one already-exported rule graph. The definition is copied before assigning the rule
    /// identity; reusing the same authored graph for different rules never mutates its Event metadata.
    /// </summary>
    /// <param name="definition">Rule identity, author enable switch, order and timing.</param>
    /// <param name="runtimeDefinition">Exported Event graph whose immutable shape is reused by the rule.</param>
    /// <returns>The owned runner sharing the World scope backing state.</returns>
    public GraphRunner RegisterRule(GameRuleDefinition definition, RuntimeGraphDefinition runtimeDefinition)
    {
      ThrowIfDisposed();
      if (definition == null) throw new ArgumentNullException(nameof(definition));
      if (runtimeDefinition == null) throw new ArgumentNullException(nameof(runtimeDefinition));
      if (string.IsNullOrWhiteSpace(definition.RuleName))
        throw new ArgumentException("A GameRule must have a stable, non-empty RuleName.", nameof(definition));
      var id = "rule:" + definition.RuleName;
      if (entries.ContainsKey(id)) throw new InvalidOperationException($"GameRule '{definition.RuleName}' is already registered.");

      var runtime = new RuntimeGraphDefinition
      {
        GraphId = id,
        GraphDomain = "Rule",
        MaxExecutionSteps = runtimeDefinition.MaxExecutionSteps,
        Subscriptions = new List<RuntimeSubscriptionDefinition>(runtimeDefinition.Subscriptions),
        Nodes = new List<RuntimeNodeDefinition>(runtimeDefinition.Nodes),
        Connections = new List<RuntimeConnectionDefinition>(runtimeDefinition.Connections)
      };
      contexts.GetOrCreate(EGraphContextScope.World, null);
      var runner = new GraphRunner(runtime, contexts.GetScopeStateOrNull(EGraphContextScope.World), executors, services, emitter);
      var entry = new RuleEntry { Definition = definition, Runner = runner };
      entry.FailureHandler = (graphId, nodeId, exception) => Disable(entry, nodeId, exception);
      router.RegisterRuleRunner(runner, definition.ExecutionOrder, definition.Timing,
        (message, domain, ct) => InvokeAsync(entry, message, domain, ct),
        () => !disposed && definition.Enabled && !entry.SessionDisabled);
      runner.NodeExecutionFailed += entry.FailureHandler;
      entries.Add(id, entry);
      return runner;
    }

    /// <summary>Look up an owned rule runner using its authored rule name.</summary>
    /// <param name="ruleName">Authored rule name, without the runtime rule identity prefix.</param>
    /// <returns>The registered runner, or null when the name is absent.</returns>
    public GraphRunner GetRunner(string ruleName)
    {
      return ruleName != null && entries.TryGetValue("rule:" + ruleName, out var entry) ? entry.Runner : null;
    }

    /// <summary>Whether this rule remains enabled after author settings and session failures are applied.</summary>
    /// <param name="ruleName">Authored rule name, without the runtime rule identity prefix.</param>
    /// <returns>True only for a registered, authored-enabled rule with no session failure.</returns>
    public bool IsEnabled(string ruleName)
    {
      return ruleName != null && entries.TryGetValue("rule:" + ruleName, out var entry) &&
             entry.Definition.Enabled && !entry.SessionDisabled;
    }

    private async UniTask InvokeAsync(RuleEntry entry, IGameMessage message, string domain, CancellationToken ct)
    {
      if (disposed || !entry.Definition.Enabled || entry.SessionDisabled || ct.IsCancellationRequested) return;
      if (entry.Depth > 0)
      {
        log.Error("GameRule '{RuleName}' re-entered its own dispatch; skipping the recursive invocation", entry.Definition.RuleName);
        return;
      }
      entry.Depth++;
      try
      {
        await entry.Runner.HandleMessageAsync(message, domain, ct);
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        // Cancellation is not a rule failure; the router observes the same cancelled token.
      }
      catch (Exception exception)
      {
        // Authoring errors intentionally escape normal GraphRunner execution. Rules isolate those too.
        Disable(entry, null, exception);
      }
      finally
      {
        entry.Depth--;
      }
    }

    private static void Disable(RuleEntry entry, string nodeId, Exception exception)
    {
      if (entry.SessionDisabled) return;
      entry.SessionDisabled = true;
      log.Error(exception, "GameRule '{RuleName}' failed at node '{NodeId}' and is disabled for this session",
        entry.Definition.RuleName, nodeId ?? "(dispatch)");
    }

    /// <summary>Release owned runners and failure handlers while retaining persistent World scope state.</summary>
    public void ClearRules()
    {
      foreach (var entry in entries.Values.ToArray())
      {
        router.UnregisterRunner(entry.Runner);
        entry.Runner.NodeExecutionFailed -= entry.FailureHandler;
      }
      entries.Clear();
    }

    /// <summary>End this rule session and release all owned routing registrations.</summary>
    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      ClearRules();
    }

    private void ThrowIfDisposed()
    {
      if (disposed) throw new ObjectDisposedException(nameof(GameRuleRegistry));
    }
  }
}
