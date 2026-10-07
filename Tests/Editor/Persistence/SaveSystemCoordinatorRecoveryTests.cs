using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.Migration;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.Persistence
{
  /// <summary>Recovery must discard the local scope without weakening application save safety.</summary>
  [TestFixture]
  public sealed class SaveSystemCoordinatorRecoveryTests
  {
    private bool previousIgnoreFailingMessages;

    [SetUp]
    public void SetUp()
    {
      previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
      LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void TearDown()
    {
      LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
    }

    [UnityTest]
    public IEnumerator FailedLocalRestore_AfterDiscard_CanLoadAndSaveWithFreshControllers()
      => UniTask.ToCoroutine(async () =>
    {
      using var session = new Session();
      var failed = new Controller { FailLoad = true };
      session.Coordinator.RegisterLocalController(failed);
      session.FailRequiredLoad(failed);
      session.Coordinator.UnregisterLocalController(failed);

      BeginAfterDiscard(session.Coordinator);

      Assert.IsFalse(session.Coordinator.IsSaveBlocked.Value,
        "A discarded failed local restore must not poison the next gameplay scope.");
      var fresh = new Controller();
      session.Coordinator.RegisterLocalController(fresh);
      await session.Coordinator.LoadAsync(new[] { fresh.GetType().FullName }, _ => true);
      await session.Coordinator.SaveAsync();
      Assert.AreEqual(1, fresh.LoadCount);
      Assert.AreEqual(1, fresh.SaveCount);
      Assert.AreEqual(1, failed.LoadCount);
      Assert.AreEqual(0, failed.SaveCount);
      Assert.AreEqual(1, session.Es3.FlushCount,
        "The recovered session must still complete the durable save boundary.");
    });

    [TestCase(false)]
    [TestCase(true)]
    public async Task DurableFlush_RejectsDiscardUntilReceiptAndNotificationComplete(bool requireReceipt)
    {
      using var session = new Session();
      var completion = new UniTaskCompletionSource<ES3FlushReceipt>();
      session.Es3.PendingFlush = completion;
      var expected = new ES3FlushReceipt(session.Es3.GetSaveContext(), new string('a', 64), DateTime.UtcNow);
      ES3FlushReceipt observed = null;
      ES3FlushReceipt returned = null;
      bool observerDiscardRejected = false;
      session.Coordinator.DurableSaveCompleted += receipt =>
      {
        observed = receipt;
        try { BeginAfterDiscard(session.Coordinator); }
        catch (InvalidOperationException) { observerDiscardRejected = true; }
      };

      async UniTask SaveSelectedEntryPointAsync()
      {
        if (requireReceipt) returned = await session.Coordinator.SaveWithReceiptAsync();
        else await session.Coordinator.SaveAsync();
      }

      var saving = SaveSelectedEntryPointAsync();
      try
      {
        Assert.AreEqual(UniTaskStatus.Pending, saving.Status);
        Assert.IsNull(observed, "A pending disk flush cannot publish a receipt.");
        Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator),
          "An empty controller registry must not allow discard while the disk flush is pending.");
      }
      finally
      {
        completion.TrySetResult(expected);
        await saving;
      }

      Assert.AreSame(expected, observed);
      if (requireReceipt) Assert.AreSame(expected, returned);
      Assert.IsTrue(observerDiscardRejected, "Receipt notification is still inside the persistence boundary.");
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DurableFlushFailure_ReleasesBoundaryWithoutPublishingReceipt(bool requireReceipt)
    {
      using var session = new Session();
      session.Es3.FlushFailure = new IOException("injected flush failure");
      int notifications = 0;
      session.Coordinator.DurableSaveCompleted += _ => notifications++;

      if (requireReceipt)
        Assert.Throws<IOException>(() => session.Coordinator.SaveWithReceiptAsync().GetAwaiter().GetResult());
      else
        Assert.Throws<IOException>(() => session.Coordinator.SaveAsync().GetAwaiter().GetResult());

      Assert.AreEqual(0, notifications);
      Assert.AreEqual(1, session.Es3.FlushCount);
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    }

    [Test]
    public void ReceiptSave_DoesNotBypassApplicationBlockAfterDiscard()
    {
      using var session = new Session();
      session.Coordinator.IsSaveBlocked.Value = true;
      BeginAfterDiscard(session.Coordinator);

      Assert.Throws<InvalidOperationException>(() => session.Coordinator.SaveWithReceiptAsync().GetAwaiter().GetResult());
      Assert.AreEqual(0, session.Es3.FlushCount);
      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value);
    }

    [UnityTest]
    public IEnumerator CancelledControllerProfileBatch_AfterDiscard_DoesNotLeakIntoFreshSave()
      => CancelledProfileBatch_AfterDiscard_DoesNotLeakIntoFreshSave("controller").ToCoroutine();

    [UnityTest]
    public IEnumerator CancelledFlushEntryProfileBatch_AfterDiscard_DoesNotLeakIntoFreshSave()
      => CancelledProfileBatch_AfterDiscard_DoesNotLeakIntoFreshSave("flush-entry").ToCoroutine();

    private static async UniTask CancelledProfileBatch_AfterDiscard_DoesNotLeakIntoFreshSave(string cancelPoint)
    {
      using var session = new Session(useProfileAwareService: true);
      using var cancellation = new CancellationTokenSource();
      ProfileAwareES3Service service = session.ProfileAwareEs3;
      await service.SaveAsync("existing-key", "original");
      int notifications = 0;
      session.Coordinator.DurableSaveCompleted += _ => notifications++;
      var cancelled = new Controller
      {
        OnSave = async ct =>
        {
          await service.SaveAsync("cancelled-key", "partial", ct);
          cancellation.Cancel();
          if (cancelPoint == "controller") ct.ThrowIfCancellationRequested();
        }
      };
      session.Coordinator.RegisterLocalController(cancelled);

      try
      {
        await session.Coordinator.SaveWithReceiptAsync(cancellation.Token);
        Assert.Fail("The cancelled save must propagate cancellation.");
      }
      catch (OperationCanceledException error)
      {
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
      }
      Assert.AreEqual(0, notifications, "Cancellation must not publish a completed receipt.");
      session.Coordinator.UnregisterLocalController(cancelled);
      BeginAfterDiscard(session.Coordinator);

      var fresh = new Controller { OnSave = ct => service.SaveAsync("fresh-key", "new", ct) };
      session.Coordinator.RegisterLocalController(fresh);
      ES3FlushReceipt receipt = await session.Coordinator.SaveWithReceiptAsync();

      Assert.IsNotNull(receipt);
      Assert.AreEqual(1, notifications);
      Assert.AreEqual("original", await service.LoadAsync("existing-key", string.Empty));
      Assert.AreEqual("new", await service.LoadAsync("fresh-key", string.Empty));
      Assert.IsFalse(service.KeyExists("cancelled-key"),
        "The abandoned controller's cache must not enter the recovered session's save file.");
    }

    [Test]
    public void ProvisionalCapture_PinsParticipantsAndBlocksDiscardUntilReverseDisposal()
    {
      using var session = new Session();
      var events = new List<string>();
      var first = new CaptureController("first", events);
      var second = new CaptureController("second", events);
      session.Coordinator.RegisterLocalController(first);
      session.Coordinator.RegisterLocalController(second);

      IDisposable capture = session.Coordinator.BeginProvisionalSaveCapture();
      session.Coordinator.UnregisterLocalController(first);
      session.Coordinator.UnregisterLocalController(second);
      session.Coordinator.SwitchToGlobalSystem();
      Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator),
        "The provisional scope owns persistence until the caller restores its original profile.");

      capture.Dispose();
      capture.Dispose();

      CollectionAssert.AreEqual(new[] { "enter:first", "enter:second", "exit:second", "exit:first" }, events);
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    }

    [Test]
    public void ProvisionalCapture_EnterFailureUnwindsAllParticipantsAndPreservesOriginalFailure()
    {
      using var session = new Session();
      var events = new List<string>();
      var first = new CaptureController("first", events);
      var second = new CaptureController("second", events) { ExitFailure = new IOException("cleanup failed") };
      var original = new InvalidOperationException("participant entry failed");
      var third = new CaptureController("third", events) { EnterFailure = original };
      session.Coordinator.RegisterLocalController(first);
      session.Coordinator.RegisterLocalController(second);
      session.Coordinator.RegisterLocalController(third);

      InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => session.Coordinator.BeginProvisionalSaveCapture());

      Assert.AreSame(original, error);
      CollectionAssert.AreEqual(new[] { "enter:first", "enter:second", "enter:third", "exit:second", "exit:first" }, events);
      session.Coordinator.UnregisterLocalController(first);
      session.Coordinator.UnregisterLocalController(second);
      session.Coordinator.UnregisterLocalController(third);
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    }

    [UnityTest]
    public IEnumerator ProvisionalCapture_RejectsAnExistingSaveBeforeEnteringParticipants()
      => UniTask.ToCoroutine(async () =>
    {
      using var session = new Session();
      var completion = new UniTaskCompletionSource();
      var controller = new Controller { PendingSave = completion.Task };
      var events = new List<string>();
      session.Coordinator.RegisterLocalController(controller);
      session.Coordinator.RegisterLocalController(new CaptureController("participant", events));
      var saving = session.Coordinator.SaveWithReceiptAsync();
      IDisposable unexpected = null;
      try
      {
        Assert.AreEqual(1, controller.SaveCount);
        Assert.IsFalse(session.Coordinator.IsSaving.Value,
          "Controller awaits expose the guard gap even while the public saving flag is false.");
        Assert.Throws<InvalidOperationException>(() => unexpected = session.Coordinator.BeginProvisionalSaveCapture());
        Assert.IsEmpty(events, "Rejected admission must not change participant bindings.");
      }
      finally
      {
        unexpected?.Dispose();
        completion.TrySetResult();
        await saving;
      }

      using (session.Coordinator.BeginProvisionalSaveCapture())
        CollectionAssert.AreEqual(new[] { "enter:participant" }, events);
      CollectionAssert.AreEqual(new[] { "enter:participant", "exit:participant" }, events);
    });

    [Test]
    public void FailedLocalRestore_WithOldControllers_RejectsRecovery()
    {
      using var session = new Session();
      var failed = new Controller { FailLoad = true };
      session.Coordinator.RegisterLocalController(failed);
      session.FailRequiredLoad(failed);

      Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator));
      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value);
    }

    [Test]
    public void RemainingHydrator_RejectsRecoveryUntilUnsubscribed()
    {
      using var session = new Session();
      Func<CancellationToken, UniTask> hydrator = _ => UniTask.CompletedTask;
      session.Coordinator.PostLoadHydrate += hydrator;

      Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator));

      session.Coordinator.PostLoadHydrate -= hydrator;
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    }

    [TestCase("saving")]
    [TestCase("loading")]
    [TestCase("restore")]
    [TestCase("auto-save")]
    [TestCase("es3-saving")]
    [TestCase("es3-loading")]
    public void BusyPersistenceBoundary_RejectsRecovery(string busy)
    {
      using var session = new Session();
      switch (busy)
      {
        case "saving": session.Coordinator.IsSaving.Value = true; break;
        case "loading": session.Coordinator.IsLoading.Value = true; break;
        case "restore": session.Coordinator.RestoreInProgress.Value = true; break;
        case "auto-save": session.Coordinator.IsAutoSaveExecuting.Value = true; break;
        case "es3-saving": session.Es3.IsSaving.Value = true; break;
        case "es3-loading": session.Es3.IsLoading.Value = true; break;
      }

      Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator));
    }

    [Test]
    public void InFlightControllerLoad_RejectsRecoveryWithoutInterruptingIt()
    {
      using var session = new Session();
      var completion = new UniTaskCompletionSource();
      var controller = new Controller { PendingLoad = completion.Task };
      session.Coordinator.RegisterLocalController(controller);
      var loading = session.Coordinator.LoadAsync(new[] { controller.GetType().FullName }, _ => true);
      // Even removing the last controller cannot release an executing load gate.
      session.Coordinator.UnregisterLocalController(controller);
      try
      {
        Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator));
      }
      finally
      {
        completion.TrySetResult();
        loading.GetAwaiter().GetResult();
      }
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    }

    [UnityTest]
    public IEnumerator InFlightControllerSave_RejectsRecoveryWithoutInterruptingIt()
      => UniTask.ToCoroutine(async () =>
    {
      using var session = new Session();
      var completion = new UniTaskCompletionSource();
      var controller = new Controller { PendingSave = completion.Task };
      session.Coordinator.RegisterLocalController(controller);
      var saving = session.Coordinator.SaveAsync();
      Assert.AreEqual(1, controller.SaveCount, "The controller save must have started.");
      // An unregistered controller can still own a suspended save transaction.
      session.Coordinator.UnregisterLocalController(controller);
      try
      {
        Assert.Throws<InvalidOperationException>(() => BeginAfterDiscard(session.Coordinator));
      }
      finally
      {
        completion.TrySetResult();
        await saving;
      }
      Assert.DoesNotThrow(() => BeginAfterDiscard(session.Coordinator));
    });

    [TestCase(false)]
    [TestCase(true)]
    public void SevereTruncation_RemainsBlockedAfterLocalDiscard(bool ordered)
    {
      using var session = new Session();
      var controller = new Controller { OnLoad = () => session.Reporter.Report(SevereEntry()) };
      session.Coordinator.RegisterLocalController(controller);
      if (ordered)
        session.Coordinator.LoadAsync(new[] { controller.GetType().FullName }, _ => true).GetAwaiter().GetResult();
      else
        session.Coordinator.LoadAsync().GetAwaiter().GetResult();
      session.Coordinator.UnregisterLocalController(controller);

      BeginAfterDiscard(session.Coordinator);

      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value);
      AssertSavesRemainBlocked(session);
    }

    [Test]
    public void SevereTruncation_ThenFailedLocalRestore_CannotBeClearedByDiscard()
    {
      using var session = new Session();
      var controller = new Controller { OnLoad = () => session.Reporter.Report(SevereEntry()) };
      session.Coordinator.RegisterLocalController(controller);
      session.Coordinator.LoadAsync(new[] { controller.GetType().FullName }, _ => true).GetAwaiter().GetResult();
      controller.FailLoad = true;
      session.FailRequiredLoad(controller);
      session.Coordinator.UnregisterLocalController(controller);

      BeginAfterDiscard(session.Coordinator);

      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value);
      AssertSavesRemainBlocked(session);
    }

    [Test]
    public void FailedGlobalRestore_RemainsBlockedAfterLocalDiscard()
    {
      using var session = new Session();
      session.Coordinator.SwitchToGlobalSystem();
      session.Global.FailLoad = true;
      session.FailRequiredLoad(session.Global);

      BeginAfterDiscard(session.Coordinator);

      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value);
      AssertSavesRemainBlocked(session);
    }

    [Test]
    public async Task FailedGlobalRestore_AfterModeSwitch_RemainsBlockedAfterLocalDiscard()
    {
      using var session = new Session();
      var completion = new UniTaskCompletionSource();
      session.Global.PendingLoad = completion.Task;
      session.Coordinator.SwitchToGlobalSystem();
      var loading = session.Coordinator.LoadAsync(new[] { session.Global.GetType().FullName }, _ => true);
      Assert.AreEqual(1, session.Global.LoadCount);
      session.Coordinator.SwitchToLocalSystem();
      completion.TrySetException(new InvalidOperationException("Required global controller failed after mode switch"));
      var failures = await loading;
      Assert.AreEqual(1, failures.Count);

      BeginAfterDiscard(session.Coordinator);

      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value,
        "A global failure remains application-sticky even if local routing changed during its await.");
      AssertSavesRemainBlocked(session);
    }

    [Test]
    public async Task QueuedGlobalRestore_AfterModeSwitch_RemainsBlockedAfterLocalDiscard()
    {
      using var session = new Session();
      var completion = new UniTaskCompletionSource();
      var local = new Controller { PendingLoad = completion.Task };
      session.Coordinator.RegisterLocalController(local);
      var localLoading = session.Coordinator.LoadAsync(new[] { local.GetType().FullName }, _ => true);
      session.Coordinator.SwitchToGlobalSystem();
      session.Global.FailLoad = true;
      // The global transaction binds its system before waiting behind the local load.
      var globalLoading = session.Coordinator.LoadAsync(new[] { session.Global.GetType().FullName }, _ => true);
      session.Coordinator.SwitchToLocalSystem();
      session.Coordinator.UnregisterLocalController(local);
      completion.TrySetResult();
      await localLoading;
      var failures = await globalLoading;
      Assert.AreEqual(1, failures.Count);

      BeginAfterDiscard(session.Coordinator);

      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value,
        "A queued global failure retains the domain bound when the transaction was requested.");
      AssertSavesRemainBlocked(session);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnclassifiedExistingBlock_IsPreserved(bool followedByLocalFailure)
    {
      using var session = new Session();
      session.Coordinator.IsSaveBlocked.Value = true;
      if (followedByLocalFailure)
      {
        var failed = new Controller { FailLoad = true };
        session.Coordinator.RegisterLocalController(failed);
        session.FailRequiredLoad(failed);
        session.Coordinator.UnregisterLocalController(failed);
      }

      BeginAfterDiscard(session.Coordinator);

      Assert.IsTrue(session.Coordinator.IsSaveBlocked.Value);
      AssertSavesRemainBlocked(session);
    }

    private static TruncationEntry SevereEntry()
      => new("test", 2, 1, TruncationEntry.ReasonNewerBuild, 0, isLoadBearing: true);

    private static void AssertSavesRemainBlocked(Session session)
    {
      var fresh = new Controller();
      session.Coordinator.RegisterLocalController(fresh);
      session.Coordinator.SwitchToLocalSystem();
      session.Coordinator.SaveAsync().GetAwaiter().GetResult();
      Assert.AreEqual(0, fresh.SaveCount, "Local save writes must remain blocked.");
      session.Coordinator.SwitchToGlobalSystem();
      session.Coordinator.SaveAsync().GetAwaiter().GetResult();
      Assert.AreEqual(0, session.Global.SaveCount, "Global save writes must remain blocked.");
    }

    private static void BeginAfterDiscard(SaveSystemCoordinator coordinator)
    {
      // Before the additive seam exists, exercise the unchanged coordinator as a no-op boundary.
      // This keeps the red focused on poisoned recovery and unsafe discard, rather than compilation.
      MethodInfo method = typeof(SaveSystemCoordinator).GetMethod("BeginLocalSessionAfterDiscard");
      if (method == null) return;
      try { method.Invoke(coordinator, null); }
      catch (TargetInvocationException exception) when (exception.InnerException != null)
      { throw exception.InnerException; }
    }

    private sealed class Session : IDisposable
    {
      private readonly string directory = Path.Combine(Path.GetTempPath(), "mtk_recovery_" + Guid.NewGuid().ToString("N"));
      private readonly ES3SaveConfig config;
      private readonly GameSettingsModule settings = new();
      private readonly ProfileManager profiles;
      public MemoryES3Service Es3 { get; } = new();
      public ProfileAwareES3Service ProfileAwareEs3 { get; }
      public TruncationReporter Reporter { get; } = new();
      public Controller Global { get; } = new();
      public SaveSystemCoordinator Coordinator { get; }

      public Session(bool useProfileAwareService = false)
      {
        config = ScriptableObject.CreateInstance<ES3SaveConfig>();
        SetConfigField("usePersistentDataPath", false);
        SetConfigField("usePlatformSpecificPaths", false);
        SetConfigField("saveDirectory", directory);
        settings.AutoSave.Value = false;
        SetConfigField("gameSettings", settings);
        profiles = new ProfileManager(Es3, config);
        if (useProfileAwareService) ProfileAwareEs3 = new ProfileAwareES3Service(config, profiles);
        var globals = new SaveDomainControllerRegistry();
        globals.RegisterController(Global);
        Coordinator = new SaveSystemCoordinator((IES3Service)ProfileAwareEs3 ?? Es3,
          globals, new SaveDomainControllerRegistry(), config, profiles, Reporter);
        Coordinator.Start();
        Coordinator.SwitchToLocalSystem();
      }

      public void FailRequiredLoad(Controller controller)
      {
        var failures = Coordinator.LoadAsync(new[] { controller.GetType().FullName }, _ => true).GetAwaiter().GetResult();
        Assert.AreEqual(1, failures.Count);
        Assert.IsTrue(Coordinator.IsSaveBlocked.Value);
      }

      private void SetConfigField(string name, object value)
        => typeof(ES3SaveConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);

      public void Dispose()
      {
        Coordinator.Shutdown();
        ProfileAwareEs3?.AbortSaveBatch();
        ProfileAwareEs3?.Dispose();
        profiles.Dispose();
        settings.OnShutdown();
        UnityEngine.Object.DestroyImmediate(config);
        Es3.IsSaving.Dispose(); Es3.IsLoading.Dispose(); Es3.LastSaveTime.Dispose();
        Es3.LastLoadTime.Dispose(); Es3.SaveCounter.Dispose();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
      }
    }

    private sealed class Controller : ISaveDomainController
    {
      public ESaveDomain Domain => ESaveDomain.World;
      public bool FailLoad { get; set; }
      public int SaveCount { get; private set; }
      public int LoadCount { get; private set; }
      public Action OnLoad { get; set; }
      public Func<CancellationToken, UniTask> OnSave { get; set; }
      public UniTask PendingSave { get; set; } = UniTask.CompletedTask;
      public UniTask PendingLoad { get; set; } = UniTask.CompletedTask;
      public bool HasSaveData() => true;
      public UniTask SaveAsync(CancellationToken ct = default) { SaveCount++; return OnSave?.Invoke(ct) ?? PendingSave; }
      public UniTask LoadAsync(CancellationToken ct = default)
      {
        LoadCount++;
        if (FailLoad) throw new InvalidOperationException("Required controller failed");
        OnLoad?.Invoke();
        return PendingLoad;
      }
    }

    private sealed class CaptureController : ISaveDomainController, IProvisionalSaveCaptureParticipant
    {
      private readonly string name;
      private readonly List<string> events;
      public ESaveDomain Domain => ESaveDomain.World;
      public Exception EnterFailure { get; set; }
      public Exception ExitFailure { get; set; }

      public CaptureController(string name, List<string> events) { this.name = name; this.events = events; }
      public bool HasSaveData() => true;
      public UniTask SaveAsync(CancellationToken ct = default) => UniTask.CompletedTask;
      public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;

      public IDisposable BeginProvisionalSaveCapture()
      {
        events.Add("enter:" + name);
        if (EnterFailure != null) throw EnterFailure;
        return new CaptureLease(() =>
        {
          events.Add("exit:" + name);
          if (ExitFailure != null) throw ExitFailure;
        });
      }
    }

    private sealed class CaptureLease : IDisposable
    {
      private readonly Action dispose;
      public CaptureLease(Action dispose) => this.dispose = dispose;
      public void Dispose() => dispose();
    }
  }
}
