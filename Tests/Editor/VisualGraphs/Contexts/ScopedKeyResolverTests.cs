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
  ///   Pins <see cref="ScopedKeyResolver" />: the parse grammar (four forms + malformed fail-loud), cross-scope
  ///   routing (Quest↔Player), and the three-case fallback semantics — a present value or declared default
  ///   returns silently; an unset+undeclared key on a resolved target, or a missing target context, warns and
  ///   returns the caller fallback (asserted via a real Serilog sink, not just no-throw).
  /// </summary>
  [TestFixture]
  public sealed class ScopedKeyResolverTests : UnityObjectCleanup
  {
    private RecordingEmitter emitter;
    private GraphContextRegistry registry;
    private ScopedKeyResolver resolver;

    [SetUp]
    public void SetUp()
    {
      LogAssert.ignoreFailingMessages = true;
      NodeDebugEvents.ClearAllSubscribers();
      emitter = new RecordingEmitter();
      registry = new GraphContextRegistry(emitter);
      resolver = new ScopedKeyResolver(registry);
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

    private IGraphContext LocalGraph(string owner = "q-1") =>
      registry.GetOrCreate(EGraphContextScope.Graph, owner, new InMemoryGraphState());

    // ---- Parse ----

    [Test]
    public void Parse_BareKey_ResolvesLocal()
    {
      var r = ScopedKeyResolver.Parse("gold");
      Assert.That(r.IsLocal, Is.True);
      Assert.That(r.Key, Is.EqualTo("gold"));
    }

    [Test]
    public void Parse_BareKeyWithDots_ResolvesLocalVerbatim()
    {
      var r = ScopedKeyResolver.Parse("stats.kills");
      Assert.That(r.IsLocal, Is.True);
      Assert.That(r.Key, Is.EqualTo("stats.kills"));
    }

    [Test]
    public void Parse_BarePlayerWithoutDot_ResolvesLocal()
    {
      var r = ScopedKeyResolver.Parse("player");
      Assert.That(r.IsLocal, Is.True, "only the dotted 'player.' prefix is reserved");
      Assert.That(r.Key, Is.EqualTo("player"));
    }

    [Test]
    public void Parse_PlayerPrefix_ResolvesPlayerScope()
    {
      var r = ScopedKeyResolver.Parse("player.gold");
      Assert.That(r.IsLocal, Is.False);
      Assert.That(r.Scope, Is.EqualTo(EGraphContextScope.Player));
      Assert.That(r.Key, Is.EqualTo("gold"));
    }

    [Test]
    public void Parse_WorldPrefix_ResolvesWorldScope()
    {
      var r = ScopedKeyResolver.Parse("world.time_of_day");
      Assert.That(r.Scope, Is.EqualTo(EGraphContextScope.World));
      Assert.That(r.Key, Is.EqualTo("time_of_day"));
    }

    [Test]
    public void Parse_QuestPrefix_ResolvesGraphScopeWithOwnerId()
    {
      var r = ScopedKeyResolver.Parse("quest:q-123.kills");
      Assert.That(r.Scope, Is.EqualTo(EGraphContextScope.Graph));
      Assert.That(r.OwnerId, Is.EqualTo("q-123"));
      Assert.That(r.Key, Is.EqualTo("kills"));
    }

    [Test]
    public void Parse_QuestKeyWithDots_KeepsRemainderVerbatim()
    {
      var r = ScopedKeyResolver.Parse("quest:q-123.stats.kills");
      Assert.That(r.OwnerId, Is.EqualTo("q-123"), "the id runs to the FIRST dot");
      Assert.That(r.Key, Is.EqualTo("stats.kills"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_NullOrEmptyOrWhitespace_ThrowsArgumentException(string input)
    {
      Assert.Throws<ArgumentException>(() => ScopedKeyResolver.Parse(input));
    }

    [TestCase("player.")]
    [TestCase("world.")]
    [TestCase("quest:")]
    [TestCase("quest:abc")]
    [TestCase("quest:.kills")]
    [TestCase("quest:abc.")]
    public void Parse_MalformedReservedForms_ThrowsArgumentException(string input)
    {
      Assert.Throws<ArgumentException>(() => ScopedKeyResolver.Parse(input));
    }

    // ---- Cross-scope routing ----

    [Test]
    public void Get_QuestContextReadsPlayerKey_ReturnsPlayerValue()
    {
      registry.GetOrCreate(EGraphContextScope.Player, null).Variables.Set("gold", 50);
      var local = LocalGraph();

      Assert.That(resolver.Get("player.gold", local, -1), Is.EqualTo(50));
    }

    [Test]
    public void Get_PlayerLocalReadsQuestKey_ReturnsQuestValue()
    {
      registry.GetOrCreate(EGraphContextScope.Graph, "q-7", new InMemoryGraphState()).Variables.Set("kills", 3);
      var playerLocal = registry.GetOrCreate(EGraphContextScope.Player, null);

      Assert.That(resolver.Get("quest:q-7.kills", playerLocal, -1), Is.EqualTo(3));
    }

    // ---- Fallback semantics (three cases) ----

    [Test]
    public void Get_ResolvedTargetUnsetKeyWithDeclaration_ReturnsDeclaredDefaultSilently()
    {
      registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("gold", 100)));
      var local = LocalGraph();

      using var sink = new SerilogSinkScope();
      Assert.That(resolver.Get("player.gold", local, -1), Is.EqualTo(100));
      Assert.That(sink.Warnings, Is.Empty, "a declared default is a legitimate value, not a miss");
    }

    [Test]
    public void Get_ResolvedTargetUnsetKeyNoDeclaration_ReturnsCallerFallbackAndLogsWarning()
    {
      var local = LocalGraph();

      using var sink = new SerilogSinkScope();
      Assert.That(resolver.Get("player.gold", local, -1), Is.EqualTo(-1));
      Assert.That(sink.ContainsWarning("unset and undeclared"), Is.True);
    }

    [Test]
    public void Get_MissingQuestContext_ReturnsCallerFallbackAndLogsWarning()
    {
      var local = LocalGraph();

      using var sink = new SerilogSinkScope();
      Assert.That(resolver.Get("quest:ghost.kills", local, 99), Is.EqualTo(99));
      Assert.That(sink.ContainsWarning("no matching context"), Is.True);
    }

    [Test]
    public void Set_MissingQuestContext_NoOpsAndLogsWarning()
    {
      var local = LocalGraph();

      using var sink = new SerilogSinkScope();
      Assert.DoesNotThrow(() => resolver.Set("quest:ghost.kills", local, 5));
      Assert.That(sink.ContainsWarning("Set ignored"), Is.True);
      Assert.That(registry.TryGet(EGraphContextScope.Graph, "ghost", out _), Is.False,
        "a missing-target Set must not create the context");
    }

    [Test]
    public void Set_PlayerKey_WritesToPlayerStorage()
    {
      var local = LocalGraph();
      resolver.Set("player.gold", local, 250);

      Assert.That(registry.GetOrCreate(EGraphContextScope.Player, null).Variables.Get("gold", -1), Is.EqualTo(250));
    }

    [Test]
    public void Get_BareKeyWithNullLocal_ThrowsArgumentNullException()
    {
      Assert.Throws<ArgumentNullException>(() => resolver.Get("gold", null, -1));
    }

    [Test]
    public void ScopedFallback_DeclaredDefaultWhenTargetExists_CallerFallbackWithWarningWhenMissing()
    {
      // The clarified roadmap/DOD sentence, made unambiguous across three distinct cases.
      registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("gold", 100)));
      var local = LocalGraph();

      // (a) target exists, key unset, declaration exists -> declared default, SILENT (this is NOT a missing target)
      using (var sink = new SerilogSinkScope())
      {
        Assert.That(resolver.Get("player.gold", local, -1), Is.EqualTo(100));
        Assert.That(sink.Warnings, Is.Empty);
      }

      // (b) target exists (World lazily), key unset, NO declaration -> caller fallback + warning
      using (var sink = new SerilogSinkScope())
      {
        Assert.That(resolver.Get("world.unset", local, -7), Is.EqualTo(-7));
        Assert.That(sink.ContainsWarning("unset and undeclared"), Is.True);
      }

      // (c) target context itself missing (quest) -> caller fallback + warning; declared default is unreachable here
      using (var sink = new SerilogSinkScope())
      {
        Assert.That(resolver.Get("quest:ghost.kills", local, -9), Is.EqualTo(-9));
        Assert.That(sink.ContainsWarning("no matching context"), Is.True);
      }
    }

    [Test]
    public void Get_QuestScopedGraphReadsPlayerGoldAndWorldKey_ResolvesBoth()
    {
      // M04 DOD bullet: a quest-scoped graph reads player.gold (declared default, unset) and world.<key>
      // (stored), both through the resolver, with no throw.
      registry.SetScopeDeclarations(EGraphContextScope.Player, DeclSet(IntDecl("gold", 100)));
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("time_of_day", 12);

      var questLocal = registry.GetOrCreate(EGraphContextScope.Graph, "q-dod", new InMemoryGraphState());

      Assert.That(resolver.Get("player.gold", questLocal, -1), Is.EqualTo(100));
      Assert.That(resolver.Get("world.time_of_day", questLocal, -1), Is.EqualTo(12));
    }
  }
}
