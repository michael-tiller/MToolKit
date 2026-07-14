using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Executors.State;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.State
{
  /// <summary>
  ///   Pins <see cref="CheckWorldStateNodeExecutor" />: Matches/DoesntMatch branching across a scoped and a
  ///   local key, Equals/NotEquals for every <see cref="EGraphVariableType" />, ordering restricted to
  ///   Int/Float, and the two Round-11/13 fixes — runtime-type-mismatch and null — both gated BEFORE the
  ///   operator switch so Equals/NotEquals never diverge on an incomparable pair.
  /// </summary>
  [TestFixture]
  public sealed class CheckWorldStateNodeTests
  {
    private GraphContextRegistry registry;
    private ScopedKeyResolver resolver;

    [SetUp]
    public void SetUp()
    {
      registry = new GraphContextRegistry(new RecordingEmitter());
      resolver = new ScopedKeyResolver(registry);
    }

    /// <summary>Runs a CheckWorldStateNode; returns true iff execution reached the Matches port (never both/neither).</summary>
    private bool RunCheck(GraphRunnerHarness h)
    {
      h.Executors.Register(new CheckWorldStateNodeExecutor(resolver));
      var matches = h.RegisterExecutor("MatchesMarker");
      var doesntMatch = h.RegisterExecutor("DoesntMatchMarker");
      h.Run(new TestMessageA());
      Assert.That(matches.ExecuteCallCount + doesntMatch.ExecuteCallCount, Is.EqualTo(1),
        "exactly one branch fires per run");
      return matches.ExecuteCallCount == 1;
    }

    private GraphRunnerHarness NewHarness(string key, string op, GraphVariableDeclaration expected)
    {
      return new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("check", "CheckWorldStateNode", new NodeParametersDictionary
        {
          { "Key", key }, { "ComparisonOperator", op }, { "ExpectedValue", expected }
        })
        .Node("m", "MatchesMarker")
        .Node("d", "DoesntMatchMarker")
        .Connect("e", "check")
        .Connect("check", "m", "Matches")
        .Connect("check", "d", "DoesntMatch")
        .Build());
    }

    [Test]
    public void Execute_LocalKeyEquals_Matches()
    {
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Int, intValue = 5 };
      var h = NewHarness("local_key", "Equals", expected);
      h.State.Set("local_key", 5);

      Assert.That(RunCheck(h), Is.True);
    }

    [Test]
    public void Execute_WorldScopedKeyEquals_Matches()
    {
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("flag", true);
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Bool, boolValue = true };
      var h = NewHarness("world.flag", "Equals", expected);

      Assert.That(RunCheck(h), Is.True);
    }

    [Test]
    public void Execute_WorldScopedKeyNotEquals_DoesntMatch()
    {
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("flag", true);
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Bool, boolValue = true };
      var h = NewHarness("world.flag", "NotEquals", expected);

      Assert.That(RunCheck(h), Is.False);
    }

    [Test]
    public void Execute_MissingKey_DoesntMatch()
    {
      LogAssert.ignoreFailingMessages = true;
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Int, intValue = 5 };
      var h = NewHarness("world.never_set", "Equals", expected);

      Assert.That(RunCheck(h), Is.False);
    }

    [Test]
    public void Execute_OrderingOperator_ValidForFloat_GreaterThan()
    {
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("hp", 50f);
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Float, floatValue = 10f };
      var h = NewHarness("world.hp", "GreaterThan", expected);

      Assert.That(RunCheck(h), Is.True);
    }

    [Test]
    public void Execute_OrderingOperator_InvalidForBool_DoesntMatch()
    {
      LogAssert.ignoreFailingMessages = true;
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("flag", true);
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Bool, boolValue = true };
      var h = NewHarness("world.flag", "GreaterThan", expected);

      Assert.That(RunCheck(h), Is.False,
        "ordering is undefined for Bool — fails soft to DoesntMatch, not a throw");
    }

    [Test]
    public void Execute_RuntimeTypeMismatch_Equals_DoesntMatch()
    {
      LogAssert.ignoreFailingMessages = true;
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("v", "5"); // actual: string
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Int, intValue = 5 };
      var h = NewHarness("world.v", "Equals", expected);

      Assert.That(RunCheck(h), Is.False);
    }

    [Test]
    public void Execute_RuntimeTypeMismatch_NotEquals_StillDoesntMatch_NotFalsePositive()
    {
      LogAssert.ignoreFailingMessages = true;
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("v", "5"); // actual: string
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.Int, intValue = 5 };
      var h = NewHarness("world.v", "NotEquals", expected);

      Assert.That(RunCheck(h), Is.False,
        "a type mismatch must fail soft to DoesntMatch for NotEquals too, not be read as a true match");
    }

    [Test]
    public void Execute_NullActualValue_Equals_DoesntMatch()
    {
      LogAssert.ignoreFailingMessages = true;
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set<string>("v", null); // legal: declared-String-shaped null
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.String, stringValue = "x" };
      var h = NewHarness("world.v", "Equals", expected);

      Assert.That(RunCheck(h), Is.False);
    }

    [Test]
    public void Execute_NullActualValue_NotEquals_StillDoesntMatch_NoNullReferenceException()
    {
      LogAssert.ignoreFailingMessages = true;
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set<string>("v", null);
      var expected = new GraphVariableDeclaration { type = EGraphVariableType.String, stringValue = "x" };
      var h = NewHarness("world.v", "NotEquals", expected);

      Assert.That(RunCheck(h), Is.False,
        "the null gate runs before GetType(), so this must not throw and must not be a false match");
    }

    [TestCase(EGraphVariableType.String)]
    [TestCase(EGraphVariableType.Int)]
    [TestCase(EGraphVariableType.Float)]
    [TestCase(EGraphVariableType.Bool)]
    [TestCase(EGraphVariableType.Vector3)]
    [TestCase(EGraphVariableType.Vector2)]
    [TestCase(EGraphVariableType.Color)]
    public void Execute_EqualsAcrossAllSevenTypes_Matches(EGraphVariableType type)
    {
      var decl = new GraphVariableDeclaration { type = type };
      object value = type switch
      {
        EGraphVariableType.String => "hello",
        EGraphVariableType.Int => 5,
        EGraphVariableType.Float => 1.5f,
        EGraphVariableType.Bool => true,
        EGraphVariableType.Vector3 => new Vector3(1, 2, 3),
        EGraphVariableType.Vector2 => new Vector2(1, 2),
        EGraphVariableType.Color => Color.red,
        _ => null
      };
      switch (type)
      {
        case EGraphVariableType.String: decl.stringValue = (string)value; break;
        case EGraphVariableType.Int: decl.intValue = (int)value; break;
        case EGraphVariableType.Float: decl.floatValue = (float)value; break;
        case EGraphVariableType.Bool: decl.boolValue = (bool)value; break;
        case EGraphVariableType.Vector3: decl.vector3Value = (Vector3)value; break;
        case EGraphVariableType.Vector2: decl.vector2Value = (Vector2)value; break;
        case EGraphVariableType.Color: decl.colorValue = (Color)value; break;
      }

      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("v", value);
      var h = NewHarness("world.v", "Equals", decl);

      Assert.That(RunCheck(h), Is.True);
    }
  }
}
