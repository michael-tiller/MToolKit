using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.Persistence
{
  [TestFixture]
  public sealed class OrderedLoadTransactionTests
  {
    [Test]
    public void RequiredFailure_StopsDependentControllers_InDeclaredOrder()
    {
      var calls = new List<string>();
      var first = new FirstController(calls);
      var requiredFailure = new RequiredFailureController(calls);
      var downstream = new DownstreamController(calls);
      var system = new ES3GameSaveSystem(
        new ISaveDomainController[] { downstream, requiredFailure, first },
        new MemoryES3Service());

      IReadOnlyList<string> failures = system.LoadAsync(
        new[] { TypeName(first), TypeName(requiredFailure), TypeName(downstream) },
        name => name == TypeName(requiredFailure)).GetAwaiter().GetResult();

      CollectionAssert.AreEqual(new[] { "first", "required" }, calls);
      CollectionAssert.AreEqual(new[] { TypeName(requiredFailure) }, failures);
      CollectionAssert.AreEqual(failures, system.FailedControllers);
    }

    [Test]
    public void OptionalFailure_DoesNotBlockIndependentLaterController()
    {
      var calls = new List<string>();
      var optionalFailure = new OptionalFailureController(calls);
      var later = new LaterController(calls);
      var system = new ES3GameSaveSystem(
        new ISaveDomainController[] { later, optionalFailure }, new MemoryES3Service());

      IReadOnlyList<string> failures = system.LoadAsync(
        new[] { TypeName(optionalFailure), TypeName(later) }, _ => false).GetAwaiter().GetResult();

      CollectionAssert.AreEqual(new[] { "optional", "later" }, calls);
      CollectionAssert.AreEqual(new[] { TypeName(optionalFailure) }, failures);
    }

    [Test]
    public void RetryAfterRequiredFailure_ReexecutesTheEntireDeclaredTransaction()
    {
      var calls = new List<string>();
      var first = new FirstController(calls);
      var requiredFailure = new RequiredFailureController(calls);
      var downstream = new DownstreamController(calls);
      var system = new ES3GameSaveSystem(
        new ISaveDomainController[] { first, requiredFailure, downstream }, new MemoryES3Service());
      string[] order = { TypeName(first), TypeName(requiredFailure), TypeName(downstream) };

      system.LoadAsync(order, name => name == TypeName(requiredFailure)).GetAwaiter().GetResult();
      requiredFailure.ThrowsOnLoad = false;
      IReadOnlyList<string> retryFailures = system.LoadAsync(
        order, name => name == TypeName(requiredFailure)).GetAwaiter().GetResult();

      CollectionAssert.AreEqual(
        new[] { "first", "required", "first", "required", "downstream" }, calls);
      Assert.That(retryFailures, Is.Empty);
    }

    private static string TypeName(ISaveDomainController controller)
      => controller.GetType().FullName;

    private abstract class RecordingController : ISaveDomainController
    {
      private readonly List<string> calls;
      private bool throwsOnLoad;

      protected RecordingController(string name, List<string> calls, bool throwsOnLoad = false)
      {
        Name = name;
        this.calls = calls;
        this.throwsOnLoad = throwsOnLoad;
      }

      public string Name { get; }
      public bool ThrowsOnLoad { get => throwsOnLoad; set => throwsOnLoad = value; }
      public ESaveDomain Domain => ESaveDomain.World;
      public UniTask SaveAsync(System.Threading.CancellationToken ct = default) => UniTask.CompletedTask;
      public bool HasSaveData() => true;

      public UniTask LoadAsync(System.Threading.CancellationToken ct = default)
      {
        calls.Add(Name);
        if (throwsOnLoad) throw new InvalidOperationException(Name);
        return UniTask.CompletedTask;
      }
    }

    private sealed class FirstController : RecordingController
    {
      public FirstController(List<string> calls) : base("first", calls) { }
    }

    private sealed class RequiredFailureController : RecordingController
    {
      public RequiredFailureController(List<string> calls) : base("required", calls, true) { }
    }

    private sealed class DownstreamController : RecordingController
    {
      public DownstreamController(List<string> calls) : base("downstream", calls) { }
    }

    private sealed class OptionalFailureController : RecordingController
    {
      public OptionalFailureController(List<string> calls) : base("optional", calls, true) { }
    }

    private sealed class LaterController : RecordingController
    {
      public LaterController(List<string> calls) : base("later", calls) { }
    }
  }
}
