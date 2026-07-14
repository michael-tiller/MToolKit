using MToolKit.Runtime.VisualGraphs.Executors.Logic;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.Logic
{
  /// <summary>Pins <see cref="XorNodeExecutor" />: binary XOR truth table.</summary>
  [TestFixture]
  public sealed class XorNodeTests
  {
    private static bool RunXor(bool left, bool right)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("xor", "XorNode", new NodeParametersDictionary
        {
          { "LeftKey", "left" }, { "RightKey", "right" }, { "ResultKey", "result" }
        })
        .Connect("e", "xor")
        .Build());
      h.Executors.Register(new XorNodeExecutor());
      h.State.Set("left", left);
      h.State.Set("right", right);
      h.Run(new TestMessageA());
      Assert.That(h.State.TryGet<bool>("result", out var result), Is.True);
      return result;
    }

    [Test]
    public void Execute_TrueTrue_ReturnsFalse() => Assert.That(RunXor(true, true), Is.False);

    [Test]
    public void Execute_TrueFalse_ReturnsTrue() => Assert.That(RunXor(true, false), Is.True);

    [Test]
    public void Execute_FalseTrue_ReturnsTrue() => Assert.That(RunXor(false, true), Is.True);

    [Test]
    public void Execute_FalseFalse_ReturnsFalse() => Assert.That(RunXor(false, false), Is.False);
  }
}
