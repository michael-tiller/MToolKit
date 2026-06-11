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
  ///   Characterization of <see cref="GraphEventRouter" />. The load-bearing pin is the WILDCARD
  ///   SEMANTIC: routing tries the exact (type, domain) bucket first and only falls back to the empty-domain
  ///   bucket when no exact bucket exists — so an empty-domain ("any") subscriber is SUPPRESSED whenever an
  ///   exact-domain subscriber exists for that domain. Also pins exact-type-only matching (no inheritance),
  ///   registration delivery, and pre-cancellation. Routing keys on the runtime message type and dispatches
  ///   synchronously (fakes return UniTask.CompletedTask), so GetAwaiter().GetResult() observes real delivery.
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
    public void Route_ExactMatch_SuppressesEmptyDomainSubscriber()
    {
      var exact = RunnerWith("exact", typeof(TestMessageA), "Quest");
      var anyDomain = RunnerWith("any", typeof(TestMessageA), null);
      router.RegisterRunner(exact);
      router.RegisterRunner(anyDomain);

      Route(new TestMessageA(), "Quest");

      Assert.That(exact.HandleMessageAsyncCallCount, Is.EqualTo(1), "exact-domain subscriber receives it");
      Assert.That(anyDomain.HandleMessageAsyncCallCount, Is.EqualTo(0),
        "the empty-domain ('any') subscriber is SUPPRESSED because an exact (type, domain) bucket exists");
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
