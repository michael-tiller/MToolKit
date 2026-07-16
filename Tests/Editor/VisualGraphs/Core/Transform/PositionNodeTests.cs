using System.Linq;
using MToolKit.Runtime.VisualGraphs.Executors.Transform;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Transform
{
  /// <summary>Pins <see cref="PositionNodeExecutor" />: Vector3 read/write happy path and missing-key fallback.</summary>
  [TestFixture]
  public sealed class PositionNodeTests
  {
    private static GraphRunnerHarness HarnessWithPosition()
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("pos", "PositionNode", new NodeParametersDictionary
        {
          { "SourceKey", "source" }, { "DestinationKey", "result" }
        })
        .Connect("e", "pos")
        .Build());
      h.Executors.Register(new PositionNodeExecutor());
      return h;
    }

    [Test]
    public void Execute_ReadsSource_WritesDestination_AndEmitsStateChanged()
    {
      var h = HarnessWithPosition();
      h.State.Set("source", new Vector3(1, 2, 3));

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<Vector3>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(new Vector3(1, 2, 3)));

      var emitted = h.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1));
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(new Vector3(1, 2, 3)));
    }

    [Test]
    public void Execute_MissingSourceKey_FallsBackToVector3Zero()
    {
      LogAssert.ignoreFailingMessages = true;
      var h = HarnessWithPosition();

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<Vector3>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(Vector3.zero));
    }
  }
}
