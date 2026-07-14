using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Math
{
  /// <summary>
  ///   Pins <see cref="LerpNodeExecutor" />: interpolates between two float state values, with t clamped
  ///   at 0, 1, and out-of-range inputs (Mathf.Lerp semantics).
  /// </summary>
  [TestFixture]
  public sealed class LerpNodeTests
  {
    private static float RunLerp(float a, float b, float t)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("lerp", "LerpNode", new NodeParametersDictionary
        {
          { "AKey", "a" },
          { "BKey", "b" },
          { "TKey", "t" },
          { "ResultKey", "result" }
        })
        .Connect("e", "lerp")
        .Build());
      h.Executors.Register(new LerpNodeExecutor());
      h.State.Set("a", a);
      h.State.Set("b", b);
      h.State.Set("t", t);
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      return result;
    }

    [Test]
    public void Execute_TZero_ReturnsA() =>
      Assert.That(RunLerp(0f, 10f, 0f), Is.EqualTo(0f));

    [Test]
    public void Execute_TOne_ReturnsB() =>
      Assert.That(RunLerp(0f, 10f, 1f), Is.EqualTo(10f));

    [Test]
    public void Execute_THalf_ReturnsMidpoint() =>
      Assert.That(RunLerp(0f, 10f, 0.5f), Is.EqualTo(5f));

    [Test]
    public void Execute_TOutOfRangeAbove_ClampsToB() =>
      Assert.That(RunLerp(0f, 10f, 2f), Is.EqualTo(10f), "Mathf.Lerp implicitly clamps t to [0,1]");

    [Test]
    public void Execute_TOutOfRangeBelow_ClampsToA() =>
      Assert.That(RunLerp(0f, 10f, -1f), Is.EqualTo(0f), "Mathf.Lerp implicitly clamps t to [0,1]");
  }
}
