using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.State
{
  /// <summary>
  ///   DIRECT order model of the variable precedence chain — NOT GraphLoader coverage. The real loader
  ///   (<c>GraphLoader</c>) awaits <c>UniTask.WaitForEndOfFrame</c> in every load path, so it cannot be driven
  ///   synchronously in EditMode. This fixture instead applies the same legs in the production order the loader
  ///   uses — global variables (GraphLoader.cs:214) then initial variables (GraphLoader.cs:219) — and then the
  ///   restored-save leg via the real <c>GraphRunner.ImportState</c> (GraphRunner.cs:369-370), pinning
  ///   global &lt; initial &lt; save. Also characterizes <see cref="GraphVariableSet.ApplyTo" /> directly.
  /// </summary>
  [TestFixture]
  public sealed class VariablePrecedenceTests : UnityObjectCleanup
  {
    private GraphVariableSet NewSet()
    {
      return Track(ScriptableObject.CreateInstance<GraphVariableSet>());
    }

    private static GraphVariableDeclaration IntEntry(string key, int value)
    {
      return new GraphVariableDeclaration
      {
        key = key,
        type = EGraphVariableType.Int,
        intValue = value
      };
    }

    [Test]
    public void Precedence_GlobalThenInitialThenImport_SaveWins()
    {
      var harness = new GraphRunnerHarness(GraphDefBuilder.New().Id("g1").Build());

      var global = NewSet();
      global.entries.Add(IntEntry("hp", 10));
      var initial = NewSet();
      initial.entries.Add(IntEntry("hp", 20));

      global.ApplyTo(harness.State); // leg 1 — global base
      Assert.That(Read(harness, "hp"), Is.EqualTo(10));

      initial.ApplyTo(harness.State); // leg 2 — definition initial overrides global
      Assert.That(Read(harness, "hp"), Is.EqualTo(20));

      harness.Runner.ImportState(new GraphStateSnapshot // leg 3 — restored save wins
      {
        GraphId = "g1",
        Data = new Dictionary<string, object> { { "hp", 99 } }
      });
      Assert.That(Read(harness, "hp"), Is.EqualTo(99),
        "restored save value wins over both global and initial — the precedence order the loader establishes");
    }

    [Test]
    public void ApplyTo_WritesEachSupportedTypeAsTyped()
    {
      var set = NewSet();
      set.entries.Add(new GraphVariableDeclaration
        { key = "s", type = EGraphVariableType.String, stringValue = "hello" });
      set.entries.Add(IntEntry("i", 7));
      set.entries.Add(new GraphVariableDeclaration
        { key = "f", type = EGraphVariableType.Float, floatValue = 1.5f });
      set.entries.Add(new GraphVariableDeclaration
        { key = "b", type = EGraphVariableType.Bool, boolValue = true });

      var state = new InMemoryGraphState();
      set.ApplyTo(state);

      Assert.That(state.TryGet<string>("s", out var s), Is.True);
      Assert.That(s, Is.EqualTo("hello"));
      Assert.That(state.TryGet<int>("i", out var i), Is.True);
      Assert.That(i, Is.EqualTo(7));
      Assert.That(state.TryGet<float>("f", out var f), Is.True);
      Assert.That(f, Is.EqualTo(1.5f));
      Assert.That(state.TryGet<bool>("b", out var b), Is.True);
      Assert.That(b, Is.True);
    }

    [Test]
    public void ApplyTo_SkipsEmptyKey()
    {
      var set = NewSet();
      set.entries.Add(IntEntry("", 5));

      var state = new InMemoryGraphState();
      set.ApplyTo(state);

      Assert.That(state.AsReadOnly(), Is.Empty, "entries with an empty key are skipped");
    }

    [Test]
    public void ApplyTo_WritesVector3Vector2ColorAsTyped()
    {
      var set = NewSet();
      set.entries.Add(new GraphVariableDeclaration
        { key = "v3", type = EGraphVariableType.Vector3, vector3Value = new Vector3(1f, 2f, 3f) });
      set.entries.Add(new GraphVariableDeclaration
        { key = "v2", type = EGraphVariableType.Vector2, vector2Value = new Vector2(4f, 5f) });
      set.entries.Add(new GraphVariableDeclaration
        { key = "c", type = EGraphVariableType.Color, colorValue = Color.red });

      var state = new InMemoryGraphState();
      set.ApplyTo(state);

      Assert.That(state.TryGet<Vector3>("v3", out var v3), Is.True);
      Assert.That(v3, Is.EqualTo(new Vector3(1f, 2f, 3f)));
      Assert.That(state.TryGet<Vector2>("v2", out var v2), Is.True);
      Assert.That(v2, Is.EqualTo(new Vector2(4f, 5f)));
      Assert.That(state.TryGet<Color>("c", out var c), Is.True);
      Assert.That(c, Is.EqualTo(Color.red));
    }

    [Test]
    public void ApplyTo_SkipsNullEntry()
    {
      var set = NewSet();
      set.entries.Add(null); // Unity list serialization can produce null slots
      set.entries.Add(IntEntry("hp", 5));

      var state = new InMemoryGraphState();
      set.ApplyTo(state);

      Assert.That(state.TryGet<int>("hp", out var hp), Is.True, "null entries are skipped, the rest still apply");
      Assert.That(hp, Is.EqualTo(5));
    }

    private static int Read(GraphRunnerHarness harness, string key)
    {
      harness.State.TryGet<int>(key, out var value);
      return value;
    }
  }
}
