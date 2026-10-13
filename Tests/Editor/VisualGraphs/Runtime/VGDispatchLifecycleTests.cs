using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MToolKit.Runtime.VisualGraphs.Config;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Dialogue.Messages;
using MToolKit.Runtime.VisualGraphs.Event.Definitions;
using MToolKit.Runtime.VisualGraphs.Event.Graphs;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Loading;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  public sealed class VGDispatchLifecycleTests : UnityObjectCleanup
  {
    [Test]
    public void UnregisterRunner_RemovesEveryDuplicateAndSubscription()
    {
      var router = new GraphEventRouter();
      var runner = new FakeGraphRunner("g", GraphDefBuilder.New().Subscribe(typeof(TestMessageA)).Build());
      router.RegisterRunner(runner);
      router.RegisterRunner(runner);
      var unregister = typeof(GraphEventRouter).GetMethod("UnregisterRunner");
      Assert.That(unregister, Is.Not.Null, "the router must expose ownership-safe teardown");
      unregister.Invoke(router, new object[] { runner });
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(router.GetRunners(), Is.Empty);
      Assert.That(router.GetSubscribedMessageTypes(), Is.Empty);
      Assert.That(runner.HandleMessageAsyncCallCount, Is.Zero);
    }

    [Test]
    public void UnloadGraph_UnregistersRunnerWithoutDeletingForeignQuestContext()
    {
      var router = new GraphEventRouter();
      var emitter = new RecordingEmitter();
      var contexts = new GraphContextRegistry(emitter);
      var ownedState = new InMemoryGraphState();
      contexts.GetOrCreate(EGraphContextScope.Graph, "quest", ownedState);
      var loader = new GraphLoader(Track(ScriptableObject.CreateInstance<VisualGraphRegistry>()), router,
        new NodeExecutorRegistry(), new NullServiceProvider(), emitter);
      var runner = new FakeGraphRunner("quest", GraphDefBuilder.New().Subscribe(typeof(TestMessageA)).Build());
      var loaded = (Dictionary<string, IGraphRunner>)typeof(GraphLoader)
        .GetField("loadedRunners", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
      loaded.Add("quest", runner);
      router.RegisterRunner(runner);
      loader.UnloadGraph("quest");
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(runner.HandleMessageAsyncCallCount, Is.Zero);
      Assert.That(contexts.TryGet(EGraphContextScope.Graph, "quest", out _), Is.True);
    }

    [TestCase("different", false)]
    [TestCase("hello", true)]
    [TestCase("dialogue_guid", true)]
    public void DialogueStart_GuardMatchesCanonicalNameBeforeDebugStart(string requested, bool accepted)
    {
      var definition = new RuntimeGraphDefinition { GraphId = "dialogue_guid", GraphDomain = "Dialogue" };
      var name = typeof(RuntimeGraphDefinition).GetField("DialogueName");
      Assert.That(name, Is.Not.Null, "human dialogue name must survive export/load independently of the GUID");
      name.SetValue(definition, "hello");
      definition.Nodes.Add(new RuntimeNodeDefinition { NodeId = "e", NodeType = "DialogueStartNode" });
      definition.Nodes.Add(new RuntimeNodeDefinition { NodeId = "line", NodeType = "DialogueLineNode" });
      definition.Connections.Add(new RuntimeConnectionDefinition { FromNodeId = "e", ToNodeId = "line" });
      var harness = new GraphRunnerHarness(definition);
      using var debug = new DebugEventRecorder();
      harness.Run(new DialogueStartMessage(requested));
      Assert.That(debug.GraphExecution.Any(e => e.isStarting), Is.EqualTo(accepted));
      Assert.That(harness.State.Contains("Dialogue.NextNodeIds"), Is.EqualTo(accepted));
    }

    [Test]
    public void DialogueStart_EventGraphStillObservesAllDialogueStarts()
    {
      var harness = new GraphRunnerHarness(GraphDefBuilder.New().Domain("Event")
        .EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var executor = harness.RegisterExecutor("Act");
      harness.Run(new DialogueStartMessage("anything"));
      Assert.That(executor.ExecuteCallCount, Is.EqualTo(1));
    }

    [Test]
    public void ExecutorFailure_RaisesDirectFailureWithGraphAndNodeIdentity()
    {
      var harness = new GraphRunnerHarness(GraphDefBuilder.New().Id("g")
        .EntryNode("e").Node("boom", "Boom").Connect("e", "boom").Build());
      harness.RegisterExecutor("Boom").ShouldThrow = true;
      var failure = typeof(GraphRunner).GetEvent("NodeExecutionFailed");
      Assert.That(failure, Is.Not.Null);
      string graphId = null, nodeId = null;
      Exception exception = null;
      Action<string, string, Exception> handler = (g, n, ex) => { graphId = g; nodeId = n; exception = ex; };
      failure.AddEventHandler(harness.Runner, handler);
      harness.Run(new TestMessageA());
      Assert.That(graphId, Is.EqualTo("g"));
      Assert.That(nodeId, Is.EqualTo("boom"));
      Assert.That(exception, Is.TypeOf<InvalidOperationException>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EventContext_BindsLiveStateAndDeclarations_AndUnloadPreservesReplacement(bool replaced)
    {
      var router = new GraphEventRouter();
      var emitter = new RecordingEmitter();
      var contexts = new GraphContextRegistry(emitter);
      var asset = Track(ScriptableObject.CreateInstance<EventGraphAsset>());
      Track(asset.AddNode<TestEntryNode>());
      var declarations = Track(ScriptableObject.CreateInstance<GraphVariableSet>());
      declarations.entries.Add(new GraphVariableDeclaration { key = "count", type = EGraphVariableType.Int, intValue = 7 });
      asset.DeclaredVariables = declarations;
      var definition = Track(ScriptableObject.CreateInstance<EventDefinition>());
      definition.Guid = "lifecycle_event";
      definition.GraphAsset = asset;
      var loader = new GraphLoader(Track(ScriptableObject.CreateInstance<VisualGraphRegistry>()), router,
        new NodeExecutorRegistry(), new NullServiceProvider(), emitter, contexts);
      // Exercise the synchronous creation leg without requiring an end-of-frame player yield in EditMode.
      var create = typeof(GraphLoader).GetMethod("CreateEventGraphRunner", BindingFlags.Instance | BindingFlags.NonPublic);
      var runner = (GraphRunner)create.Invoke(loader, new object[] { definition });
      Assert.That(contexts.TryGet(EGraphContextScope.Graph, definition.EventId, out var context), Is.True);
      Assert.That(context.Variables.Get<int>("count"), Is.EqualTo(7));
      runner.ImportState(new GraphStateSnapshot { GraphId = definition.EventId,
        Data = new Dictionary<string, object> { { "count", 9 } } });
      Assert.That(context.Variables.Get<int>("count"), Is.EqualTo(9), "runner and scoped accessor share live state");
      var loaded = (Dictionary<string, IGraphRunner>)typeof(GraphLoader)
        .GetField("loadedRunners", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
      loaded.Add(definition.EventId, runner);
      router.RegisterRunner(runner);
      IGraphContext replacement = null;
      if (replaced)
      {
        contexts.Remove(definition.EventId);
        replacement = contexts.GetOrCreate(EGraphContextScope.Graph, definition.EventId, new InMemoryGraphState());
      }
      loader.UnloadGraph(definition.EventId);
      Assert.That(router.GetRunners(), Is.Empty);
      Assert.That(contexts.TryGet(EGraphContextScope.Graph, definition.EventId, out var retained), Is.EqualTo(replaced));
      if (replaced) Assert.That(retained, Is.SameAs(replacement));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void QuestTeardown_UnregistersOwnedRunner(bool disposeManager)
    {
      using var harness = new QuestManagerHarness();
      var manager = harness.Manager;
      var router = (GraphEventRouter)typeof(QuestManager).GetField("eventRouter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
      var runner = new FakeGraphRunner("q_runner", GraphDefBuilder.New().Subscribe(typeof(TestMessageA)).Build());
      var loaded = (Dictionary<string, IGraphRunner>)typeof(QuestManager)
        .GetField("loadedRunners", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
      loaded.Add("q_runner", runner);
      router.RegisterRunner(runner);
      if (disposeManager) manager.Dispose();
      else
      {
        var state = new QuestRuntimeState("q");
        state.LoadedGraphInstanceIds.Add("q_runner");
        typeof(QuestManager).GetMethod("UnloadQuestGraphs", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(manager, new object[] { state });
        Assert.That(state.LoadedGraphInstanceIds, Is.Empty);
      }
      router.RouteAsync(new TestMessageA()).GetAwaiter().GetResult();
      Assert.That(runner.HandleMessageAsyncCallCount, Is.Zero);
      Assert.That(loaded, Is.Empty);
    }
  }
}
