using System;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Contexts
{
  /// <summary>
  ///   Pins <see cref="GraphContextRegistry" />: Player/World lazy singleton identity with silent owner
  ///   normalization, the Graph create/return/throw matrix and the Remove re-create path, load-order-tolerant
  ///   SetScopeDeclarations, the never-re-wrap debug-event contract, the persistence seam, and Emit delegation.
  ///   Warning/error paths are asserted via a real Serilog sink.
  /// </summary>
  [TestFixture]
  public sealed class GraphContextRegistryTests : UnityObjectCleanup
  {
    private RecordingEmitter emitter;
    private GraphContextRegistry registry;

    [SetUp]
    public void SetUp()
    {
      LogAssert.ignoreFailingMessages = true;
      NodeDebugEvents.ClearAllSubscribers();
      emitter = new RecordingEmitter();
      registry = new GraphContextRegistry(emitter);
    }

    [TearDown]
    public void TearDownEvents()
    {
      NodeDebugEvents.ClearAllSubscribers();
    }

    private GraphVariableSet DeclSet(params GraphVariableDeclaration[] decls)
    {
      var set = Track(ScriptableObject.CreateInstance<GraphVariableSet>());
      set.entries.AddRange(decls);
      return set;
    }

    private static GraphVariableDeclaration IntDecl(string key, int value) =>
      new() { key = key, type = EGraphVariableType.Int, intValue = value };

    // ---- Singleton identity + normalization ----

    [Test]
    public void GetOrCreate_PlayerTwice_ReturnsSameInstance()
    {
      var a = registry.GetOrCreate(EGraphContextScope.Player, null);
      var b = registry.GetOrCreate(EGraphContextScope.Player, null);
      Assert.That(a, Is.SameAs(b));
    }

    [Test]
    public void GetOrCreate_WorldTwice_ReturnsSameInstance()
    {
      var a = registry.GetOrCreate(EGraphContextScope.World, null);
      var b = registry.GetOrCreate(EGraphContextScope.World, null);
      Assert.That(a, Is.SameAs(b));
    }

    [Test]
    public void GetOrCreate_PlayerAndWorldWithNullOwnerId_SucceedWithoutWarning()
    {
      using var sink = new SerilogSinkScope();
      Assert.DoesNotThrow(() => registry.GetOrCreate(EGraphContextScope.Player, null));
      Assert.DoesNotThrow(() => registry.GetOrCreate(EGraphContextScope.World, null));
      Assert.That(sink.Warnings, Is.Empty, "null owner is the normal call shape for the singleton scopes");
    }

    [Test]
    public void GetOrCreate_PlayerWithForeignOwnerId_NormalizesToPlayerConstant()
    {
      using var sink = new SerilogSinkScope();
      var ctx = registry.GetOrCreate(EGraphContextScope.Player, "weird");
      Assert.That(ctx.OwnerId, Is.EqualTo(GraphContextRegistry.PlayerOwnerId));
      Assert.That(sink.ContainsWarning("normalizing"), Is.True);
    }

    [Test]
    public void GetOrCreate_PlayerWithStateArg_ThrowsArgumentException()
    {
      Assert.Throws<ArgumentException>(() =>
        registry.GetOrCreate(EGraphContextScope.Player, null, new InMemoryGraphState()));
    }

    [Test]
    public void TryGet_PlayerBeforeFirstGetOrCreate_ReturnsFalse()
    {
      Assert.That(registry.TryGet(EGraphContextScope.Player, null, out _), Is.False);
    }

    [Test]
    public void TryGet_PlayerWithNullOrForeignOwnerAfterCreation_NormalizesAndReturnsTrueWithoutWarning()
    {
      var created = registry.GetOrCreate(EGraphContextScope.Player, null);

      using var sink = new SerilogSinkScope();
      Assert.That(registry.TryGet(EGraphContextScope.Player, null, out var byNull), Is.True);
      Assert.That(registry.TryGet(EGraphContextScope.Player, "someone", out var byForeign), Is.True);
      Assert.That(byNull, Is.SameAs(created));
      Assert.That(byForeign, Is.SameAs(created));
      Assert.That(sink.Warnings, Is.Empty, "lookup normalization stays noise-free");
    }

    // ---- Graph create/return/throw matrix ----

    [Test]
    public void GetOrCreate_GraphNullOwnerId_ThrowsArgumentNullException()
    {
      Assert.Throws<ArgumentNullException>(() =>
        registry.GetOrCreate(EGraphContextScope.Graph, null, new InMemoryGraphState()));
    }

    [Test]
    public void GetOrCreate_GraphEmptyOwnerId_ThrowsArgumentException()
    {
      Assert.Throws<ArgumentException>(() =>
        registry.GetOrCreate(EGraphContextScope.Graph, "  ", new InMemoryGraphState()));
    }

    [Test]
    public void GetOrCreate_GraphFirstCallNullState_ThrowsArgumentNullException()
    {
      Assert.Throws<ArgumentNullException>(() =>
        registry.GetOrCreate(EGraphContextScope.Graph, "q-1", null));
    }

    [Test]
    public void GetOrCreate_GraphSameOwnerSameOrNullState_ReturnsExisting()
    {
      var state = new InMemoryGraphState();
      var first = registry.GetOrCreate(EGraphContextScope.Graph, "q-1", state);
      var sameState = registry.GetOrCreate(EGraphContextScope.Graph, "q-1", state);
      var nullState = registry.GetOrCreate(EGraphContextScope.Graph, "q-1");

      Assert.That(sameState, Is.SameAs(first));
      Assert.That(nullState, Is.SameAs(first));
    }

    [Test]
    public void GetOrCreate_GraphSameOwnerDifferentState_ThrowsInvalidOperationException()
    {
      registry.GetOrCreate(EGraphContextScope.Graph, "q-1", new InMemoryGraphState());
      Assert.Throws<InvalidOperationException>(() =>
        registry.GetOrCreate(EGraphContextScope.Graph, "q-1", new InMemoryGraphState()));
    }

    // ---- Remove ----

    [Test]
    public void Remove_ThenGetOrCreateWithNewState_CreatesFreshContext()
    {
      var first = registry.GetOrCreate(EGraphContextScope.Graph, "q-1", new InMemoryGraphState());
      Assert.That(registry.Remove("q-1"), Is.True);
      var second = registry.GetOrCreate(EGraphContextScope.Graph, "q-1", new InMemoryGraphState());

      Assert.That(second, Is.Not.SameAs(first), "the legitimate re-create path");
    }

    [Test]
    public void Remove_ReservedScopeConstant_ThrowsArgumentException()
    {
      Assert.Throws<ArgumentException>(() => registry.Remove(GraphContextRegistry.PlayerOwnerId));
      Assert.Throws<ArgumentException>(() => registry.Remove(GraphContextRegistry.WorldOwnerId));
    }

    [Test]
    public void Remove_AbsentOwner_ReturnsFalse()
    {
      Assert.That(registry.Remove("nope"), Is.False);
    }

    // ---- SetScopeDeclarations ----

    [Test]
    public void SetScopeDeclarations_AfterContextCreated_IgnoredWithoutThrow()
    {
      registry.GetOrCreate(EGraphContextScope.Player, null);

      using var sink = new SerilogSinkScope();
      Assert.DoesNotThrow(() =>
        registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("gold", 5))));
      Assert.That(sink.ContainsError("already created"), Is.True);
    }

    [Test]
    public void SetScopeDeclarations_GraphScope_ThrowsArgumentException()
    {
      Assert.Throws<ArgumentException>(() =>
        registry.SetScopeDeclarations(EGraphContextScope.Graph, DeclSet()));
    }

    // ---- Debug-event wrapping ----

    [Test]
    public void PlayerContextSet_RaisesStateChangedDebugEvent()
    {
      var player = registry.GetOrCreate(EGraphContextScope.Player, null);

      using var recorder = new StateChangeRecorder();
      player.Variables.Set("gold", 7);

      Assert.That(recorder.Changes, Has.Count.EqualTo(1),
        "the registry-created Player state is DebuggableGraphState-wrapped");
      Assert.That(recorder.Changes[0].graphId, Is.EqualTo(GraphContextRegistry.PlayerOwnerId));
      Assert.That(recorder.Changes[0].key, Is.EqualTo("gold"));
    }

    [Test]
    public void GraphContextSet_DoesNotDoubleEmit_WhenCallerStateAlreadyDebuggable()
    {
      var debuggable = new DebuggableGraphState(new InMemoryGraphState(), "q-dbg");
      var ctx = registry.GetOrCreate(EGraphContextScope.Graph, "q-dbg", debuggable);

      using var recorder = new StateChangeRecorder();
      ctx.Variables.Set("x", 1);

      Assert.That(recorder.Changes, Has.Count.EqualTo(1),
        "the context must not re-wrap an already-debuggable caller state (would double-emit)");
    }

    // ---- Persistence seam ----

    [Test]
    public void GetScopeStateOrNull_BeforeAndAfterCreation_NullThenState()
    {
      Assert.That(registry.GetScopeStateOrNull(EGraphContextScope.Player), Is.Null);
      registry.GetOrCreate(EGraphContextScope.Player, null);
      Assert.That(registry.GetScopeStateOrNull(EGraphContextScope.Player), Is.Not.Null);
    }

    [Test]
    public void GetScopeStateOrNull_GraphScope_ThrowsArgumentException()
    {
      Assert.Throws<ArgumentException>(() => registry.GetScopeStateOrNull(EGraphContextScope.Graph));
    }

    // ---- Emit delegation ----

    [Test]
    public void Context_Emit_DelegatesToInjectedEmitter()
    {
      var player = registry.GetOrCreate(EGraphContextScope.Player, null);
      var msg = new TestMessageA();

      player.Emit(msg, "dom");
      Assert.That(emitter.Emitted, Has.Count.EqualTo(1));
      Assert.That(emitter.Emitted[0].message, Is.SameAs(msg));
      Assert.That(emitter.Emitted[0].domain, Is.EqualTo("dom"));

      using var sink = new SerilogSinkScope();
      player.Emit(null);
      Assert.That(emitter.Emitted, Has.Count.EqualTo(1), "a null message is not emitted");
      Assert.That(sink.ContainsError("null message"), Is.True);
    }
  }
}
