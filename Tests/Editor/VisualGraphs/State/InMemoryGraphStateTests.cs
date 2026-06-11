using MToolKit.Runtime.VisualGraphs.Runtime.State;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.State
{
  /// <summary>
  ///   Characterization of <see cref="InMemoryGraphState" />. Pins the typed get/set contract — including
  ///   the deliberate null-handling split (null is a valid value for reference types, not for value types)
  ///   that downstream variable persistence relies on.
  /// </summary>
  [TestFixture]
  public sealed class InMemoryGraphStateTests
  {
    [SetUp]
    public void SetUp()
    {
      state = new InMemoryGraphState();
    }

    private InMemoryGraphState state;

    [Test]
    public void TryGet_PresentTypedValue_ReturnsTrueAndValue()
    {
      state.Set("k", 42);

      Assert.That(state.TryGet<int>("k", out var value), Is.True);
      Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void TryGet_TypeMismatch_ReturnsFalse()
    {
      state.Set("k", 42);

      Assert.That(state.TryGet<string>("k", out var value), Is.False);
      Assert.That(value, Is.Null);
    }

    [Test]
    public void TryGet_MissingKey_ReturnsFalse()
    {
      Assert.That(state.TryGet<int>("absent", out var value), Is.False);
      Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void TryGet_NullStoredValue_ReferenceType_ReturnsTrueWithNull()
    {
      state.Set<string>("k", null);

      Assert.That(state.TryGet<string>("k", out var value), Is.True,
        "Null is a valid value for a reference type — the key is present, so TryGet succeeds with null.");
      Assert.That(value, Is.Null);
    }

    [Test]
    public void TryGet_NullStoredValue_ValueType_ReturnsFalse()
    {
      state.Set<string>("k", null);

      Assert.That(state.TryGet<int>("k", out var value), Is.False,
        "Null is not a valid int — TryGet<int> over a stored null fails rather than returning 0 as a hit.");
      Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void Set_Overwrite_ReplacesValue()
    {
      state.Set("k", "first");
      state.Set("k", "second");

      Assert.That(state.TryGet<string>("k", out var value), Is.True);
      Assert.That(value, Is.EqualTo("second"));
    }

    [Test]
    public void Contains_ReflectsPresence()
    {
      Assert.That(state.Contains("k"), Is.False);
      state.Set("k", 1);
      Assert.That(state.Contains("k"), Is.True);
    }

    [Test]
    public void AsReadOnly_ReflectsStoredEntries()
    {
      state.Set("a", 1);
      state.Set("b", "two");

      var snapshot = state.AsReadOnly();

      Assert.That(snapshot.Count, Is.EqualTo(2));
      Assert.That(snapshot["a"], Is.EqualTo(1));
      Assert.That(snapshot["b"], Is.EqualTo("two"));
    }
  }
}
