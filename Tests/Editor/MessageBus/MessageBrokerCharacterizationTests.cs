using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.MessageBus;
using MToolKit.Tests.Editor.MessageBus.Support;
using NUnit.Framework;
using VContainer;

namespace MToolKit.Tests.Editor.MessageBus
{
  /// <summary>
  ///   Pins the OBSERVABLE behaviour of the two static brokers before the MessageBrokerCore&lt;TScope&gt;
  ///   de-duplication, and must pass UNCHANGED after it. Any edit needed here post-refactor means the
  ///   refactor altered the public contract — read the failure rather than re-baselining it.
  ///
  ///   <para>Both brokers are static singletons, so every test resets both on entry and exit. The two
  ///   are asserted to hold INDEPENDENT state, which is the property the current two-class duplication
  ///   provides and which statics-per-closed-generic must reproduce.</para>
  /// </summary>
  [TestFixture]
  public class MessageBrokerCharacterizationTests
  {
    private IObjectResolver resolver;

    [SetUp]
    public void SetUp()
    {
      GameMessageBroker.Reset();
      GlobalAsyncMessageBroker.Reset();

      var builder = new ContainerBuilder();
      MessagePipeOptions options = builder.RegisterMessagePipe();
      builder.RegisterMessageBroker<BrokerPing>(options);
      builder.RegisterMessageBroker<BrokerPong>(options);
      builder.RegisterMessageBroker<BrokerRequest>(options);
      builder.RegisterMessageBroker<BrokerResponse>(options);
      resolver = builder.Build();
    }

    [TearDown]
    public void TearDown()
    {
      GameMessageBroker.Reset();
      GlobalAsyncMessageBroker.Reset();
      resolver?.Dispose();
      resolver = null;
    }

    // ---------------------------------------------------------------- scope independence

    [Test]
    public void Initialize_Game_DoesNotMakeGlobalAvailable()
    {
      GameMessageBroker.Initialize(resolver);

      Assert.That(GameMessageBroker.IsAvailable(), Is.True, "game broker should be available after Initialize");
      Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.False, "global broker must hold independent state");
    }

    [Test]
    public void Initialize_Global_DoesNotMakeGameAvailable()
    {
      GlobalAsyncMessageBroker.Initialize(resolver);

      Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True);
      Assert.That(GameMessageBroker.IsAvailable(), Is.False);
    }

    [Test]
    public void Reset_Game_LeavesGlobalInitialized()
    {
      GameMessageBroker.Initialize(resolver);
      GlobalAsyncMessageBroker.Initialize(resolver);

      GameMessageBroker.Reset();

      Assert.That(GameMessageBroker.IsAvailable(), Is.False);
      Assert.That(GlobalAsyncMessageBroker.IsAvailable(), Is.True, "resetting one broker must not reset the other");
    }

    // ---------------------------------------------------------------- pre-init grace

    [Test]
    public void GetPublisher_BeforeInitialize_ReturnsNull()
    {
      Assert.That(GameMessageBroker.GetPublisher<BrokerPing>(), Is.Null);
      Assert.That(GlobalAsyncMessageBroker.GetPublisher<BrokerPing>(), Is.Null);
    }

    [Test]
    public void GetSubscriber_BeforeInitialize_ReturnsNull()
    {
      Assert.That(GameMessageBroker.GetSubscriber<BrokerPing>(), Is.Null);
      Assert.That(GlobalAsyncMessageBroker.GetSubscriber<BrokerPing>(), Is.Null);
    }

    [Test]
    public void Publish_BeforeInitialize_IsSilentNoOp()
    {
      Assert.DoesNotThrow(() => GameMessageBroker.Publish(new BrokerPing(1)));
      Assert.DoesNotThrow(() => GlobalAsyncMessageBroker.Publish(new BrokerPing(1)));
    }

    // ---------------------------------------------------------------- unregistered types throw

    [Test]
    public void GetPublisher_UnregisteredType_Throws()
    {
      GameMessageBroker.Initialize(resolver);

      Assert.Throws<InvalidOperationException>(() => GameMessageBroker.GetPublisher<BrokerUnregistered>());
    }

    [Test]
    public void GetSubscriber_UnregisteredType_Throws()
    {
      GameMessageBroker.Initialize(resolver);

      Assert.Throws<InvalidOperationException>(() => GameMessageBroker.GetSubscriber<BrokerUnregistered>());
    }

    [Test]
    public void Publish_UnregisteredType_PropagatesTheThrow()
    {
      // Publish deliberately does NOT swallow: the call site is the consistent failure point.
      GameMessageBroker.Initialize(resolver);

      Assert.Throws<InvalidOperationException>(() => GameMessageBroker.Publish(new BrokerUnregistered()));
    }

    // ---------------------------------------------------------------- publish/subscribe round trip

    [Test]
    public void Publish_ReachesSubscriber()
    {
      GameMessageBroker.Initialize(resolver);
      int received = 0;
      using IDisposable sub = GameMessageBroker.GetSubscriber<BrokerPing>().Subscribe(m => received = m.Value);

      GameMessageBroker.Publish(new BrokerPing(42));

      Assert.That(received, Is.EqualTo(42));
    }

    [Test]
    public void Publish_DoesNotCrossMessageTypes()
    {
      GameMessageBroker.Initialize(resolver);
      int pings = 0;
      using IDisposable sub = GameMessageBroker.GetSubscriber<BrokerPing>().Subscribe(_ => pings++);

      GameMessageBroker.Publish(new BrokerPong(1));

      Assert.That(pings, Is.Zero);
    }

    // ---------------------------------------------------------------- double initialize

    [Test]
    public void Initialize_Twice_KeepsFirstResolverAndDoesNotThrow()
    {
      GameMessageBroker.Initialize(resolver);

      var second = new ContainerBuilder();
      MessagePipeOptions options = second.RegisterMessagePipe();
      second.RegisterMessageBroker<BrokerPing>(options);
      using IObjectResolver secondResolver = second.Build();

      Assert.DoesNotThrow(() => GameMessageBroker.Initialize(secondResolver));

      // The FIRST resolver still wins: a subscriber taken from the original scope must still receive.
      int received = 0;
      using IDisposable sub = GameMessageBroker.GetSubscriber<BrokerPing>().Subscribe(m => received = m.Value);
      resolver.Resolve<IPublisher<BrokerPing>>().Publish(new BrokerPing(7));

      Assert.That(received, Is.EqualTo(7), "second Initialize must be a no-op that keeps the first resolver");
    }

    // ---------------------------------------------------------------- request/response

    [Test]
    public async Task RequestAsync_CompletesFromCompleteRequest()
    {
      GameMessageBroker.Initialize(resolver);
      using IDisposable sub = GameMessageBroker.GetSubscriber<BrokerRequest>()
        .Subscribe(req => GameMessageBroker.CompleteRequest(new BrokerResponse(req.Id)));

      BrokerResponse response = await GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("a"));

      Assert.That(response.Id, Is.EqualTo("a"));
    }

    [Test]
    public async Task RequestAsync_TwoInFlight_CompleteFifo()
    {
      GameMessageBroker.Initialize(resolver);

      // No auto-responder: queue both, then complete manually in order and assert FIFO pairing.
      UniTask<BrokerResponse> first = GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("first"));
      UniTask<BrokerResponse> second = GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("second"));

      GameMessageBroker.CompleteRequest(new BrokerResponse("one"));
      GameMessageBroker.CompleteRequest(new BrokerResponse("two"));

      Assert.That((await first).Id, Is.EqualTo("one"), "first request must take the first response");
      Assert.That((await second).Id, Is.EqualTo("two"));
    }

    [Test]
    public async Task RequestAsync_BeforeInitialize_ReturnsDefault()
    {
      BrokerResponse response =
        await GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("x"));

      Assert.That(response.Id, Is.Null);
    }

    [Test]
    public void CompleteRequest_WithNoPendingRequest_DoesNotThrow()
    {
      GameMessageBroker.Initialize(resolver);

      Assert.DoesNotThrow(() => GameMessageBroker.CompleteRequest(new BrokerResponse("orphan")));
    }

    [Test]
    public async Task Reset_CancelsPendingRequests()
    {
      GameMessageBroker.Initialize(resolver);
      UniTask<BrokerResponse> pending =
        GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("dropped"));

      GameMessageBroker.Reset();

      // RequestAsync catches OperationCanceledException internally and returns default rather than faulting.
      BrokerResponse response = await pending;

      Assert.That(response.Id, Is.Null);
    }

    [Test]
    public async Task RequestAsync_HonoursCancellationToken()
    {
      GameMessageBroker.Initialize(resolver);
      using var cts = new CancellationTokenSource();

      UniTask<BrokerResponse> pending =
        GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("cancelled"), cts.Token);
      cts.Cancel();

      Assert.That((await pending).Id, Is.Null);
    }

    // ---------------------------------------------------------------- reset

    [Test]
    public void Reset_ClearsInitialization()
    {
      GameMessageBroker.Initialize(resolver);

      GameMessageBroker.Reset();

      Assert.That(GameMessageBroker.IsAvailable(), Is.False);
      Assert.That(GameMessageBroker.GetPublisher<BrokerPing>(), Is.Null);
    }

    [Test]
    public void Reset_ThenInitialize_RestoresAvailability()
    {
      GameMessageBroker.Initialize(resolver);
      GameMessageBroker.Reset();

      GameMessageBroker.Initialize(resolver);

      Assert.That(GameMessageBroker.IsAvailable(), Is.True);
    }
  }
}
