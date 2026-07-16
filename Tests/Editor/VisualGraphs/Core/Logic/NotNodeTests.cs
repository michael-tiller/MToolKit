using MToolKit.Runtime.VisualGraphs.Executors.Logic;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Logic
{
  /// <summary>Pins <see cref="NotNodeExecutor" />: negation truth table plus missing-key fail-soft.</summary>
  [TestFixture]
  public sealed class NotNodeTests
  {
    private static bool RunNot(System.Action<GraphRunnerHarness> seed)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("not", "NotNode", new NodeParametersDictionary { { "Key", "value" }, { "ResultKey", "result" } })
        .Connect("e", "not")
        .Build());
      h.Executors.Register(new NotNodeExecutor());
      seed(h);
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var result), Is.True);
      return result;
    }

    [Test]
    public void Execute_True_ReturnsFalse() =>
      Assert.That(RunNot(h => h.State.Set("value", true)), Is.False);

    [Test]
    public void Execute_False_ReturnsTrue() =>
      Assert.That(RunNot(h => h.State.Set("value", false)), Is.True);

    [Test]
    public void Execute_MissingKey_TreatedAsFalse_ReturnsTrue()
    {
      LogAssert.ignoreFailingMessages = true;
      Assert.That(RunNot(_ => { }), Is.True, "a missing key is treated as false, so NOT false = true");
    }
  }
}
