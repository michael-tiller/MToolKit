using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.ErrorSystem.Messages;
using MToolKit.Runtime.MessageBus;
using MToolKit.Tests.Editor.MessageBus.Support;
using NUnit.Framework;
using VContainer;

namespace MToolKit.Tests.Editor.MessageBus
{
  /// <summary>
  ///   Covers the pre/post interception seam: band ordering, cancellation, failure reporting and its
  ///   recursion stops, reentrancy, scope isolation and lifecycle.
  ///
  ///   <para>Failures are captured through <see cref="InterceptorFailureReporter.OverrideScope" /> rather
  ///   than a Serilog sink: the reporter's <c>Lazy&lt;ILogger&gt;</c> is one-shot and never reset, so a
  ///   log-sink assertion would be order-dependent on any earlier fixture that evaluated it.</para>
  /// </summary>
  [TestFixture]
  public class MessageInterceptorTests
  {
    private IObjectResolver resolver;
    private readonly List<IDisposable> subscriptions = new();

    [SetUp]
    public void SetUp()
    {
      ResetAll();

      var builder = new ContainerBuilder();
      MessagePipeOptions options = builder.RegisterMessagePipe();
      builder.RegisterMessageBroker<CancellableCommand>(options);
      builder.RegisterMessageBroker<OtherCancellableCommand>(options);
      builder.RegisterMessageBroker<PlainNotification>(options);
      builder.RegisterMessageBroker<ErrorRequestMessage>(options);
      builder.RegisterMessageBroker<object>(options);
      resolver = builder.Build();

      GameMessageBroker.Initialize(resolver);
      GlobalAsyncMessageBroker.Initialize(resolver);
    }

    [TearDown]
    public void TearDown()
    {
      foreach (IDisposable s in subscriptions) s.Dispose();
      subscriptions.Clear();
      ResetAll();
      resolver?.Dispose();
      resolver = null;
    }

    private static void ResetAll()
    {
      GameMessageBroker.Reset();
      GameMessageBroker.ResetInterceptors();
      GlobalAsyncMessageBroker.Reset();
      GlobalAsyncMessageBroker.ResetInterceptors();
    }

    /// <summary>Tracks a subscription so TearDown disposes it even if the test throws.</summary>
    private T Track<T>(T disposable) where T : IDisposable
    {
      subscriptions.Add(disposable);
      return disposable;
    }

    private void OnGame<T>(Action<T> handler) =>
      Track(GameMessageBroker.GetSubscriber<T>().Subscribe(handler));

    // ================================================================ ordering

    [Test]
    public void PreOps_RunInAscendingBandOrder()
    {
      var order = new List<string>();
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("last"), HandlerPriority.Last));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("first"), HandlerPriority.First));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("normal")));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(order, Is.EqualTo(new[] { "first", "normal", "last" }));
    }

    [Test]
    public void WithinABand_RegistrationOrderIsExecutionOrder()
    {
      // The prior art iterated each band backwards, so the LAST registered ran FIRST — inverting load order.
      var order = new List<string>();
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("a"), HandlerPriority.First));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("b"), HandlerPriority.First));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("c"), HandlerPriority.First));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(order, Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void PostOps_RunAfterSubscribers_InBandOrder()
    {
      var order = new List<string>();
      OnGame<PlainNotification>(_ => order.Add("subscriber"));
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => order.Add("post-last"), HandlerPriority.Last));
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => order.Add("post-first"), HandlerPriority.First));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(order, Is.EqualTo(new[] { "subscriber", "post-first", "post-last" }));
    }

    // ================================================================ cancellation

    [Test]
    public void Cancel_PreventsDeliveryToSubscribers()
    {
      int delivered = 0;
      OnGame<CancellableCommand>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(m => m.Cancel("nope"), HandlerPriority.First));

      GameMessageBroker.Publish(new CancellableCommand());

      Assert.That(delivered, Is.Zero);
    }

    [Test]
    public void Cancel_StopsRemainingPreOps_IncludingTheRestOfItsOwnBand()
    {
      var ran = new List<string>();
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(m => { ran.Add("canceller"); m.Cancel(); }, HandlerPriority.First));
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(_ => ran.Add("same-band"), HandlerPriority.First));
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(_ => ran.Add("later-band"), HandlerPriority.Normal));

      GameMessageBroker.Publish(new CancellableCommand());

      Assert.That(ran, Is.EqualTo(new[] { "canceller" }), "cancel must stop its own band too, not just later ones");
    }

    [Test]
    public void PostOps_RunAfterCancel_AndObserveTheReason()
    {
      bool postRan = false;
      bool sawCancelled = false;
      string sawReason = null;
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(m => m.Cancel("vetoed"), HandlerPriority.First));
      Track(GameMessageBroker.InterceptPost<CancellableCommand>(m =>
      {
        postRan = true;
        sawCancelled = m.IsCancelled;
        sawReason = m.CancelReason;
      }));

      GameMessageBroker.Publish(new CancellableCommand());

      Assert.That(postRan, Is.True, "post ops must run even when the message was cancelled");
      Assert.That(sawCancelled, Is.True);
      Assert.That(sawReason, Is.EqualTo("vetoed"));
    }

    [Test]
    public void PostOps_RunWhenASubscriberThrows_AndTheExceptionStillReachesTheCaller()
    {
      bool postRan = false;
      OnGame<PlainNotification>(_ => throw new InvalidOperationException("subscriber blew up"));
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => postRan = true));

      Assert.Throws<InvalidOperationException>(() => GameMessageBroker.Publish(new PlainNotification()));
      Assert.That(postRan, Is.True, "post ops run in a finally, so a throwing subscriber cannot skip them");
    }

    [Test]
    public void AlreadyCancelledInstance_SkipsPreOpsAndSubscribers_ButStillRunsPostOps()
    {
      // Sticky cancellation, with ZERO pre registrations — the case a registry-miss early-out would let slip.
      int delivered = 0;
      bool postRan = false;
      OnGame<CancellableCommand>(_ => delivered++);
      Track(GameMessageBroker.InterceptPost<CancellableCommand>(_ => postRan = true));

      var message = new CancellableCommand();
      message.Cancel("pre-cancelled");
      GameMessageBroker.Publish(message);

      Assert.That(delivered, Is.Zero);
      Assert.That(postRan, Is.True);
    }

    [Test]
    public void CancelThenThrow_IsReportedAndStillStopsTheChain()
    {
      var reports = new List<string>();
      var ran = new List<string>();
      using IDisposable scope = InterceptorFailureReporter.OverrideScope((owner, t, e) => reports.Add(owner));

      int delivered = 0;
      OnGame<CancellableCommand>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(m =>
      {
        ran.Add("thrower");
        m.Cancel("cancelled before throwing");
        throw new InvalidOperationException("boom");
      }, HandlerPriority.First, "mod.a"));
      Track(GameMessageBroker.InterceptPre<CancellableCommand>(_ => ran.Add("later"), HandlerPriority.Normal));

      GameMessageBroker.Publish(new CancellableCommand());

      Assert.That(reports, Is.EqualTo(new[] { "mod.a" }), "the throw is reported");
      Assert.That(ran, Is.EqualTo(new[] { "thrower" }), "and the cancel still stops the chain");
      Assert.That(delivered, Is.Zero);
    }

    [Test]
    public void NonCancellableTypes_PublishNormallyWithOpsAttached()
    {
      var order = new List<string>();
      OnGame<PlainNotification>(_ => order.Add("subscriber"));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => order.Add("pre")));
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => order.Add("post")));

      GameMessageBroker.Publish(new PlainNotification { Value = 3 });

      Assert.That(order, Is.EqualTo(new[] { "pre", "subscriber", "post" }));
    }

    // ================================================================ declared vs runtime type

    [Test]
    public void CancellableClassPublishedAsObject_UsesObjectKeyedInterceptors_AndIsStillCancellable()
    {
      int delivered = 0;
      OnGame<object>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<object>(m => ((CancellableCommand)m).Cancel("via object")));

      var message = new CancellableCommand();
      GameMessageBroker.Publish<object>(message);

      Assert.That(message.IsCancelled, Is.True, "cancellation is decided from the runtime instance");
      Assert.That(delivered, Is.Zero);
    }

    [Test]
    public void BoxedCancellableStruct_IsRejectedAtDispatch()
    {
      // The registration-time typeof(T) guard cannot see these: typeof(object) is not a value type.
      Track(GameMessageBroker.InterceptPre<object>(_ => { }));

      Assert.Throws<ArgumentException>(() => GameMessageBroker.Publish<object>(new BadCancellableStruct()));
    }

    // ================================================================ registration validation

    [Test]
    public void RegisteringForACancellableValueType_Throws()
    {
      Assert.Throws<ArgumentException>(() => GameMessageBroker.InterceptPre<BadCancellableStruct>(_ => { }));
      Assert.Throws<ArgumentException>(() => GameMessageBroker.InterceptPost<BadCancellableStruct>(_ => { }));
    }

    [Test]
    public void RegisteringANullOp_Throws()
    {
      Assert.Throws<ArgumentNullException>(() => GameMessageBroker.InterceptPre<PlainNotification>(null));
      Assert.Throws<ArgumentNullException>(() => GameMessageBroker.InterceptPost<PlainNotification>(null));
    }

    [Test]
    public void NullOpWins_WhenBothOpAndTypeAreInvalid()
    {
      Assert.Throws<ArgumentNullException>(() => GameMessageBroker.InterceptPre<BadCancellableStruct>(null));
    }

    // ================================================================ failure reporting

    [Test]
    public void AThrowingPreOp_DoesNotBreakTheChain_AndIsReportedWithItsOwner()
    {
      var reports = new List<(string Owner, Type Type)>();
      using IDisposable scope = InterceptorFailureReporter.OverrideScope((owner, type, e) => reports.Add((owner, type)));

      bool laterRan = false;
      int delivered = 0;
      OnGame<PlainNotification>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => throw new InvalidOperationException("boom"),
        HandlerPriority.First, "mod.broken"));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => laterRan = true, HandlerPriority.Normal));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(laterRan, Is.True, "a throwing op must not abort the remaining chain");
      Assert.That(delivered, Is.EqualTo(1), "and the publish still happens");
      Assert.That(reports.Count, Is.EqualTo(1));
      Assert.That(reports[0].Owner, Is.EqualTo("mod.broken"));
      Assert.That(reports[0].Type, Is.EqualTo(typeof(PlainNotification)));
    }

    [Test]
    public void AThrowingOpWithNoOwner_ReportsTheUnknownFallback()
    {
      var owners = new List<string>();
      using IDisposable scope = InterceptorFailureReporter.OverrideScope((owner, t, e) => owners.Add(owner));

      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => throw new InvalidOperationException("boom")));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(owners, Is.EqualTo(new[] { "<unknown>" }));
    }

    [Test]
    public void AThrowingInterceptorOnErrorRequestMessage_IsNotReported()
    {
      // Type suppression at the catch site: reporting an ErrorRequestMessage failure BY publishing an
      // ErrorRequestMessage is the direct recursion cycle.
      int reportCount = 0;
      using IDisposable scope = InterceptorFailureReporter.OverrideScope((o, t, e) => reportCount++);

      Track(GlobalAsyncMessageBroker.InterceptPre<ErrorRequestMessage>(_ => throw new InvalidOperationException("boom")));

      Assert.DoesNotThrow(() => GlobalAsyncMessageBroker.Publish(new ErrorRequestMessage("original")));
      Assert.That(reportCount, Is.Zero, "failures on the error type itself must be suppressed, not reported");
    }

    [Test]
    public void AThrowingReporterOverride_DoesNotBreakTheChain()
    {
      using IDisposable scope = InterceptorFailureReporter.OverrideScope((o, t, e) => throw new InvalidOperationException("reporter blew up"));

      bool laterRan = false;
      int delivered = 0;
      OnGame<PlainNotification>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => throw new InvalidOperationException("boom")));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => laterRan = true));

      Assert.DoesNotThrow(() => GameMessageBroker.Publish(new PlainNotification()));
      Assert.That(laterRan, Is.True);
      Assert.That(delivered, Is.EqualTo(1));
    }

    [Test]
    public void AFailingReportPublish_DoesNotBreakTheChain()
    {
      // No override, and ErrorRequestMessage unregistered in a fresh scope, so the report publish throws.
      ResetAll();
      resolver.Dispose();

      var builder = new ContainerBuilder();
      MessagePipeOptions options = builder.RegisterMessagePipe();
      builder.RegisterMessageBroker<PlainNotification>(options);
      resolver = builder.Build();
      GameMessageBroker.Initialize(resolver);
      GlobalAsyncMessageBroker.Initialize(resolver);

      bool laterRan = false;
      int delivered = 0;
      OnGame<PlainNotification>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => throw new InvalidOperationException("boom")));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => laterRan = true));

      Assert.DoesNotThrow(() => GameMessageBroker.Publish(new PlainNotification()));
      Assert.That(laterRan, Is.True);
      Assert.That(delivered, Is.EqualTo(1));
    }

    [Test]
    public void IndirectCrossScopeCycle_ReportsOnceAndDoesNotRecurse()
    {
      // Deliberately runs the REAL ErrorSystem path: an active override would replace the publish, so no
      // ErrorRequestMessage would reach the subscriber and the cycle could never form.
      // Game A throws -> report publishes ErrorRequestMessage -> its subscriber publishes B ->
      // B's interceptor throws -> that second Report lands with reporting == true.
      int errorMessages = 0;
      OnGame<PlainNotification>(_ => { });
      Track(GlobalAsyncMessageBroker.GetSubscriber<ErrorRequestMessage>().Subscribe(_ =>
      {
        errorMessages++;
        GameMessageBroker.Publish(new OtherCancellableCommand());
      }));

      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => throw new InvalidOperationException("first"),
        HandlerPriority.First, "mod.a"));
      Track(GameMessageBroker.InterceptPre<OtherCancellableCommand>(_ => throw new InvalidOperationException("second"),
        HandlerPriority.First, "mod.b"));

      Assert.DoesNotThrow(() => GameMessageBroker.Publish(new PlainNotification()));
      Assert.That(errorMessages, Is.EqualTo(1), "the depth guard must swallow the second report");
    }

    // ================================================================ disposal

    [Test]
    public void DisposingARegistration_Unregisters_AndDisposingTwiceIsANoOp()
    {
      int ran = 0;
      IDisposable token = GameMessageBroker.InterceptPre<PlainNotification>(_ => ran++);

      GameMessageBroker.Publish(new PlainNotification());
      token.Dispose();
      token.Dispose();
      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(ran, Is.EqualTo(1));
    }

    [Test]
    public void APostOpDisposingALaterPostOp_PreventsItRunningInTheSamePublish()
    {
      // Snapshot-only semantics would let the disposed op still run; the per-registration check prevents it.
      bool laterRan = false;
      IDisposable later = null;
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => later?.Dispose(), HandlerPriority.First));
      later = Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => laterRan = true, HandlerPriority.Last));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(laterRan, Is.False, "disposal is honoured immediately, not deferred to the next publish");
    }

    // ================================================================ reentrancy

    [Test]
    public void NestedSameTypePublish_DeliversTwice_ButRunsInterceptorsOnce()
    {
      int delivered = 0;
      int preRuns = 0;
      int postRuns = 0;
      bool nested = false;
      OnGame<PlainNotification>(_ => delivered++);
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ =>
      {
        preRuns++;
        if (nested) return;
        nested = true;
        GameMessageBroker.Publish(new PlainNotification());
      }));
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => postRuns++));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(delivered, Is.EqualTo(2), "the nested message is a real publish and reaches subscribers");
      Assert.That(preRuns, Is.EqualTo(1), "but no interceptor runs for it");
      Assert.That(postRuns, Is.EqualTo(1));
    }

    [Test]
    public void NestedDifferentTypePublish_IsInterceptedNormally()
    {
      int otherPreRuns = 0;
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => GameMessageBroker.Publish(new OtherCancellableCommand())));
      Track(GameMessageBroker.InterceptPre<OtherCancellableCommand>(_ => otherPreRuns++));

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(otherPreRuns, Is.EqualTo(1), "different-type reentrancy is normal and must stay intercepted");
    }

    [Test]
    public void AfterAThrowingPreOp_TheGuardIsCleared_AndLaterPublishesAreStillIntercepted()
    {
      using IDisposable scope = InterceptorFailureReporter.OverrideScope((o, t, e) => { });

      int runs = 0;
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ =>
      {
        runs++;
        throw new InvalidOperationException("boom");
      }));

      GameMessageBroker.Publish(new PlainNotification());
      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(runs, Is.EqualTo(2), "the in-flight set must be cleared in a finally");
    }

    // ================================================================ scope isolation

    [Test]
    public void GameInterceptors_DoNotFireOnGlobalPublishes()
    {
      int gameOps = 0;
      int globalOps = 0;
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => gameOps++));
      Track(GlobalAsyncMessageBroker.InterceptPre<PlainNotification>(_ => globalOps++));

      GlobalAsyncMessageBroker.Publish(new PlainNotification());

      Assert.That(globalOps, Is.EqualTo(1));
      Assert.That(gameOps, Is.Zero, "registries are per closed core, so scopes must not cross");
    }

    // ================================================================ lifecycle

    [Test]
    public void Reset_DoesNotClearRegistrations()
    {
      int ran = 0;
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => ran++));

      GameMessageBroker.Reset();
      GameMessageBroker.Initialize(resolver);
      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(ran, Is.EqualTo(1), "broker Reset is resolver lifecycle only; it must not destroy registrations");
    }

    [Test]
    public void ResetInterceptors_ClearsWithoutTearingDownTheBroker()
    {
      int ran = 0;
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => ran++));

      GameMessageBroker.ResetInterceptors();

      Assert.That(GameMessageBroker.IsAvailable(), Is.True, "the broker itself stays up");
      GameMessageBroker.Publish(new PlainNotification());
      Assert.That(ran, Is.Zero);
    }

    [Test]
    public void DisposingATokenAfterResetInterceptors_IsAHarmlessNoOp()
    {
      IDisposable token = GameMessageBroker.InterceptPre<PlainNotification>(_ => { });

      GameMessageBroker.ResetInterceptors();

      Assert.DoesNotThrow(() => token.Dispose());
    }

    [Test]
    public void AnUnavailableBroker_RunsNoInterceptorsAtAll()
    {
      int pre = 0;
      int post = 0;
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => pre++));
      Track(GameMessageBroker.InterceptPost<PlainNotification>(_ => post++));

      GameMessageBroker.Reset();
      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(pre, Is.Zero);
      Assert.That(post, Is.Zero);
    }

    // ================================================================ override scope

    [Test]
    public void OverrideScope_RejectsNull()
    {
      Assert.Throws<ArgumentNullException>(() => InterceptorFailureReporter.OverrideScope(null));
    }

    [Test]
    public void OverrideScope_RejectsOverlappingScopes()
    {
      using IDisposable first = InterceptorFailureReporter.OverrideScope((o, t, e) => { });

      Assert.Throws<InvalidOperationException>(() => InterceptorFailureReporter.OverrideScope((o, t, e) => { }));
    }

    [Test]
    public void OverrideScope_DisposeIsIdempotent()
    {
      IDisposable scope = InterceptorFailureReporter.OverrideScope((o, t, e) => { });
      scope.Dispose();

      Assert.DoesNotThrow(() => scope.Dispose());
      // Proven released: a fresh scope can be acquired.
      Assert.DoesNotThrow(() =>
      {
        using IDisposable next = InterceptorFailureReporter.OverrideScope((o, t, e) => { });
      });
    }

    // ================================================================ concurrency (barrier-driven)

    [Test]
    public void DisposalFromAWorkerThread_IsVisibleToAnInFlightDispatch()
    {
      // The exact race a missing memory barrier would let through: dispatch reads Disposed OUTSIDE the lock,
      // so a lock taken by the disposing thread establishes nothing for it without Volatile/Interlocked.
      using var opStarted = new ManualResetEventSlim(false);
      using var disposeDone = new ManualResetEventSlim(false);

      bool laterRan = false;
      IDisposable later = null;

      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ =>
      {
        opStarted.Set();
        disposeDone.Wait(TimeSpan.FromSeconds(5));
      }, HandlerPriority.First));
      later = Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => laterRan = true, HandlerPriority.Last));

      Task worker = Task.Run(() =>
      {
        opStarted.Wait(TimeSpan.FromSeconds(5));
        later.Dispose();
        disposeDone.Set();
      });

      GameMessageBroker.Publish(new PlainNotification());
      worker.Wait(TimeSpan.FromSeconds(5));

      Assert.That(laterRan, Is.False, "a worker-thread disposal must be honoured by the in-flight snapshot");
    }

    [Test]
    public void ResetFromAWorkerThread_IsVisibleToAnInFlightDispatch()
    {
      using var opStarted = new ManualResetEventSlim(false);
      using var resetDone = new ManualResetEventSlim(false);

      bool laterRan = false;

      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ =>
      {
        opStarted.Set();
        resetDone.Wait(TimeSpan.FromSeconds(5));
      }, HandlerPriority.First));
      Track(GameMessageBroker.InterceptPre<PlainNotification>(_ => laterRan = true, HandlerPriority.Last));

      Task worker = Task.Run(() =>
      {
        opStarted.Wait(TimeSpan.FromSeconds(5));
        GameMessageBroker.ResetInterceptors();
        resetDone.Set();
      });

      GameMessageBroker.Publish(new PlainNotification());
      worker.Wait(TimeSpan.FromSeconds(5));

      Assert.That(laterRan, Is.False, "Reset marks registrations disposed before clearing, disarming live snapshots");
    }

    [Test]
    public void ConcurrentRegistrations_AllSurvive_AndEachRunsExactlyOnce()
    {
      // Sequence allocation shares the insertion lock, so concurrent registration cannot lose or duplicate
      // an entry. Which interleaving wins is lock-acquisition order and is not assertable — that every
      // registration survives and fires once is.
      const int threads = 8;
      var tokens = new IDisposable[threads];
      var seen = new List<int>();
      var startLine = new ManualResetEventSlim(false);

      var workers = new Task[threads];
      for (int i = 0; i < threads; i++)
      {
        int index = i;
        workers[i] = Task.Run(() =>
        {
          startLine.Wait(TimeSpan.FromSeconds(5));
          tokens[index] = GameMessageBroker.InterceptPre<PlainNotification>(
            _ => { lock (seen) seen.Add(index); }, HandlerPriority.Normal, $"owner{index}");
        });
      }

      startLine.Set();
      Task.WaitAll(workers, TimeSpan.FromSeconds(10));
      foreach (IDisposable t in tokens) Track(t);
      startLine.Dispose();

      GameMessageBroker.Publish(new PlainNotification());

      Assert.That(tokens.All(t => t != null), Is.True, "every concurrent registration must have succeeded");
      Assert.That(seen.Count, Is.EqualTo(threads), "no registration lost or duplicated");
      Assert.That(seen.OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, threads)), "each ran exactly once");
    }
  }
}
