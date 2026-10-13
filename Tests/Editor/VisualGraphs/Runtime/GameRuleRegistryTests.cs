using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.Runtime.Core.Types;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Persistence;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  public sealed class GameRuleRegistryTests : UnityObjectCleanup
  {
    private GraphEventRouter router;
    private GraphContextRegistry contexts;
    private NodeExecutorRegistry executors;
    private object registry;
    private Type definitionType;

    [SetUp]
    public void SetupRules()
    {
      router = new GraphEventRouter();
      var emitter = new RecordingEmitter();
      contexts = new GraphContextRegistry(emitter);
      executors = new NodeExecutorRegistry();
      var assembly = typeof(GraphRunner).Assembly;
      var registryType = assembly.GetType("MToolKit.Runtime.VisualGraphs.Rules.GameRuleRegistry");
      definitionType = assembly.GetType("MToolKit.Runtime.VisualGraphs.Rules.GameRuleDefinition");
      Assert.That(registryType, Is.Not.Null, "GameRules must run the existing event graph engine");
      Assert.That(definitionType, Is.Not.Null);
      registry = Activator.CreateInstance(registryType, router, contexts, executors, new NullServiceProvider(), emitter);
    }

    [TearDown]
    public void TeardownRules()
    {
      (registry as IDisposable)?.Dispose();
    }

    private GraphRunner AddRule(string name, int order, string timing, string action, bool enabled = true)
    {
      var definition = Track(ScriptableObject.CreateInstance(definitionType));
      definitionType.GetField("RuleName").SetValue(definition, name);
      definitionType.GetField("ExecutionOrder").SetValue(definition, order);
      definitionType.GetField("Enabled").SetValue(definition, enabled);
      var timingField = definitionType.GetField("Timing");
      timingField.SetValue(definition, Enum.Parse(timingField.FieldType, timing));
      var graph = new RuntimeGraphDefinition { GraphId = "shared_graph", GraphDomain = "Event" };
      graph.Subscriptions.Add(new RuntimeSubscriptionDefinition { MessageType = new MessageTypeReference(typeof(TestMessageA)) });
      graph.Nodes.Add(new RuntimeNodeDefinition { NodeId = "entry", NodeType = "EventEntryNode" });
      graph.Nodes.Add(new RuntimeNodeDefinition { NodeId = "action", NodeType = action });
      graph.Connections.Add(new RuntimeConnectionDefinition { FromNodeId = "entry", ToNodeId = "action" });
      var register = registry.GetType().GetMethod("RegisterRule", new[] { definitionType, typeof(RuntimeGraphDefinition) });
      Assert.That(register, Is.Not.Null);
      return (GraphRunner)register.Invoke(registry, new object[] { definition, graph });
    }

    private void AddExecutor(string type, Action callback, bool throws = false)
    {
      executors.Register(new RecordingExecutor(type) { OnExecute = (_, __) => callback(), ShouldThrow = throws });
    }

    [Test]
    public void Rules_RunOrderedBeforeAndAfterNormalDomainGraphs()
    {
      var order = new List<string>();
      foreach (var name in new[] { "before1", "before2", "after1", "after2" })
      {
        var capture = name;
        AddExecutor(name, () => order.Add(capture));
      }
      AddRule("b2", 2, "BeforeDomainGraphs", "before2");
      AddRule("a2", 2, "AfterDomainGraphs", "after2");
      AddRule("b1", 1, "BeforeDomainGraphs", "before1");
      AddRule("a1", 1, "AfterDomainGraphs", "after1");
      router.RegisterRunner(new FakeGraphRunner("domain", GraphDefBuilder.New().Subscribe(typeof(TestMessageA)).Build())
        { OnHandled = () => order.Add("domain") });
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(order, Is.EqualTo(new[] { "before1", "before2", "domain", "after1", "after2" }));
    }

    [Test]
    public void DisabledRule_IsInert()
    {
      var calls = 0;
      AddExecutor("Act", () => calls++);
      AddRule("disabled", 0, "BeforeDomainGraphs", "Act", false);
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(calls, Is.Zero);
    }

    [Test]
    public void DisabledTraffic_DoesNotSuspendRuleWhenAuthorEnablesIt()
    {
      router.TimeProvider = () => 0f;
      var calls = 0;
      AddExecutor("Act", () => calls++);
      var runner = AddRule("disabled", 0, "BeforeDomainGraphs", "Act", false);
      for (var i = 0; i <= GraphEventRouter.MaxDispatchesPerWindow; i++)
        router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      var entries = (System.Collections.IDictionary)registry.GetType()
        .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
      var entry = entries[runner.GraphId];
      var definition = entry.GetType().GetField("Definition").GetValue(entry);
      definitionType.GetField("Enabled").SetValue(definition, true);
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(calls, Is.EqualTo(1), "disabled traffic must not consume the active dispatch budget");
    }

    [Test]
    public void ThrowingRule_DisabledForSessionAndSameMessageStillReachesDomainGraph()
    {
      var calls = 0;
      AddExecutor("Boom", () => calls++, true);
      var runner = AddRule("broken", 0, "BeforeDomainGraphs", "Boom");
      var failureCount = 0;
      typeof(GraphRunner).GetEvent("NodeExecutionFailed").AddEventHandler(runner,
        new Action<string, string, Exception>((_, __, ___) => failureCount++));
      var domain = new FakeGraphRunner("domain", GraphDefBuilder.New().Subscribe(typeof(TestMessageA)).Build());
      router.RegisterRunner(domain);
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(calls, Is.EqualTo(1));
      Assert.That(failureCount, Is.EqualTo(1));
      Assert.That(domain.HandleMessageAsyncCallCount, Is.EqualTo(2));
    }

    [Test]
    public void SelfTrigger_PerRuleDepthGuardSkipsReentry()
    {
      var calls = 0;
      AddExecutor("Republish", () => { calls++; router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult(); });
      AddRule("recursive", 0, "BeforeDomainGraphs", "Republish");
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(calls, Is.EqualTo(1), "per-rule guard must stop the recursive call before the generic depth budget");
    }

    [Test]
    public void Rules_SharedGraphHasDistinctIdentityAndWorldState()
    {
      AddExecutor("Act", () => {});
      var first = AddRule("one", 0, "BeforeDomainGraphs", "Act");
      var second = AddRule("two", 0, "BeforeDomainGraphs", "Act");
      Assert.That(first.GraphId, Is.Not.EqualTo(second.GraphId));
      first.ImportState(new MToolKit.Runtime.VisualGraphs.Runtime.State.GraphStateSnapshot
        { GraphId = first.GraphId, Data = new Dictionary<string, object> { { "rule_counter", 2 } } });
      var world = contexts.GetScopeStateOrNull(EGraphContextScope.World);
      Assert.That(world, Is.Not.Null);
      Assert.That(world.TryGet<int>("rule_counter", out var counter), Is.True);
      Assert.That(counter, Is.EqualTo(2));
    }

    [Test]
    public void EqualOrder_StableAndRulesAreListedOnceForBrokerDiscoveryAndPersistence()
    {
      var order = new List<string>();
      AddExecutor("A", () => order.Add("a"));
      AddExecutor("B", () => order.Add("b"));
      AddRule("one", 0, "BeforeDomainGraphs", "A");
      AddRule("two", 0, "BeforeDomainGraphs", "B");
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(order, Is.EqualTo(new[] { "a", "b" }));
      Assert.That(new List<IGraphRunner>(router.GetRunners()).Count, Is.EqualTo(2));
      Assert.That(router.GetSubscribedMessageTypes(), Does.Contain(typeof(TestMessageA)));
      ((IDisposable)registry).Dispose();
      Assert.That(router.GetRunners(), Is.Empty);
      Assert.That(router.GetSubscribedMessageTypes(), Is.Empty);
      Assert.That(contexts.GetScopeStateOrNull(EGraphContextScope.World), Is.Not.Null);
    }

    [Test]
    public void RuleSubscription_RequiresTypeAndDomainMatch()
    {
      var calls = 0;
      AddExecutor("Act", () => calls++);
      var runner = AddRule("filtered", 0, "BeforeDomainGraphs", "Act");
      ((RuntimeGraphDefinition)runner.Definition).Subscriptions[0].DomainFilter = "selected";
      router.RouteAsync(new TestMessageB(), "selected").GetAwaiter().GetResult();
      router.RouteAsync(new TestMessageA(), "other").GetAwaiter().GetResult();
      Assert.That(calls, Is.Zero);
      router.RouteAsync(new TestMessageA(), "selected").GetAwaiter().GetResult();
      Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void RuleWorldCounter_SurvivesRealEs3RoundTripAndScopeRestore()
    {
      AddExecutor("Act", () => {});
      var runner = AddRule("counter", 0, "BeforeDomainGraphs", "Act");
      runner.ImportState(new GraphStateSnapshot { GraphId = runner.GraphId,
        Data = new Dictionary<string, object> { { "dungeon_entries", 3 } } });
      var file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
        "../Temp/automation-scratch/vg-runtime-rules/world-counter.es3"));
      System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
      try
      {
        var world = contexts.GetScopeStateOrNull(EGraphContextScope.World);
        ES3.Save("world_scope_state", new GraphStateSnapshot { GraphId = "world",
          Data = new Dictionary<string, object>(world.AsReadOnly()) }, file);
        var restored = ES3.Load<GraphStateSnapshot>("world_scope_state", file);
        var freshContexts = new GraphContextRegistry(new RecordingEmitter());
        freshContexts.RestoreScopeState(EGraphContextScope.World, restored.Data);
        Assert.That(freshContexts.GetOrCreate(EGraphContextScope.World, null).Variables.Get<int>("dungeon_entries"), Is.EqualTo(3));
      }
      finally { if (ES3.FileExists(file)) ES3.DeleteFile(file); }
    }

    [Test]
    public void Controller_SaveRulesOnlyInCanonicalWorldSnapshot()
    {
      AddExecutor("Act", () => {});
      var rule = AddRule("persistent", 0, "BeforeDomainGraphs", "Act");
      rule.ImportState(new GraphStateSnapshot { GraphId = rule.GraphId,
        Data = new Dictionary<string, object> { { "counter", 4 } } });
      var es3 = new MemoryES3Service();
      new GraphStateSaveController(router, es3, contextRegistry: contexts).SaveAsync().GetAwaiter().GetResult();
      var savedGraphs = es3.LoadAsync<Dictionary<string, GraphStateSnapshot>>("graphs_graph_states").GetAwaiter().GetResult();
      Assert.That(savedGraphs.ContainsKey(rule.GraphId), Is.False,
        "rule runners share World scope, which must have only one canonical saved snapshot");
      var savedWorld = es3.LoadAsync<GraphStateSnapshot>("graphs_world_scope_state").GetAwaiter().GetResult();
      Assert.That(savedWorld.Data["counter"], Is.EqualTo(4));
    }

    [Test]
    public void Controller_LoadIgnoresLegacyRuleSnapshotsAfterWorldSchemaSanitization()
    {
      using var sink = new SerilogSinkScope();
      var declarations = Track(ScriptableObject.CreateInstance<GraphVariableSet>());
      declarations.entries.Add(new GraphVariableDeclaration { key = "counter", type = EGraphVariableType.Int, intValue = 7 });
      contexts.SetScopeDeclarations(EGraphContextScope.World, declarations);
      AddExecutor("Act", () => {});
      var rule = AddRule("persistent", 0, "BeforeDomainGraphs", "Act");
      var ordinary = new FakeGraphRunner("ordinary");
      router.RegisterRunner(ordinary);
      var es3 = new MemoryES3Service();
      es3.SaveAsync("graphs_world_scope_state", new GraphStateSnapshot { GraphId = "world",
        Data = new Dictionary<string, object> { { "counter", "obsolete string" } } }).GetAwaiter().GetResult();
      es3.SaveAsync("graphs_graph_states", new Dictionary<string, GraphStateSnapshot>
      {
        [rule.GraphId] = new GraphStateSnapshot { GraphId = rule.GraphId,
          Data = new Dictionary<string, object> { { "counter", "obsolete duplicate" } } },
        [ordinary.GraphId] = new GraphStateSnapshot { GraphId = ordinary.GraphId,
          Data = new Dictionary<string, object> { { "answer", 42 } } }
      }).GetAwaiter().GetResult();
      new GraphStateSaveController(router, es3, contextRegistry: contexts).LoadAsync().GetAwaiter().GetResult();
      Assert.That(contexts.GetScopeStateOrNull(EGraphContextScope.World).TryGet<int>("counter", out var restored), Is.True,
        "legacy rule graph snapshots must not overwrite sanitized canonical World state");
      Assert.That(restored, Is.EqualTo(7));
      Assert.That(ordinary.Imported.Count, Is.EqualTo(1));
      Assert.That(ordinary.Imported[0].Data["answer"], Is.EqualTo(42));
    }
  }
}
