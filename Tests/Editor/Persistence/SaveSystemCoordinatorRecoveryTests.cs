using System;
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

namespace MToolKit.Tests.Editor.Persistence
{
  /// <summary>Recovery must discard the local scope without weakening application save safety.</summary>
  [TestFixture]
  public sealed class SaveSystemCoordinatorRecoveryTests
  {
    [Test]
    public async Task FailedLocalRestore_AfterDiscard_CanLoadAndSaveWithFreshControllers()
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
    }

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

    [Test]
    public async Task InFlightControllerSave_RejectsRecoveryWithoutInterruptingIt()
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
    }

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
      public TruncationReporter Reporter { get; } = new();
      public Controller Global { get; } = new();
      public SaveSystemCoordinator Coordinator { get; }

      public Session()
      {
        config = ScriptableObject.CreateInstance<ES3SaveConfig>();
        SetConfigField("usePersistentDataPath", false);
        SetConfigField("usePlatformSpecificPaths", false);
        SetConfigField("saveDirectory", directory);
        settings.AutoSave.Value = false;
        SetConfigField("gameSettings", settings);
        profiles = new ProfileManager(Es3, config);
        var globals = new SaveDomainControllerRegistry();
        globals.RegisterController(Global);
        Coordinator = new SaveSystemCoordinator(Es3, globals, new SaveDomainControllerRegistry(), config, profiles, Reporter);
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
      public UniTask PendingSave { get; set; } = UniTask.CompletedTask;
      public UniTask PendingLoad { get; set; } = UniTask.CompletedTask;
      public bool HasSaveData() => true;
      public UniTask SaveAsync(CancellationToken ct = default) { SaveCount++; return PendingSave; }
      public UniTask LoadAsync(CancellationToken ct = default)
      {
        LoadCount++;
        if (FailLoad) throw new InvalidOperationException("Required controller failed");
        OnLoad?.Invoke();
        return PendingLoad;
      }
    }
  }
}
