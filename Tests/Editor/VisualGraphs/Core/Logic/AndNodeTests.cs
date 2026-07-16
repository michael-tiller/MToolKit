using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Executors.Logic;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Logic
{
  /// <summary>
  ///   Pins <see cref="AndNodeExecutor" />: n-ary AND fold at list lengths 0/1/2/3+, the identity for an
  ///   empty list (true), the skip-from-fold behavior for a missing/unresolvable key, and the emit on
  ///   result write.
  /// </summary>
  [TestFixture]
  public sealed class AndNodeTests
  {
    private static GraphRunnerHarness HarnessWithAnd(List<string> keys)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("and", "AndNode", new NodeParametersDictionary
        {
          { "Keys", keys },
          { "ResultKey", "result" }
        })
        .Connect("e", "and")
        .Build());
      h.Executors.Register(new AndNodeExecutor());
      return h;
    }

    private static bool RunAnd(List<string> keys, System.Action<GraphRunnerHarness> seed = null)
    {
      var h = HarnessWithAnd(keys);
      seed?.Invoke(h);
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var result), Is.True);
      return result;
    }

    [Test]
    public void Execute_EmptyList_ReturnsIdentityTrue() =>
      Assert.That(RunAnd(new List<string>()), Is.True);

    [Test]
    public void Execute_SingleTrueKey_ReturnsTrue() =>
      Assert.That(RunAnd(new List<string> { "k1" }, h => h.State.Set("k1", true)), Is.True);

    [Test]
    public void Execute_SingleFalseKey_ReturnsFalse() =>
      Assert.That(RunAnd(new List<string> { "k1" }, h => h.State.Set("k1", false)), Is.False);

    [Test]
    public void Execute_TwoTrueKeys_ReturnsTrue() =>
      Assert.That(RunAnd(new List<string> { "k1", "k2" }, h =>
      {
        h.State.Set("k1", true);
        h.State.Set("k2", true);
      }), Is.True);

    [Test]
    public void Execute_ThreeKeysOneFalse_ReturnsFalse() =>
      Assert.That(RunAnd(new List<string> { "k1", "k2", "k3" }, h =>
      {
        h.State.Set("k1", true);
        h.State.Set("k2", false);
        h.State.Set("k3", true);
      }), Is.False);

    [Test]
    public void Execute_MissingKeyInList_IsSkippedFromFold_NotTreatedAsFalse()
    {
      LogAssert.ignoreFailingMessages = true;
      // "missing" never set — if this were treated as false the fold would (wrongly) be false
      Assert.That(RunAnd(new List<string> { "k1", "missing" }, h => h.State.Set("k1", true)), Is.True,
        "a missing/unresolvable key is skipped from the fold, not treated as false");
    }

    [Test]
    public void Execute_EmitsStateChangedOnResultWrite()
    {
      var h = HarnessWithAnd(new List<string> { "k1" });
      h.State.Set("k1", true);

      h.Run(new TestMessageA());

      var emitted = h.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1));
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(true));
    }
  }
}
