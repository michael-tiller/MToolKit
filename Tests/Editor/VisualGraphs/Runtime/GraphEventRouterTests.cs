using System;
using System.Linq;
using System.Threading;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  /// <summary>
  ///   Characterization of <see cref="GraphEventRouter" />. The load-bearing pin is ADDITIVE DELIVERY:
  ///   routing delivers to BOTH the exact (type, domain) bucket AND the empty-domain ("any") bucket, deduplicated
  ///   by runner reference identity, dispatched in overall registration order — an empty-domain subscriber is
  ///   NEVER suppressed by an exact-domain subscriber existing for that domain (this replaces the prior
  ///   exact-ELSE-wildcard suppression semantic; see MToolKit CHANGELOG for the deliberate flip). Also pins
  ///   exact-type-only matching (no inheritance), registration delivery, and pre-cancellation. Routing keys on the
  ///   runtime message type and dispatches synchronously (fakes return UniTask.CompletedTask), so
  ///   GetAwaiter().GetResult() observes real delivery.
  /// </summary>
  [TestFixture]
  public sealed class GraphEventRouterTests
  {
    [SetUp]
    public void SetUp()
    {
      // The router logs via Serilog, not Unity Debug; ignore so an incidental log can't fail an assertion
      // that is made on observable delivery (HandleMessageAsync call counts), not on logs.
      LogAssert.ignoreFailingMessages = true;
      router = new GraphEventRouter();
    }

    private GraphEventRouter router;

    private static FakeGraphRunner RunnerWith(string graphId, Type messageType, string domain)
    {
      return new FakeGraphRunner(graphId, GraphDefBuilder.New().Id(graphId).Subscribe(messageType, domain).Build());
    }

    private void Route(object message, string domain)
    {
      router.RouteAsync((MToolKit.Runtime.MessageBus.Interfaces.IGameMessage)message, domain, CancellationToken.None)
        .GetAwaiter().GetResult();
    }

    [Test]
    public void RegisterRunner_Null_ThrowsArgumentNull()
    {
      Assert.Throws<ArgumentNullException>(() => router.RegisterRunner(null));
    }

    [Test]
    public void Route_ExactTypeAndDomain_Delivers()
    {
      var runner = RunnerWith("g1", typeof(TestMessageA), "Quest");
      router.RegisterRunner(runner);

      var message = new TestMessageA();
      Route(message, "Quest");

      Assert.That(runner.HandleMessageAsyncCallCount, Is.EqualTo(1));
      Assert.That(runner.Handled.Single().message, Is.SameAs(message));
      Assert.That(runner.Handled.Single().domain, Is.EqualTo("Quest"));
    }

    [Test]
    public void Route_ExactMatch_AlsoDeliversToEmptyDomainSubscriber()
    {
      var exact = RunnerWith("exact", typeof(TestMessageA), "Quest");
      var anyDomain = RunnerWith("any", typeof(TestMessageA), null);
      router.RegisterRunner(exact);
      router.RegisterRunner(anyDomain);

      Route(new TestMessageA(), "Quest");

      Assert.That(exact.HandleMessageAsyncCallCount, Is.EqualTo(1), "exact-domain subscriber receives it");
      Assert.That(anyDomain.HandleMessageAsyncCallCount, Is.EqualTo(1),
        "additive delivery: the empty-domain ('any') subscriber is NEVER suppressed by an exact-domain subscriber");
    }

    [Test]
    public void Route_TwoGraphsDifferentEventNameDomains_EachFiresOnlyOnOwn()
    {
      var graphA = RunnerWith("graphA", typeof(TestNamedMessage), "a");
      var graphB = RunnerWith("graphB", typeof(TestNamedMessage), "b");
      router.RegisterRunner(graphA);
      router.RegisterRunner(graphB);

      Route(new TestNamedMessage("a"), "a");

      Assert.That(graphA.HandleMessageAsyncCallCount, Is.EqualTo(1), "subscribed to event 'a', receives it");
      Assert.That(graphB.HandleMessageAsyncCallCount, Is.EqualTo(0), "subscribed to event 'b', must not fire on 'a'");
    }

    [Test]
    public void Route_ThreeOverlappingGraphs_Independent()
    {
      var graphA = RunnerWith("graphA", typeof(TestNamedMessage), "a");
      var graphB = RunnerWith("graphB", typeof(TestNamedMessage), "b");
      var graphUnfiltered = RunnerWith("graphUnfiltered", typeof(TestNamedMessage), null);
      router.RegisterRunner(graphA);
      router.RegisterRunner(graphB);
      router.RegisterRunner(graphUnfiltered);

      Route(new TestNamedMessage("a"), "a");

      Assert.That(graphA.HandleMessageAsyncCallCount, Is.EqualTo(1), "exact event-name match fires");
      Assert.That(graphB.HandleMessageAsyncCallCount, Is.EqualTo(0), "graph filtered to a different event name stays silent");
      Assert.That(graphUnfiltered.HandleMessageAsyncCallCount, Is.EqualTo(1), "unfiltered graph receives all named events");
    }

    [Test]
    public void Route_UnfilteredSubscriber_ReceivesAllNamedEvents()
    {
      var unfiltered = RunnerWith("any", typeof(TestNamedMessage), null);
      router.RegisterRunner(unfiltered);

      Route(new TestNamedMessage("a"), "a");
      Route(new TestNamedMessage("b"), "b");

      Assert.That(unfiltered.HandleMessageAsyncCallCount, Is.EqualTo(2),
        "backward compat: a graph without an EventName filter still receives all events of the type");
    }

    [Test]
    public void Route_FilterIsCaseSensitiveOrdinal()
    {
      var lower = RunnerWith("lower", typeof(TestNamedMessage), "a");
      router.RegisterRunner(lower);

      Route(new TestNamedMessage("A"), "A");

      Assert.That(lower.HandleMessageAsyncCallCount, Is.EqualTo(0),
        "domain/event-name matching is exact ordinal (case-sensitive): filter 'a' must not match domain 'A'");
    }

    [Test]
    public void Route_SameRunnerEmptyAndFilteredSubs_DispatchedOnce()
    {
      var def = GraphDefBuilder.New().Id("g1")
        .Subscribe(typeof(TestNamedMessage), "a")
        .Subscribe(typeof(TestNamedMessage), null)
        .Build();
      var runner = new FakeGraphRunner("g1", def);
      router.RegisterRunner(runner);

      Route(new TestNamedMessage("a"), "a");

      Assert.That(runner.HandleMessageAsyncCallCount, Is.EqualTo(1),
        "a runner matching via multiple subscriptions (exact + wildcard) is dispatched exactly once, not twice");
    }

    [Test]
    public void Route_AdditiveDelivery_PreservesRegistrationOrder()
    {
      var order = new System.Collections.Generic.List<string>();
      var wildcard = RunnerWith("wildcard", typeof(TestMessageA), null);
      var exact = RunnerWith("exact", typeof(TestMessageA), "Quest");
      wildcard.OnHandled = () => order.Add("wildcard");
      exact.OnHandled = () => order.Add("exact");

      // Wildcard registered FIRST, exact SECOND — additive delivery must dispatch in overall
      // registration order, not exact-bucket-then-wildcard-bucket concatenation.
      router.RegisterRunner(wildcard);
      router.RegisterRunner(exact);

      Route(new TestMessageA(), "Quest");

      Assert.That(order, Is.EqualTo(new[] { "wildcard", "exact" }),
        "dispatch follows overall registration order, not bucket concatenation order");
      Assert.That(wildcard.HandleMessageAsyncCallCount, Is.EqualTo(1));
      Assert.That(exact.HandleMessageAsyncCallCount, Is.EqualTo(1));
    }

    [Test]
    public void Route_NoExactBucket_FallsBackToEmptyDomainSubscriber()
    {
      var anyDomain = RunnerWith("any", typeof(TestMessageA), null);
      router.RegisterRunner(anyDomain);

      Route(new TestMessageA(), "Quest");

      Assert.That(anyDomain.HandleMessageAsyncCallCount, Is.EqualTo(1),
        "with no exact (type, 'Quest') bucket, routing falls back to the empty-domain bucket");
    }

    [Test]
    public void Route_NullDomain_TreatedAsEmptyString()
    {
      var anyDomain = RunnerWith("any", typeof(TestMessageA), null);
      router.RegisterRunner(anyDomain);

      Route(new TestMessageA(), null);

      Assert.That(anyDomain.HandleMessageAsyncCallCount, Is.EqualTo(1));
    }

    [Test]
    public void Route_ExactTypeOnly_NoInheritanceMatch()
    {
      var baseSub = RunnerWith("base", typeof(TestMessageA), "Quest");
      router.RegisterRunner(baseSub);

      // DerivedTestMessage : TestMessageA — routing keys on the concrete runtime type, so a base-type
      // subscription must NOT receive a derived message.
      Route(new DerivedTestMessage(), "Quest");

      Assert.That(baseSub.HandleMessageAsyncCallCount, Is.EqualTo(0),
        "subscription to the base type does not receive the derived message (no inheritance matching)");
    }

    [Test]
    public void Route_MultipleSubscribers_AllReceive()
    {
      var r1 = RunnerWith("g1", typeof(TestMessageA), "Quest");
      var r2 = RunnerWith("g2", typeof(TestMessageA), "Quest");
      router.RegisterRunner(r1);
      router.RegisterRunner(r2);

      Route(new TestMessageA(), "Quest");

      Assert.That(r1.HandleMessageAsyncCallCount, Is.EqualTo(1));
      Assert.That(r2.HandleMessageAsyncCallCount, Is.EqualTo(1));
    }

    [Test]
    public void RegisterRunner_InvalidSubscription_NotIndexed()
    {
      var runner = new FakeGraphRunner("g1",
        GraphDefBuilder.New().Id("g1").SubscribeInvalid("Quest").Build());
      router.RegisterRunner(runner);

      Route(new TestMessageA(), "Quest");

      Assert.That(runner.HandleMessageAsyncCallCount, Is.EqualTo(0),
        "a subscription whose MessageType is invalid (Type == null) is skipped during indexing");
      Assert.That(router.GetSubscribedMessageTypes(), Is.Empty);
    }

    [Test]
    public void Route_NullMessage_NoDelivery()
    {
      var runner = RunnerWith("g1", typeof(TestMessageA), "Quest");
      router.RegisterRunner(runner);

      Route(null, "Quest");

      Assert.That(runner.HandleMessageAsyncCallCount, Is.EqualTo(0));
    }

    [Test]
    public void Route_PreCancelledToken_InvokesNoRunners()
    {
      var runner = RunnerWith("g1", typeof(TestMessageA), "Quest");
      router.RegisterRunner(runner);

      var cts = new CancellationTokenSource();
      cts.Cancel();
      router.RouteAsync(new TestMessageA(), "Quest", cts.Token).GetAwaiter().GetResult();

      Assert.That(runner.HandleMessageAsyncCallCount, Is.EqualTo(0),
        "the routing loop checks cancellation before invoking each runner, so a pre-cancelled token delivers to none");
    }

    [Test]
    public void GetRunners_ReturnsAll_AndClearEmpties()
    {
      var r1 = RunnerWith("g1", typeof(TestMessageA), "Quest");
      var r2 = RunnerWith("g2", typeof(TestMessageB), "Quest");
      router.RegisterRunner(r1);
      router.RegisterRunner(r2);

      Assert.That(router.GetRunners().Count(), Is.EqualTo(2));
      Assert.That(router.GetSubscribedMessageTypes(),
        Is.EquivalentTo(new[] { typeof(TestMessageA), typeof(TestMessageB) }));

      router.Clear();

      Assert.That(router.GetRunners(), Is.Empty);
      Assert.That(router.GetSubscribedMessageTypes(), Is.Empty);
    }
  }
}
