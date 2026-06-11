using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Test message types for VisualGraphs characterization tests.
  ///   These are CLASSES (not structs): <see cref="DerivedTestMessage" /> inherits
  ///   <see cref="TestMessageA" />, which is load-bearing for the router's
  ///   "no inheritance match" characterization (routing keys on the concrete runtime
  ///   type, so a derived message must NOT reach a base-type subscription).
  /// </summary>
  public class TestMessageA : IGameMessage
  {
  }

  /// <summary>A distinct message type with no relationship to <see cref="TestMessageA" />.</summary>
  public class TestMessageB : IGameMessage
  {
  }

  /// <summary>A subclass of <see cref="TestMessageA" /> used to pin exact-type (non-inheriting) routing.</summary>
  public sealed class DerivedTestMessage : TestMessageA
  {
  }
}
