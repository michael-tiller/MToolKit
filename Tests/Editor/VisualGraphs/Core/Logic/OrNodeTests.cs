using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Executors.Logic;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Logic
{
  /// <summary>
  ///   Pins <see cref="OrNodeExecutor" />: n-ary OR fold at list lengths 0/1/2/3+, the identity for an
  ///   empty list (false), and the skip-from-fold behavior for a missing/unresolvable key.
  /// </summary>
  [TestFixture]
  public sealed class OrNodeTests
  {
    private static bool RunOr(List<string> keys, System.Action<GraphRunnerHarness> seed = null)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("or", "OrNode", new NodeParametersDictionary { { "Keys", keys }, { "ResultKey", "result" } })
        .Connect("e", "or")
        .Build());
      h.Executors.Register(new OrNodeExecutor());
      seed?.Invoke(h);
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var result), Is.True);
      return result;
    }

    [Test]
    public void Execute_EmptyList_ReturnsIdentityFalse() =>
      Assert.That(RunOr(new List<string>()), Is.False);

    [Test]
    public void Execute_SingleTrueKey_ReturnsTrue() =>
      Assert.That(RunOr(new List<string> { "k1" }, h => h.State.Set("k1", true)), Is.True);

    [Test]
    public void Execute_SingleFalseKey_ReturnsFalse() =>
      Assert.That(RunOr(new List<string> { "k1" }, h => h.State.Set("k1", false)), Is.False);

    [Test]
    public void Execute_TwoFalseKeys_ReturnsFalse() =>
      Assert.That(RunOr(new List<string> { "k1", "k2" }, h =>
      {
        h.State.Set("k1", false);
        h.State.Set("k2", false);
      }), Is.False);

    [Test]
    public void Execute_ThreeKeysOneTrue_ReturnsTrue() =>
      Assert.That(RunOr(new List<string> { "k1", "k2", "k3" }, h =>
      {
        h.State.Set("k1", false);
        h.State.Set("k2", true);
        h.State.Set("k3", false);
      }), Is.True);

    [Test]
    public void Execute_MissingKeyInList_IsSkippedFromFold_NotTreatedAsTrue()
    {
      LogAssert.ignoreFailingMessages = true;
      // "missing" never set — if this were treated as true the fold would (wrongly) be true
      Assert.That(RunOr(new List<string> { "k1", "missing" }, h => h.State.Set("k1", false)), Is.False,
        "a missing/unresolvable key is skipped from the fold, not treated as true");
    }
  }
}
