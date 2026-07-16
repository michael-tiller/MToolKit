using System.Collections.Generic;
using System.Threading;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Persistence
{
  /// <summary>
  ///   9.0.4 schema-change behaviors — what happens when declarations change between a save and a load. The
  ///   four specified behaviors plus the merge-semantics corner (import MERGES into existing state, so a
  ///   discarded mismatch must OVERWRITE a pre-initialized value with the declared default), shared-state
  ///   order-independence (quest + objective runners share one state and must sanitize against ONE aggregate
  ///   schema), and the QuestManager aggregate wiring (scoped reads see the aggregate's declared defaults on
  ///   both the start and completed-unclaimed-restore paths).
  /// </summary>
  [TestFixture]
  public sealed class GraphStateSchemaChangeTests : UnityObjectCleanup
  {
    [SetUp]
    public void SetUp()
    {
      LogAssert.ignoreFailingMessages = true;
    }

    private GraphVariableSet DeclSet(params GraphVariableDeclaration[] decls)
    {
      var set = Track(ScriptableObject.CreateInstance<GraphVariableSet>());
      set.entries.AddRange(decls);
      return set;
    }

    private static GraphVariableDeclaration IntDecl(string key, int value) =>
      new() { key = key, type = EGraphVariableType.Int, intValue = value };

    private static GraphVariableDeclaration StringDecl(string key, string value) =>
      new() { key = key, type = EGraphVariableType.String, stringValue = value };

    private static GraphStateSnapshot Snapshot(string graphId, string key, object value) =>
      new() { GraphId = graphId, Data = new Dictionary<string, object> { { key, value } } };

    // ---- The four specified declaration-change-vs-save behaviors --------------------------------

    [Test]
    public void NewDeclaredVariable_AbsentFromSave_DeclaredDefaultApplies()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build(), DeclSet(IntDecl("hp", 10)));
      h.Runner.ImportState(Snapshot("g1", "other", 1)); // save predates the 'hp' declaration

      var vars = new VariableStorage(h.State, DeclSet(IntDecl("hp", 10)));
      Assert.That(vars.Get("hp", -1), Is.EqualTo(10),
        "a variable declared after the save was written reads its declared default");
    }

    [Test]
    public void ChangedDefault_KeyPresentInSave_SavedValueWins()
    {
      // The save carries hp=99; the declaration's default has since changed to 10. Save wins — designers
      // must version-bump or migrate to force new defaults onto existing saves.
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build(), DeclSet(IntDecl("hp", 10)));
      h.Runner.ImportState(Snapshot("g1", "hp", 99));

      var vars = new VariableStorage(h.State, DeclSet(IntDecl("hp", 10)));
      Assert.That(vars.Get("hp", -1), Is.EqualTo(99));
    }

    [Test]
    public void RemovedDeclaration_ValueStillInSave_LoadsAsUndeclaredKey()
    {
      // Declarations no longer mention 'legacy' — the saved value still loads, readable with its saved type.
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build(), DeclSet(IntDecl("hp", 10)));
      h.Runner.ImportState(Snapshot("g1", "legacy", "old-value"));

      Assert.That(h.State.TryGet<string>("legacy", out var value), Is.True,
        "an undeclared key from the save loads untouched (legal)");
      Assert.That(value, Is.EqualTo("old-value"));
    }

    [Test]
    public void TypeChanged_SavedValueDiscarded_WarningLogged_DeclaredDefaultApplies()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build(), DeclSet(IntDecl("hp", 10)));

      using var sink = new SerilogSinkScope();
      h.Runner.ImportState(Snapshot("g1", "hp", "ninety-nine")); // saved as string, now declared Int

      Assert.That(sink.ContainsWarning("Schema mismatch on load"), Is.True,
        "the discard is loud — a warning naming the key, saved type, and declared type");
      var vars = new VariableStorage(h.State, DeclSet(IntDecl("hp", 10)));
      Assert.That(vars.Get("hp", -1), Is.EqualTo(10),
        "the mismatched saved value is discarded and the declared default applies");
    }

    [Test]
    public void TypeChanged_KeyAlreadyInitializedInState_DefaultOverwritesStaleValue()
    {
      // Import MERGES into existing state and loaders apply initial variables BEFORE restore. Discarding a
      // mismatched key by REMOVAL would leave this pre-initialized value visible; the sanitizer must instead
      // overwrite with the declared default.
      var h = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build(), DeclSet(IntDecl("hp", 10)));
      h.State.Set("hp", 55); // initial-variables leg ran before restore

      h.Runner.ImportState(Snapshot("g1", "hp", "corrupt-string"));

      Assert.That(h.State.TryGet<int>("hp", out var value), Is.True);
      Assert.That(value, Is.EqualTo(10),
        "the declared default overwrites the stale pre-existing value — not just 'the saved value vanished'");
    }

    // ---- Shared-state order-independence (the quest aggregate's reason to exist) ----------------

    [Test]
    public void SharedState_TwoRunners_TypeChangeNotReintroducedRegardlessOfImportOrder()
    {
      // Quest-level + objective runners share ONE GraphState. With per-asset declarations, a runner that
      // doesn't declare 'x' would re-import the stale value its sibling just discarded — restore order would
      // decide the outcome. With the SHARED aggregate, both sanitize identically.
      void AssertOrderIndependent(bool aFirst)
      {
        var aggregate = DeclSet(IntDecl("x", 5));
        var shared = new InMemoryGraphState();
        var emitter = new RecordingEmitter();
        var execs = new NodeExecutorRegistry();
        var services = new NullServiceProvider();
        var a = new GraphRunner(GraphDefBuilder.New().Id("A").Build(), shared, execs, services, emitter, aggregate);
        var b = new GraphRunner(GraphDefBuilder.New().Id("B").Build(), shared, execs, services, emitter, aggregate);

        var snapA = Snapshot("A", "x", "stale-string");
        var snapB = Snapshot("B", "x", "stale-string");
        if (aFirst) { a.ImportState(snapA); b.ImportState(snapB); }
        else { b.ImportState(snapB); a.ImportState(snapA); }

        Assert.That(shared.TryGet<int>("x", out var value), Is.True);
        Assert.That(value, Is.EqualTo(5),
          $"order {(aFirst ? "A→B" : "B→A")}: the type-changed value never survives into the shared state");
      }

      AssertOrderIndependent(aFirst: true);
      AssertOrderIndependent(aFirst: false);
    }

    // ---- QuestManager aggregate wiring (context + scoped reads) ---------------------------------
    //
    // These drive the REAL QuestManager paths. Graph loads await WaitForEndOfFrame, which a sync EditMode
    // test cannot pump — so they pass a pre-cancelled token: every per-graph load aborts synchronously into
    // its per-objective catch, while the aggregate build + context attach (which happen BEFORE any load)
    // complete normally. The assertions only concern the attach-time schema.

    private QuestGraphAsset GraphAssetWithDeclarations(params GraphVariableDeclaration[] decls)
    {
      var asset = Track(ScriptableObject.CreateInstance<QuestGraphAsset>());
      asset.DeclaredVariables = DeclSet(decls);
      return asset;
    }

    [Test]
    public void QuestScopedRead_MissingDeclaredKey_GetsAggregateDeclaredDefault()
    {
      using var harness = new QuestManagerHarness();

      var objective = Track(ScriptableObject.CreateInstance<QuestObjective>());
      objective.Guid = "o1";
      objective.DisplayName = "o1";
      objective.RequiredProgress = 1;
      objective.ObjectiveGraph = GraphAssetWithDeclarations(IntDecl("kills_needed", 7));

      var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
      quest.Guid = "q1";
      quest.DisplayName = "q1";
      quest.Objectives = new List<QuestObjective> { objective };

      var started = harness.Manager.StartQuestAsync(quest, new CancellationToken(true)).GetAwaiter().GetResult();
      Assert.That(started, Is.True, "quest starts; the cancelled token only aborts the graph loads");

      var resolver = new ScopedKeyResolver(harness.ContextRegistry);
      var local = harness.ContextRegistry.GetOrCreate(EGraphContextScope.Player, null);
      Assert.That(resolver.Get("quest:q1.kills_needed", local, -1), Is.EqualTo(7),
        "an unset-but-aggregate-declared quest key resolves to the aggregate's declared default — the same schema import sanitization uses");
    }

    [Test]
    public void CompletedUnclaimedRestore_ContextGetsAggregateDeclarations()
    {
      using var harness = new QuestManagerHarness();

      var quest = Track(ScriptableObject.CreateInstance<QuestDefinition>());
      quest.Guid = "q2";
      quest.DisplayName = "q2";
      quest.GraphAsset = GraphAssetWithDeclarations(StringDecl("reward", "gold"));
      quest.Objectives = new List<QuestObjective>();
      GraphDefinitionRegistry.RegisterQuestDefinition(quest);

      var saveData = new QuestManagerSaveData
      {
        CompletedUnclaimedQuests = { new ActiveQuestSaveData { QuestGuid = "q2", SerializedGraphState = "" } }
      };
      harness.Manager.RestoreSaveDataAsync(saveData, null, new CancellationToken(true)).GetAwaiter().GetResult();

      Assert.That(harness.Manager.IsQuestCompleted("q2"), Is.True,
        "the completed-unclaimed quest restored despite the aborted graph loads");

      var resolver = new ScopedKeyResolver(harness.ContextRegistry);
      var local = harness.ContextRegistry.GetOrCreate(EGraphContextScope.Player, null);
      Assert.That(resolver.Get("quest:q2.reward", local, "fallback"), Is.EqualTo("gold"),
        "the restore path that BYPASSES StartQuestAsync still attaches the aggregate to the quest context");
    }
  }
}
