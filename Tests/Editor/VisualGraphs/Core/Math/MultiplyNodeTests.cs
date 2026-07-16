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
  ///   Pins <see cref="MultiplyNodeExecutor" />: multiplies two float state keys, writes+emits the result,
  ///   and fails soft (0 + warning) on a missing or wrong-typed operand key.
  /// </summary>
  [TestFixture]
  public sealed class MultiplyNodeTests
  {
    private static GraphRunnerHarness HarnessWithMultiply(string aKey = "a", string bKey = "b", string resultKey = "result")
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("mul", "MultiplyNode", new NodeParametersDictionary
        {
          { "AKey", aKey },
          { "BKey", bKey },
          { "ResultKey", resultKey }
        })
        .Connect("e", "mul")
        .Build());
      h.Executors.Register(new MultiplyNodeExecutor());
      return h;
    }

    [Test]
    public void Execute_MultipliesOperands_WritesResultKey_AndEmitsStateChanged()
    {
      var h = HarnessWithMultiply();
      h.State.Set("a", 4f);
      h.State.Set("b", 2.5f);

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(10f));

      var emitted = h.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1));
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(10f));
    }

    [Test]
    public void Execute_MissingOperandKey_TreatsMissingOperandAsZero()
    {
      LogAssert.ignoreFailingMessages = true;
      var h = HarnessWithMultiply();
      h.State.Set("b", 3f); // "a" never set — product must be 0, not 3

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(0f), "a missing operand key fails soft to 0, not a throw");
    }
  }
}
