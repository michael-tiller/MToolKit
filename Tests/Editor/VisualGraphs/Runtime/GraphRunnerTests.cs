using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MToolKit.Runtime.VisualGraphs.Dialogue.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  /// <summary>
  ///   Characterization of <see cref="GraphRunner" /> — the largest refactor surface. Pins the execution
  ///   model the Phase 9 work must not silently change: entry-node discovery + auto fan-out, executor-driven
  ///   continuation, the per-DISPATCH reentrancy guard (and that a second identical dispatch re-executes —
  ///   there is NO sequence-id dedup yet), the exact max-steps dequeue boundary, error isolation, the
  ///   dialogue pause/continue protocol, and ExportState/ImportState. Runner code logs via Serilog (not
  ///   Unity Debug) so error-path tests assert observable behavior, never LogAssert.
  /// </summary>
  [TestFixture]
  public sealed class GraphRunnerTests : UnityObjectCleanup
  {
    [SetUp]
    public void SetUp()
    {
      LogAssert.ignoreFailingMessages = true;
      NodeDebugEvents.ClearAllSubscribers();
    }

    [TearDown]
    public void TearDownRunner()
    {
      NodeDebugEvents.ClearAllSubscribers();
    }

    // ---- Constructor null-guards ---------------------------------------------------------------

    [Test]
    public void Constructor_NullArguments_Throw()
    {
      var def = GraphDefBuilder.New().Build();
      var state = new InMemoryGraphState();
      var execs = new NodeExecutorRegistry();
      var services = new NullServiceProvider();
      var emitter = new RecordingEmitter();

      Assert.Throws<ArgumentNullException>(() => new GraphRunner(null, state, execs, services, emitter));
      Assert.Throws<ArgumentNullException>(() => new GraphRunner(def, null, execs, services, emitter));
      Assert.Throws<ArgumentNullException>(() => new GraphRunner(def, state, null, services, emitter));
      Assert.Throws<ArgumentNullException>(() => new GraphRunner(def, state, execs, null, emitter));
      Assert.Throws<ArgumentNullException>(() => new GraphRunner(def, state, execs, services, null));
    }

    // ---- CanHandle -----------------------------------------------------------------------------

    [Test]
    public void CanHandle_ExactTypeAndDomain_True()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Subscribe(typeof(TestMessageA), "Quest").Build());
      Assert.That(h.Runner.CanHandle(typeof(TestMessageA), "Quest"), Is.True);
    }

    [Test]
    public void CanHandle_EmptyDomainFilter_MatchesAnyRequestedDomain()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Subscribe(typeof(TestMessageA), null).Build());
      Assert.That(h.Runner.CanHandle(typeof(TestMessageA), "AnyDomain"), Is.True);
    }

    [Test]
    public void CanHandle_DomainMismatch_False()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Subscribe(typeof(TestMessageA), "Quest").Build());
      Assert.That(h.Runner.CanHandle(typeof(TestMessageA), "Dialogue"), Is.False);
    }

    [Test]
    public void CanHandle_NullType_False()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Subscribe(typeof(TestMessageA), "Quest").Build());
      Assert.That(h.Runner.CanHandle(null), Is.False);
    }

    [Test]
    public void CanHandle_DialogueContinue_DialogueDomain_True_OtherwiseFalse()
    {
      var dialogue = new GraphRunnerHarness(GraphDefBuilder.New().Domain("Dialogue").Build());
      var quest = new GraphRunnerHarness(GraphDefBuilder.New().Domain("Quest").Build());

      Assert.That(dialogue.Runner.CanHandle(typeof(DialogueContinueMessage)), Is.True);
      Assert.That(quest.Runner.CanHandle(typeof(DialogueContinueMessage)), Is.False);
    }

    // ---- Execution flow ------------------------------------------------------------------------

    [Test]
    public void HandleMessage_EntryNode_AutoEnqueuesConnections_SameMessageReachesExecutor()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var act = h.RegisterExecutor("Act");

      var message = new TestMessageA();
      h.Run(message);

      Assert.That(act.ExecutedNodeIds, Is.EqualTo(new[] { "n" }));
      Assert.That(act.ExecutedMessages.Single(), Is.SameAs(message),
        "the same message instance is threaded through to the executor");
    }

    [Test]
    public void HandleMessage_AllEntryNodesFire_RegardlessOfSubscriptions()
    {
      // No subscriptions declared — HandleMessageAsync enqueues EVERY entry node anyway.
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e1").EntryNode("e2")
        .Node("n1", "Act").Node("n2", "Act")
        .Connect("e1", "n1").Connect("e2", "n2").Build());
      var act = h.RegisterExecutor("Act");

      h.Run(new TestMessageA());

      Assert.That(act.ExecutedNodeIds, Is.EquivalentTo(new[] { "n1", "n2" }));
    }

    [Test]
    public void HandleMessage_FeedbackEdge_NodeExecutesOncePerDispatch()
    {
      // e -> a, then a enqueues b, b enqueues a (feedback). The per-dispatch guard runs 'a' only once.
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e").Node("a", "Step").Node("b", "Step").Connect("e", "a").Build());
      var step = h.RegisterExecutor("Step");
      step.OnExecute = (node, ctx) =>
      {
        if (node.NodeId == "a") ctx.EnqueueNext("b");
        else if (node.NodeId == "b") ctx.EnqueueNext("a");
      };

      h.Run(new TestMessageA());

      Assert.That(step.ExecutedNodeIds, Is.EqualTo(new[] { "a", "b" }),
        "'a' is enqueued again by 'b' but the per-dispatch guard skips its second execution");
    }

    [Test]
    public void HandleMessage_SecondIdenticalDispatch_ReExecutes_NoSequenceIdDedup()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var act = h.RegisterExecutor("Act");

      var message = new TestMessageA();
      h.Run(message);
      h.Run(message);

      Assert.That(act.ExecuteCallCount, Is.EqualTo(2),
        "the reentrancy guard is per-dispatch only — there is no cross-message sequence-id dedup, so a " +
        "repeated message re-executes (the behavior Phase 9 dedup will deliberately change)");
    }

    [Test]
    public void HandleMessage_MaxSteps1_DequeuesOnlyEntry_RunsZeroExecutors()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .MaxSteps(1).EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var act = h.RegisterExecutor("Act");

      h.Run(new TestMessageA());

      Assert.That(act.ExecuteCallCount, Is.EqualTo(0),
        "MaxExecutionSteps=1 allows exactly one dequeue (the entry node); the action node halts before executing");
    }

    [Test]
    public void HandleMessage_MaxSteps2_AllowsFirstActionExecutor()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .MaxSteps(2).EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var act = h.RegisterExecutor("Act");

      h.Run(new TestMessageA());

      Assert.That(act.ExecuteCallCount, Is.EqualTo(1),
        "MaxExecutionSteps=2 allows the entry dequeue plus the first action executor");
    }

    [Test]
    public void HandleMessage_MissingExecutor_LogsAndSiblingBranchContinues()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e1").EntryNode("e2")
        .Node("missing", "Unregistered").Node("ok", "Ok")
        .Connect("e1", "missing").Connect("e2", "ok").Build());
      var ok = h.RegisterExecutor("Ok"); // "Unregistered" deliberately has no executor

      h.Run(new TestMessageA());

      Assert.That(ok.Executed, Is.True,
        "a missing executor on one branch is swallowed (KeyNotFoundException caught) and the sibling branch still runs");
    }

    [Test]
    public void HandleMessage_ExecutorThrows_SwallowedAndSiblingBranchContinues()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e1").EntryNode("e2")
        .Node("boom", "Boom").Node("ok", "Ok")
        .Connect("e1", "boom").Connect("e2", "ok").Build());
      var boom = h.RegisterExecutor("Boom");
      boom.ShouldThrow = true;
      var ok = h.RegisterExecutor("Ok");

      h.Run(new TestMessageA());

      Assert.That(boom.Executed, Is.True, "the throwing executor did run");
      Assert.That(ok.Executed, Is.True, "its exception is caught and the sibling branch still runs");
    }

    [Test]
    public void HandleMessage_ConnectionToUnknownNode_Skipped_ValidSiblingRuns()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e").Node("ok", "Ok")
        .Connect("e", "ghost").Connect("e", "ok").Build());
      var ok = h.RegisterExecutor("Ok");

      h.Run(new TestMessageA());

      Assert.That(ok.Executed, Is.True,
        "a connection to a non-existent node id is skipped (GetNodeById null) without aborting the dispatch");
    }

    [Test]
    public void HandleMessage_NullMessage_NoExecution()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var act = h.RegisterExecutor("Act");

      h.Run(null);

      Assert.That(act.ExecuteCallCount, Is.EqualTo(0));
    }

    [Test]
    public void HandleMessage_PreCancelledToken_RaisesStart_ButRunsNoNode()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e").Node("n", "Act").Connect("e", "n").Build());
      var act = h.RegisterExecutor("Act");

      using var recorder = new DebugEventRecorder();
      var cts = new CancellationTokenSource();
      cts.Cancel();
      h.Run(new TestMessageA(), null, cts.Token);

      Assert.That(act.ExecuteCallCount, Is.EqualTo(0), "no node executes under a pre-cancelled token");
      Assert.That(recorder.GraphExecution.Any(e => e.isStarting), Is.True,
        "the graph-start debug event still fires before the dequeue loop breaks — not 'nothing happened'");
    }

    // ---- Dialogue protocol ---------------------------------------------------------------------

    [Test]
    public void Dialogue_EntryNode_StoresNextNodeIds_AndPauses()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Domain("Dialogue")
        .Node("start", "DialogueStartNode").Node("line", "DialogueLineNode")
        .Connect("start", "line").Build());
      var lineExec = h.RegisterExecutor("DialogueLineNode");

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<List<string>>("Dialogue.NextNodeIds", out var stored), Is.True);
      Assert.That(stored, Does.Contain("line"));
      Assert.That(lineExec.ExecuteCallCount, Is.EqualTo(0),
        "the dialogue entry stores its successors in state and pauses — the line node does not execute yet");
    }

    [Test]
    public void Dialogue_Continue_ExecutesStoredNodes_AndClearsState()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Domain("Dialogue")
        .Node("line", "DialogueLineNode").Build());
      var lineExec = h.RegisterExecutor("DialogueLineNode");
      h.State.Set("Dialogue.NextNodeIds", new List<string> { "line" });

      h.Run(new DialogueContinueMessage("g1"));

      Assert.That(lineExec.ExecutedNodeIds, Is.EqualTo(new[] { "line" }));
      Assert.That(h.State.TryGet<List<string>>("Dialogue.NextNodeIds", out var cleared), Is.True);
      Assert.That(cleared, Is.Empty, "the stored successor list is reset after continuation");
    }

    [Test]
    public void Dialogue_Continue_GraphIdMismatch_Ignored()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Domain("Dialogue")
        .Node("line", "DialogueLineNode").Build());
      var lineExec = h.RegisterExecutor("DialogueLineNode");
      h.State.Set("Dialogue.NextNodeIds", new List<string> { "line" });

      h.Run(new DialogueContinueMessage("other-graph"));

      Assert.That(lineExec.ExecuteCallCount, Is.EqualTo(0),
        "a DialogueContinueMessage for a different graph id is ignored");
    }

    [Test]
    public void Dialogue_Continue_NoStoredNodes_EmitsCloseMessage()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Domain("Dialogue").Build());

      h.Run(new DialogueContinueMessage("g1"));

      Assert.That(h.Emitter.Emitted.Any(e => e.message is DialogueProgressMessage), Is.True,
        "with no stored successors, continuation emits a DialogueProgressMessage to close the dialogue");
    }

    // ---- ExportState / ImportState -------------------------------------------------------------

    [Test]
    public void ExportState_FiltersScriptableObjectValues()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());
      var so = Track(ScriptableObject.CreateInstance<ScriptableObject>());
      h.State.Set("so", so);
      h.State.Set("k", 42);

      var snapshot = h.Runner.ExportState();

      Assert.That(snapshot.GraphId, Is.EqualTo("g1"));
      Assert.That(snapshot.Data.ContainsKey("k"), Is.True);
      Assert.That(snapshot.Data.ContainsKey("so"), Is.False,
        "ScriptableObject references are filtered out of the exported snapshot (they are GUID-resolved at load)");
    }

    [Test]
    public void ExportState_LastSequenceId_IsAlwaysDefaultZero()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());
      h.State.Set("k", 1);

      Assert.That(h.Runner.ExportState().LastSequenceId, Is.EqualTo(0L),
        "ExportState never populates LastSequenceId — the field exists but sequence-id dedup is unimplemented (Phase 9)");
    }

    [Test]
    public void ImportState_NullSnapshot_Ignored()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());

      h.Runner.ImportState(null);

      Assert.That(h.State.AsReadOnly(), Is.Empty);
    }

    [Test]
    public void ImportState_GraphIdMismatch_Ignored()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());

      h.Runner.ImportState(new GraphStateSnapshot
      {
        GraphId = "other",
        Data = new Dictionary<string, object> { { "k", 1 } }
      });

      Assert.That(h.State.Contains("k"), Is.False);
    }

    [Test]
    public void ImportState_MatchingSnapshot_OverwritesKeys_SaveWins()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());
      h.State.Set("k", "old");

      h.Runner.ImportState(new GraphStateSnapshot
      {
        GraphId = "g1",
        Data = new Dictionary<string, object> { { "k", "new" } }
      });

      Assert.That(h.State.TryGet<string>("k", out var value), Is.True);
      Assert.That(value, Is.EqualTo("new"), "the restored save value wins over the existing in-memory value");
    }

    [Test]
    public void ImportState_MatchingSnapshotWithNullData_IsNoOp()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());
      h.State.Set("k", "existing");

      // 9.0.4 added the guard the old pin anticipated: a matching snapshot whose Data is null (only possible
      // for hand/ES3-constructed snapshots — ExportState always sets Data) imports nothing, consistent with
      // the null-snapshot and wrong-GraphId guards. Existing state is untouched.
      Assert.DoesNotThrow(() =>
        h.Runner.ImportState(new GraphStateSnapshot { GraphId = "g1", Data = null }));
      Assert.That(h.State.TryGet<string>("k", out var value), Is.True);
      Assert.That(value, Is.EqualTo("existing"), "a null-Data import must not disturb existing state");
    }
  }
}
