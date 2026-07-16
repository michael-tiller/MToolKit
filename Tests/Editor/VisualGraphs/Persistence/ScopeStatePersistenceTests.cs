using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Persistence;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Persistence
{
  /// <summary>
  ///   9.0.4 Player/World scope persistence through <see cref="GraphStateSaveController" /> + a fresh
  ///   <see cref="GraphContextRegistry" /> per save/load side (wire fidelity is pinned separately by
  ///   <see cref="ES3SnapshotFileRoundTripTests" />, so a <see cref="MemoryES3Service" /> suffices here).
  ///   Covers: full-type-table round-trips per scope, stale-key deletion when a scope was never created,
  ///   snapshot-identity validation, schema-mismatch sanitization on scope restore, and the
  ///   declaration-timing contract (restore-before-declarations loads undeclared; late declarations are
  ///   rejected loudly by the registry).
  /// </summary>
  [TestFixture]
  public sealed class ScopeStatePersistenceTests : UnityObjectCleanup
  {
    private const string PlayerKey = "graphs_player_scope_state";
    private const string WorldKey = "graphs_world_scope_state";

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

    private static GraphStateSaveController Controller(MemoryES3Service es3, GraphContextRegistry registry) =>
      new(new GraphEventRouter(), es3, contextRegistry: registry);

    private static void AssertScopeRoundTripsAllSevenTypes(EGraphContextScope scope)
    {
      var es3 = new MemoryES3Service();

      var saveRegistry = new GraphContextRegistry(new RecordingEmitter());
      var saveContext = saveRegistry.GetOrCreate(scope, null);
      saveContext.Variables.Set("s", "x");
      saveContext.Variables.Set("i", 7);
      saveContext.Variables.Set("f", 1.5f);
      saveContext.Variables.Set("b", true);
      saveContext.Variables.Set("v3", new Vector3(1f, 2f, 3f));
      saveContext.Variables.Set("v2", new Vector2(4f, 5f));
      saveContext.Variables.Set("c", new Color(0.25f, 0.5f, 0.75f, 1f));
      Controller(es3, saveRegistry).SaveAsync().GetAwaiter().GetResult();

      var loadRegistry = new GraphContextRegistry(new RecordingEmitter());
      Controller(es3, loadRegistry).LoadAsync().GetAwaiter().GetResult();

      var loaded = loadRegistry.GetOrCreate(scope, null);
      Assert.That(loaded.Variables.Get("s", "missing"), Is.EqualTo("x"));
      Assert.That(loaded.Variables.Get("i", -1), Is.EqualTo(7));
      Assert.That(loaded.Variables.Get("f", -1f), Is.EqualTo(1.5f));
      Assert.That(loaded.Variables.Get("b", false), Is.True);
      Assert.That(loaded.Variables.Get("v3", Vector3.zero), Is.EqualTo(new Vector3(1f, 2f, 3f)));
      Assert.That(loaded.Variables.Get("v2", Vector2.zero), Is.EqualTo(new Vector2(4f, 5f)));
      Assert.That(loaded.Variables.Get("c", Color.black), Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
    }

    [Test]
    public void PlayerScope_RoundTripsThroughController() =>
      AssertScopeRoundTripsAllSevenTypes(EGraphContextScope.Player);

    [Test]
    public void WorldScope_RoundTripsThroughController() =>
      AssertScopeRoundTripsAllSevenTypes(EGraphContextScope.World);

    [Test]
    public void ScopeNeverCreated_NothingSavedAndStaleKeyDeleted()
    {
      var es3 = new MemoryES3Service();
      // A stale player scope from an earlier session is already in the file.
      es3.SaveAsync(PlayerKey, new GraphStateSnapshot { GraphId = "player" }).GetAwaiter().GetResult();

      // This session never touches the player scope.
      Controller(es3, new GraphContextRegistry(new RecordingEmitter())).SaveAsync().GetAwaiter().GetResult();

      Assert.That(es3.KeyExists(PlayerKey), Is.False,
        "a scope never created this session deletes its stale key so it can't resurrect on the next load");
      Assert.That(es3.KeyExists(WorldKey), Is.False);
    }

    [Test]
    public void ScopeSnapshot_WrongGraphId_SkippedOnLoad()
    {
      var es3 = new MemoryES3Service();
      // Corrupt/cross-keyed: world data sitting under the player key.
      es3.SaveAsync(PlayerKey, new GraphStateSnapshot
      {
        GraphId = "world",
        Data = new Dictionary<string, object> { { "gold", 999 } }
      }).GetAwaiter().GetResult();

      var registry = new GraphContextRegistry(new RecordingEmitter());
      Controller(es3, registry).LoadAsync().GetAwaiter().GetResult();

      Assert.That(registry.GetScopeStateOrNull(EGraphContextScope.Player), Is.Null,
        "a snapshot whose GraphId doesn't match the scope owner never restores — not even partially");
    }

    [Test]
    public void DeclarationsSetBeforeRestore_MismatchDiscarded_DefaultApplies()
    {
      var es3 = new MemoryES3Service();
      es3.SaveAsync(PlayerKey, new GraphStateSnapshot
      {
        GraphId = "player",
        Data = new Dictionary<string, object> { { "hp", "corrupt-string" }, { "gold", 50 } }
      }).GetAwaiter().GetResult();

      var registry = new GraphContextRegistry(new RecordingEmitter());
      registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("hp", 10), IntDecl("gold", 0)));

      using var sink = new SerilogSinkScope();
      Controller(es3, registry).LoadAsync().GetAwaiter().GetResult();

      Assert.That(sink.ContainsWarning("Schema mismatch on load"), Is.True);
      var context = registry.GetOrCreate(EGraphContextScope.Player, null);
      Assert.That(context.Variables.Get("hp", -1), Is.EqualTo(10),
        "the type-mismatched saved value is discarded and the declared default applies (behavior #4, scope leg)");
      Assert.That(context.Variables.Get("gold", -1), Is.EqualTo(50),
        "well-typed siblings in the same snapshot restore normally");
    }

    [Test]
    public void RestoreBeforeDeclarations_ValuesLoadAsUndeclared_LateDeclarationsIgnoredWithError()
    {
      var es3 = new MemoryES3Service();
      es3.SaveAsync(PlayerKey, new GraphStateSnapshot
      {
        GraphId = "player",
        Data = new Dictionary<string, object> { { "hp", "typed-as-string-in-old-save" } }
      }).GetAwaiter().GetResult();

      var registry = new GraphContextRegistry(new RecordingEmitter());
      Controller(es3, registry).LoadAsync().GetAwaiter().GetResult(); // restore BEFORE declarations

      var context = registry.GetOrCreate(EGraphContextScope.Player, null);
      Assert.That(context.Variables.Get("hp", "missing"), Is.EqualTo("typed-as-string-in-old-save"),
        "restore before declarations sanitizes against nothing — everything loads as undeclared (legal)");

      using var sink = new SerilogSinkScope();
      registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("hp", 10)));
      Assert.That(sink.ContainsError("SetScopeDeclarations ignored"), Is.True,
        "declarations arriving after the scope exists are rejected loudly, pinning the ordering contract");
    }

    [Test]
    public void ScopeAlreadyAliveWithValue_MismatchedRestore_DefaultOverwritesLiveValue()
    {
      // Scope twin of the merge-semantics graph test: the player scope already exists and holds a live value
      // before load; the restore's mismatched save value must end as the declared default, not the live value.
      var es3 = new MemoryES3Service();
      es3.SaveAsync(PlayerKey, new GraphStateSnapshot
      {
        GraphId = "player",
        Data = new Dictionary<string, object> { { "hp", "corrupt-string" } }
      }).GetAwaiter().GetResult();

      var registry = new GraphContextRegistry(new RecordingEmitter());
      registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("hp", 10)));
      var context = registry.GetOrCreate(EGraphContextScope.Player, null);
      context.Variables.Set("hp", 55); // live value before load

      Controller(es3, registry).LoadAsync().GetAwaiter().GetResult();

      Assert.That(context.Variables.Get("hp", -1), Is.EqualTo(10),
        "the declared default overwrites the live value — the mismatched save is discarded, not silently kept");
    }
  }
}
