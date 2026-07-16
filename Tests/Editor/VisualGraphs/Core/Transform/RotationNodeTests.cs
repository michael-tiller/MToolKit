using MToolKit.Runtime.VisualGraphs.Executors.Transform;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Transform
{
  /// <summary>Pins <see cref="RotationNodeExecutor" />: Vector3 read/write happy path and missing-key fallback.</summary>
  [TestFixture]
  public sealed class RotationNodeTests
  {
    private static GraphRunnerHarness HarnessWithRotation()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("rot", "RotationNode", new NodeParametersDictionary
        {
          { "SourceKey", "source" }, { "DestinationKey", "result" }
        })
        .Connect("e", "rot")
        .Build());
      h.Executors.Register(new RotationNodeExecutor());
      return h;
    }

    [Test]
    public void Execute_ReadsSource_WritesDestination()
    {
      var h = HarnessWithRotation();
      h.State.Set("source", new Vector3(10, 20, 30));

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<Vector3>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(new Vector3(10, 20, 30)));
    }

    [Test]
    public void Execute_MissingSourceKey_FallsBackToVector3Zero()
    {
      LogAssert.ignoreFailingMessages = true;
      var h = HarnessWithRotation();

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<Vector3>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(Vector3.zero));
    }
  }
}
