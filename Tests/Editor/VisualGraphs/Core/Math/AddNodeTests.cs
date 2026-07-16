using System.Linq;
using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Math
{
  /// <summary>
  ///   Pins <see cref="AddNodeExecutor" />: sums two float state keys, writes+emits the result, and
  ///   fails soft (0 + warning) on a missing or wrong-typed operand key.
  /// </summary>
  [TestFixture]
  public sealed class AddNodeTests
  {
    private static GraphRunnerHarness HarnessWithAdd(string aKey = "a", string bKey = "b", string resultKey = "result")
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("add", "AddNode", new NodeParametersDictionary
        {
          { "AKey", aKey },
          { "BKey", bKey },
          { "ResultKey", resultKey }
        })
        .Connect("e", "add")
        .Build());
      h.Executors.Register(new AddNodeExecutor());
      return h;
    }

    [Test]
    public void Execute_SumsOperands_WritesResultKey_AndEmitsStateChanged()
    {
      var h = HarnessWithAdd();
      h.State.Set("a", 2f);
      h.State.Set("b", 3f);

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(5f));

      var emitted = h.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1), "the result write is observable via a GraphStateChangedMessage");
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(5f));
    }

    [Test]
    public void Execute_MissingOperandKey_TreatsMissingOperandAsZero()
    {
      LogAssert.ignoreFailingMessages = true;
      var h = HarnessWithAdd();
      h.State.Set("b", 3f); // "a" never set

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(3f), "a missing operand key fails soft to 0, not a throw");
    }

    [Test]
    public void Execute_WrongTypedOperandKey_TreatsAsZero()
    {
      LogAssert.ignoreFailingMessages = true;
      var h = HarnessWithAdd();
      h.State.Set("a", "not-a-float");
      h.State.Set("b", 3f);

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(3f), "a wrong-typed operand key fails soft to 0 (IGraphState.TryGet<T> is exact-type-match)");
    }
  }
}
