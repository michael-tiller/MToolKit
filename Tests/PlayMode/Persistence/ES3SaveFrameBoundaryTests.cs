using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.PlayMode.Persistence
{
  public sealed class ES3SaveFrameBoundaryTests
  {
    [SetUp]
    public void SetUp() => Application.logMessageReceived += ExpectCancellationDiagnostic;

    [TearDown]
    public void TearDown() => Application.logMessageReceived -= ExpectCancellationDiagnostic;

    [UnityTest]
    public IEnumerator ReceiptSave_SynchronousControllers_StartOnDifferentFrames()
      => AssertFrameBoundaries(requireReceipt: true).ToCoroutine();

    [UnityTest]
    public IEnumerator LegacySave_SynchronousControllers_StartOnDifferentFrames()
      => AssertFrameBoundaries(requireReceipt: false).ToCoroutine();

    [UnityTest]
    public IEnumerator ReceiptSave_CancelBetweenControllers_AbortsWithoutPublishingAndCanRetry()
      => AssertCancellationBoundary(requireReceipt: true).ToCoroutine();

    [UnityTest]
    public IEnumerator LegacySave_CancelBetweenControllers_AbortsWithoutPublishingAndCanRetry()
      => AssertCancellationBoundary(requireReceipt: false).ToCoroutine();

    private static async UniTask AssertFrameBoundaries(bool requireReceipt)
    {
      using var backend = new RecordingBackend();
      var first = new RecordingController("first", backend);
      var second = new RecordingController("second", backend);
      var third = new RecordingController("third", backend);
      var controllers = new[] { first, second, third };
      var system = new ES3GameSaveSystem(controllers, backend);

      // EarlyUpdate precedes the default UniTask Update runner. Yield(Update) can therefore
      // execute the next synchronous controller in the same frame; a real frame boundary cannot.
      await UniTask.Yield(PlayerLoopTiming.EarlyUpdate);
      ES3FlushReceipt receipt = null;
      if (requireReceipt) receipt = await system.SaveWithReceiptAsync();
      else await system.SaveAsync();

      Assert.That(second.Frame, Is.GreaterThan(first.Frame),
        "The first two synchronous serializers ran in one gameplay frame.");
      Assert.That(third.Frame, Is.GreaterThan(second.Frame));
      Assert.That(backend.FlushFrame, Is.GreaterThan(third.Frame),
        "The final controller must retain its frame boundary before the batch flush.");
      Assert.That(backend.BeginCount, Is.EqualTo(1));
      Assert.That(backend.SaveCount, Is.EqualTo(1));
      Assert.That(backend.FlushCount, Is.EqualTo(1));
      Assert.That(backend.AbortCount, Is.Zero);
      Assert.That(backend.Pending, Is.Empty);
      Assert.That(backend.Committed.Keys, Is.EquivalentTo(new[] { "first", "second", "third" }));
      foreach (RecordingController controller in controllers)
      {
        Assert.That(controller.Attempts, Is.EqualTo(1));
        Assert.That(controller.CompletionCount, Is.EqualTo(1));
        Assert.That(controller.CompletionReceipt, Is.SameAs(backend.Receipt));
      }
      if (requireReceipt) Assert.That(receipt, Is.SameAs(backend.Receipt));
    }

    private static async UniTask AssertCancellationBoundary(bool requireReceipt)
    {
      using var backend = new RecordingBackend();
      using var cancellation = new CancellationTokenSource();
      ES3SaveContext originalContext = backend.GetSaveContext();
      var first = new RecordingController("cancelled", backend);
      var second = new RecordingController("must-not-run", backend);
      var system = new ES3GameSaveSystem(new[] { first, second }, backend);

      await UniTask.Yield(PlayerLoopTiming.EarlyUpdate);
      UniTask saving = ObserveSave(system, requireReceipt, cancellation.Token);
      Assert.That(first.Attempts, Is.EqualTo(1));
      Assert.That(second.Attempts, Is.Zero);
      Assert.That(backend.Pending.ContainsKey("cancelled"), Is.True);

      // Cancel after the first serializer has returned, before the queued Update continuation.
      await UniTask.Yield(PlayerLoopTiming.PreUpdate);
      cancellation.Cancel();
      OperationCanceledException observed = null;
      try { await saving; }
      catch (OperationCanceledException error) { observed = error; }

      Assert.That(observed, Is.Not.Null);
      Assert.That(observed.CancellationToken, Is.EqualTo(cancellation.Token));
      Assert.That(second.Attempts, Is.Zero,
        "Cancellation at a frame boundary must stop before invoking another serializer.");
      Assert.That(first.CompletionCount + second.CompletionCount, Is.Zero);
      Assert.That(backend.SaveCount, Is.Zero);
      Assert.That(backend.FlushCount, Is.Zero);
      Assert.That(backend.Receipt, Is.Null);
      Assert.That(backend.AbortCount, Is.EqualTo(1));
      Assert.That(backend.Pending, Is.Empty, "The cancelled batch retained partial cached writes.");
      Assert.That(backend.Committed, Is.Empty);
      Assert.That(backend.GetSaveContext(), Is.EqualTo(originalContext));

      var fresh = new RecordingController("fresh", backend);
      system.SetDomainControllers(new[] { fresh });
      ES3FlushReceipt receipt = await system.SaveWithReceiptAsync();
      Assert.That(receipt.Context, Is.EqualTo(originalContext));
      Assert.That(backend.Committed.Keys, Is.EquivalentTo(new[] { "fresh" }),
        "The successful retry published data from the abandoned batch.");
      Assert.That(backend.Pending, Is.Empty);
      Assert.That(backend.FlushCount, Is.EqualTo(1));
      Assert.That(backend.AbortCount, Is.EqualTo(1));
      Assert.That(fresh.CompletionCount, Is.EqualTo(1));
      Assert.That(first.CompletionCount + second.CompletionCount, Is.Zero);
    }

    private static async UniTask ObserveSave(ES3GameSaveSystem system, bool requireReceipt,
      CancellationToken cancellation)
    {
      if (requireReceipt) await system.SaveWithReceiptAsync(cancellation);
      else await system.SaveAsync(cancellation);
    }

    private static void ExpectCancellationDiagnostic(string message, string stackTrace, LogType type)
    {
      // This operation intentionally reaches the existing save error diagnostic. Some hosts
      // have no Unity sink; register an expectation only when that exact cancellation is emitted.
      // All unrelated Unity errors remain failures.
      if (type == LogType.Error && message.Contains("ES3 save operation failed:")
        && message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0)
        LogAssert.Expect(type, message);
    }

    private sealed class RecordingController : ISaveDomainController, IDurableSaveCompletionHandler
    {
      private readonly string key;
      private readonly RecordingBackend backend;
      public int Attempts { get; private set; }
      public int Frame { get; private set; }
      public int CompletionCount { get; private set; }
      public ES3FlushReceipt CompletionReceipt { get; private set; }
      public ESaveDomain Domain => ESaveDomain.Settings;

      public RecordingController(string key, RecordingBackend backend)
      {
        this.key = key;
        this.backend = backend;
      }

      public UniTask SaveAsync(CancellationToken ct = default)
      {
        Attempts++;
        ct.ThrowIfCancellationRequested();
        Frame = Time.frameCount;
        return backend.SaveAsync(key, "value", ct);
      }

      public UniTask OnDurableSaveCompletedAsync(ES3FlushReceipt receipt, CancellationToken ct = default)
      {
        CompletionCount++;
        CompletionReceipt = receipt;
        return UniTask.CompletedTask;
      }

      public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
      public bool HasSaveData() => false;
    }

    private sealed class RecordingBackend : IDurableES3Service, IDisposable
    {
      private readonly ES3SaveContext context = new(Guid.NewGuid(),
        Path.Combine(Path.GetTempPath(), "MToolKitSaveFrameTests", Guid.NewGuid().ToString("N"), "save.es3"));
      public ReactiveProperty<bool> IsSaving { get; } = new(false);
      public ReactiveProperty<bool> IsLoading { get; } = new(false);
      public ReactiveProperty<string> LastSaveTime { get; } = new("Never");
      public ReactiveProperty<string> LastLoadTime { get; } = new("Never");
      public ReactiveProperty<int> SaveCounter { get; } = new(0);
      public Dictionary<string, object> Pending { get; } = new();
      public Dictionary<string, object> Committed { get; } = new();
      public int BeginCount { get; private set; }
      public int SaveCount { get; private set; }
      public int FlushCount { get; private set; }
      public int FlushFrame { get; private set; }
      public int AbortCount { get; private set; }
      public ES3FlushReceipt Receipt { get; private set; }

      public void BeginSaveBatch() => BeginCount++;
      public void EndSaveBatch() => throw new InvalidOperationException("A durable backend requires an awaited flush.");
      public void AbortSaveBatch()
      {
        AbortCount++;
        Pending.Clear();
      }

      public UniTask SaveAsync(CancellationToken ct = default)
      {
        ct.ThrowIfCancellationRequested();
        SaveCount++;
        return UniTask.CompletedTask;
      }

      public UniTask SaveAsync(string key, object value, CancellationToken ct = default)
      {
        ct.ThrowIfCancellationRequested();
        Pending[key] = value;
        return UniTask.CompletedTask;
      }

      public UniTask<ES3FlushReceipt> FlushSaveBatchAsync(CancellationToken ct = default)
      {
        ct.ThrowIfCancellationRequested();
        FlushCount++;
        FlushFrame = Time.frameCount;
        foreach (KeyValuePair<string, object> entry in Pending) Committed[entry.Key] = entry.Value;
        Pending.Clear();
        Receipt = new ES3FlushReceipt(context, new string('a', 64), DateTime.UtcNow);
        return UniTask.FromResult(Receipt);
      }

      public ES3SaveContext GetSaveContext() => context;
      public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
      public UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
        => UniTask.FromResult(Committed.TryGetValue(key, out object value) ? (T)value : defaultValue);
      public bool KeyExists(string key) => Committed.ContainsKey(key);
      public void DeleteKey(string key) => Committed.Remove(key);
      public void DeleteFile() => Committed.Clear();
      public string GetSaveFormatVersion() => "test";
      public string GetSavedFormatVersion() => "test";
      public bool CreateBackup() => false;
      public bool RestoreFromBackup() => false;
      public string[] GetAvailableBackups() => Array.Empty<string>();

      public void Dispose()
      {
        IsSaving.Dispose();
        IsLoading.Dispose();
        LastSaveTime.Dispose();
        LastLoadTime.Dispose();
        SaveCounter.Dispose();
      }
    }
  }
}
