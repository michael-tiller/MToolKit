using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.Persistence
{
  [TestFixture]
  public sealed class OrderedLoadTransactionTests
  {
    private bool previousIgnoreFailingMessages;

    [SetUp]
    public void SetUp()
    {
      previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
      LogAssert.ignoreFailingMessages = false;
    }

    [TearDown]
    public void TearDown()
    {
      LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
      try
      {
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
      }
      finally { PersistencePlayModeTestScope.RestoreSceneSetup(); }
    }

    [Test]
    public void SaveWithReceiptAsync_ReturnsTheReceiptForTheCompletedFlush()
    {
      var es3 = new MemoryES3Service();
      var system = new ES3GameSaveSystem(Array.Empty<ISaveDomainController>(), es3);

      ES3FlushReceipt receipt = system.SaveWithReceiptAsync().GetAwaiter().GetResult();

      Assert.That(receipt.Context, Is.EqualTo(es3.GetSaveContext()));
      Assert.That(receipt.FileSha256, Has.Length.EqualTo(64));
      Assert.That(es3.FlushCount, Is.EqualTo(1), "a successful save returns exactly one awaited flush receipt");
    }

    [Test]
    public async Task DurableSaveAs_SavesSourceBeforeClone()
    {
      var events = new List<string>();
      var es3 = new MemoryES3Service();
      var receipt = new ES3FlushReceipt(es3.GetSaveContext(), new string('a', 64), DateTime.UtcNow);

      string clone = await DurableSaveAsWorkflow.SaveBeforeCloneAsync(
        _ => { events.Add("source-save"); return UniTask.FromResult(receipt); },
        _ => { events.Add("clone"); return UniTask.FromResult("clone-profile"); });

      CollectionAssert.AreEqual(new[] { "source-save", "clone" }, events);
      Assert.That(clone, Is.EqualTo("clone-profile"));
    }

    [Test]
    public void DurableSaveAs_DoesNotCloneWhenSourceSaveFails()
    {
      var failure = new IOException("source checkpoint failed");
      bool cloneCalled = false;

      IOException error = Assert.ThrowsAsync<IOException>(async () =>
        await DurableSaveAsWorkflow.SaveBeforeCloneAsync(
          _ => UniTask.FromException<ES3FlushReceipt>(failure),
          _ => { cloneCalled = true; return UniTask.FromResult("clone-profile"); }));

      Assert.That(error, Is.SameAs(failure));
      Assert.That(cloneCalled, Is.False);
    }
    [Test]
    public void SaveWithReceiptAsync_WaitsForTheUnderlyingFlushReceipt()
    {
      var pendingFlush = new UniTaskCompletionSource<ES3FlushReceipt>();
      var es3 = new MemoryES3Service { PendingFlush = pendingFlush };
      var system = new ES3GameSaveSystem(Array.Empty<ISaveDomainController>(), es3);

      var saveTask = system.SaveWithReceiptAsync();
      Assert.That(saveTask.Status, Is.EqualTo(UniTaskStatus.Pending));
      var expected = new ES3FlushReceipt(es3.GetSaveContext(), new string('a', 64), DateTime.UtcNow);
      pendingFlush.TrySetResult(expected);

      Assert.That(saveTask.GetAwaiter().GetResult(), Is.SameAs(expected));
    }
    [Test]
    public void SaveWithReceiptAsync_PropagatesFlushFailureWithoutCreatingACheckpointReceipt()
    {
      var es3 = new MemoryES3Service { FlushFailure = new IOException("disk flush failed") };
      var system = new ES3GameSaveSystem(Array.Empty<ISaveDomainController>(), es3);
      using var expectedErrors = new ExpectedPersistenceErrorScope("ES3 save operation failed: disk flush failed");

      IOException error = Assert.Throws<IOException>(() => system.SaveWithReceiptAsync().GetAwaiter().GetResult());

      Assert.That(error.Message, Is.EqualTo("disk flush failed"));
      Assert.That(es3.FlushCount, Is.EqualTo(1));
    }
    [UnityTest]
    public IEnumerator SaveWithReceipt_NotifiesDurableHandlersOnlyAfterTheFlushAndIsolatesObserverFailure()
    {
      PersistencePlayModeTestScope.PrepareEmptyScene();
      yield return new EnterPlayMode();
      yield return SaveWithReceipt_NotifiesDurableHandlersOnlyAfterTheFlushAndIsolatesObserverFailureAsync().ToCoroutine();
    }

    private static async UniTask SaveWithReceipt_NotifiesDurableHandlersOnlyAfterTheFlushAndIsolatesObserverFailureAsync()
    {
      var es3 = new MemoryES3Service();
      var handler = new DurableCompletionController(es3) { ThrowOnCompletion = true };
      var system = new ES3GameSaveSystem(new ISaveDomainController[] { handler }, es3);
      using var expectedErrors = ExpectedPersistenceErrorScope.ForDurableCompletionFailure(TypeName(handler));

      ES3FlushReceipt receipt = await system.SaveWithReceiptAsync();

      Assert.That(handler.Receipt, Is.SameAs(receipt));
      Assert.That(handler.FlushCountWhenNotified, Is.EqualTo(1));
      Assert.That(es3.FlushCount, Is.EqualTo(1));
    }
    [Test]
    public void FlushReceiptForcesTheSaveFileAndHashesItsReadBackBytes()
    {
      string root = Path.Combine(Path.GetTempPath(), "mtoolkit-save-flush-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(root);
      try
      {
        string path = Path.Combine(root, "profile.es3");
        byte[] bytes = Encoding.UTF8.GetBytes("flushed-save-content");
        File.WriteAllBytes(path, bytes);
        ES3SaveContext context = ES3SaveContextIdentityStore.GetOrCreate(path);

        ES3FlushReceipt receipt = ES3FlushReceipt.ReadBackAndFlush(context);
        string expected;
        using (SHA256 sha = SHA256.Create())
          expected = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();

        Assert.That(receipt.Context, Is.EqualTo(context));
        Assert.That(receipt.FileSha256, Is.EqualTo(expected));
        Assert.That(receipt.CompletedUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
      }
      finally
      {
        Directory.Delete(root, recursive: true);
      }
    }
    [Test]
    public void SaveContextIdentity_IsPersistentAndScopedToTheCanonicalFilePath()
    {
      string root = Path.Combine(Path.GetTempPath(), "mtoolkit-save-context-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(root);
      try
      {
        string firstPath = Path.Combine(root, "profile.es3");
        ES3SaveContext first = ES3SaveContextIdentityStore.GetOrCreate(firstPath);
        ES3SaveContext reopened = ES3SaveContextIdentityStore.GetOrCreate(firstPath);
        ES3SaveContext fork = ES3SaveContextIdentityStore.GetOrCreate(Path.Combine(root, "save-as.es3"));

        Assert.That(first.SaveId, Is.EqualTo(reopened.SaveId));
        Assert.That(first.SaveId, Is.Not.EqualTo(fork.SaveId));
        Assert.That(first.SaveFilePath, Is.EqualTo(Path.GetFullPath(firstPath)));
        Assert.That(File.Exists(firstPath + ".saveid"), Is.True);
      }
      finally
      {
        Directory.Delete(root, recursive: true);
      }
    }
    [Test]
    public void RequiredFailure_StopsDependentControllers_InDeclaredOrder()
    {
      var calls = new List<string>();
      var first = new FirstController(calls);
      var requiredFailure = new RequiredFailureController(calls);
      var downstream = new DownstreamController(calls);
      using var expectedErrors = ExpectedPersistenceErrorScope.ForControllerLoadFailure(TypeName(requiredFailure));
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
      using var expectedErrors = ExpectedPersistenceErrorScope.ForControllerLoadFailure(TypeName(optionalFailure));
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
      using var expectedErrors = ExpectedPersistenceErrorScope.ForControllerLoadFailure(TypeName(requiredFailure));
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

    private sealed class DurableCompletionController : ISaveDomainController, IDurableSaveCompletionHandler
    {
      private readonly MemoryES3Service es3;
      public DurableCompletionController(MemoryES3Service es3) { this.es3 = es3; }
      public ES3FlushReceipt Receipt { get; private set; }
      public int FlushCountWhenNotified { get; private set; }
      public bool ThrowOnCompletion { get; set; }
      public ESaveDomain Domain => ESaveDomain.World;
      public UniTask SaveAsync(System.Threading.CancellationToken ct = default) => UniTask.CompletedTask;
      public UniTask LoadAsync(System.Threading.CancellationToken ct = default) => UniTask.CompletedTask;
      public bool HasSaveData() => true;
      public UniTask OnDurableSaveCompletedAsync(ES3FlushReceipt receipt, System.Threading.CancellationToken ct = default)
      {
        Receipt = receipt;
        FlushCountWhenNotified = es3.FlushCount;
        if (ThrowOnCompletion) throw new InvalidOperationException("completion failed");
        return UniTask.CompletedTask;
      }
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
