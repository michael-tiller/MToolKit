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

    private static GraphVariableSet.GraphVariableEntry IntEntry(string key, int value)
    {
      return new GraphVariableSet.GraphVariableEntry
      {
        key = key,
        type = GraphVariableSet.EGraphVariableType.Int,
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
      set.entries.Add(new GraphVariableSet.GraphVariableEntry
        { key = "s", type = GraphVariableSet.EGraphVariableType.String, stringValue = "hello" });
      set.entries.Add(IntEntry("i", 7));
      set.entries.Add(new GraphVariableSet.GraphVariableEntry
        { key = "f", type = GraphVariableSet.EGraphVariableType.Float, floatValue = 1.5f });
      set.entries.Add(new GraphVariableSet.GraphVariableEntry
        { key = "b", type = GraphVariableSet.EGraphVariableType.Bool, boolValue = true });

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

    private static int Read(GraphRunnerHarness harness, string key)
    {
      harness.State.TryGet<int>(key, out var value);
      return value;
    }
  }
}
