using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  /// <summary>
  ///   Characterization of <see cref="NodeExecutorRegistry" />. Pins the dictionary-backed contract the
  ///   Phase 9 refactor must preserve: validation throws, registration is last-wins, lookup is fail-loud.
  /// </summary>
  [TestFixture]
  public sealed class NodeExecutorRegistryTests
  {
    [SetUp]
    public void SetUp()
    {
      registry = new NodeExecutorRegistry();
    }

    private NodeExecutorRegistry registry;

    [Test]
    public void Register_NullExecutor_ThrowsArgumentNull()
    {
      Assert.Throws<ArgumentNullException>(() => registry.Register(null));
    }

    [Test]
    public void Register_EmptyNodeType_ThrowsArgument()
    {
      Assert.Throws<ArgumentException>(() => registry.Register(new RecordingExecutor("")));
    }

    [Test]
    public void Register_NullNodeType_ThrowsArgument()
    {
      Assert.Throws<ArgumentException>(() => registry.Register(new RecordingExecutor(null)));
    }

    [Test]
    public void Register_DuplicateNodeType_LastRegistrationWins()
    {
      var first = new RecordingExecutor("Dup");
      var second = new RecordingExecutor("Dup");

      registry.Register(first);
      registry.Register(second);

      Assert.That(registry.Get("Dup"), Is.SameAs(second),
        "Registering the same node type twice overwrites the prior executor (dictionary indexer).");
    }

    [Test]
    public void Get_UnknownType_ThrowsKeyNotFound()
    {
      Assert.Throws<KeyNotFoundException>(() => registry.Get("NeverRegistered"));
    }

    [Test]
    public void Get_NullOrEmpty_ThrowsArgument()
    {
      Assert.Throws<ArgumentException>(() => registry.Get(null));
      Assert.Throws<ArgumentException>(() => registry.Get(string.Empty));
    }

    [Test]
    public void HasExecutor_ReflectsRegistration_AndIsNullSafe()
    {
      registry.Register(new RecordingExecutor("Known"));

      Assert.That(registry.HasExecutor("Known"), Is.True);
      Assert.That(registry.HasExecutor("Unknown"), Is.False);
      Assert.That(registry.HasExecutor(null), Is.False, "HasExecutor must not throw on null.");
      Assert.That(registry.HasExecutor(string.Empty), Is.False);
    }

    [Test]
    public void KnownTypes_ReflectsRegisteredTypeNames()
    {
      registry.Register(new RecordingExecutor("A"));
      registry.Register(new RecordingExecutor("B"));

      Assert.That(registry.KnownTypes.OrderBy(t => t), Is.EqualTo(new[] { "A", "B" }));
    }
  }
}
