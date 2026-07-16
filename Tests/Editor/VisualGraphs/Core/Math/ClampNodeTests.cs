using System.Linq;
using MToolKit.Runtime.VisualGraphs.Executors.Math;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Math
{
  /// <summary>
  ///   Pins <see cref="ClampNodeExecutor" />: clamps a float value between two float bounds, at all three
  ///   regions (below min, above max, inside), and writes+emits the result.
  /// </summary>
  [TestFixture]
  public sealed class ClampNodeTests
  {
    private static GraphRunnerHarness HarnessWithClamp()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("clamp", "ClampNode", new NodeParametersDictionary
        {
          { "ValueKey", "value" },
          { "MinKey", "min" },
          { "MaxKey", "max" },
          { "ResultKey", "result" }
        })
        .Connect("e", "clamp")
        .Build());
      h.Executors.Register(new ClampNodeExecutor());
      return h;
    }

    private static float RunClamp(float value, float min, float max)
    {
      var h = HarnessWithClamp();
      h.State.Set("value", value);
      h.State.Set("min", min);
      h.State.Set("max", max);
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<float>("result", out var result), Is.True);
      return result;
    }

    [Test]
    public void Execute_ValueBelowMin_ClampsToMin() =>
      Assert.That(RunClamp(-5f, 0f, 10f), Is.EqualTo(0f));

    [Test]
    public void Execute_ValueAboveMax_ClampsToMax() =>
      Assert.That(RunClamp(15f, 0f, 10f), Is.EqualTo(10f));

    [Test]
    public void Execute_ValueInsideBounds_PassesThroughUnchanged() =>
      Assert.That(RunClamp(5f, 0f, 10f), Is.EqualTo(5f));

    [Test]
    public void Execute_EmitsStateChangedOnResultWrite()
    {
      var h = HarnessWithClamp();
      h.State.Set("value", 5f);
      h.State.Set("min", 0f);
      h.State.Set("max", 10f);

      h.Run(new TestMessageA());

      var emitted = h.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1));
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(5f));
    }
  }
}
