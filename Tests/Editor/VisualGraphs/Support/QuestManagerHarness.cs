using System;
using MessagePipe;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Quest;
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using VContainer;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Stands up a real <see cref="QuestManager" /> over a minimal MessagePipe/VContainer scope wired into
  ///   the static <see cref="GameMessageBroker" />. QuestManager publishes via injected <c>IPublisher</c>s but
  ///   SUBSCRIBES through the static broker, so both must resolve from the SAME scope — tests then observe its
  ///   published lifecycle messages via <see cref="Subscriber{T}" /> (== <c>GameMessageBroker.GetSubscriber</c>).
  ///
  ///   <para>The harness owns ALL shared static-state reset. <see cref="Dispose" /> order matters:
  ///   <see cref="QuestManager.Dispose" /> owns the MessagePipe subscription disposables, so the manager is
  ///   disposed BEFORE the resolver is dropped and the static broker reset.</para>
  /// </summary>
  public sealed class QuestManagerHarness : IDisposable
  {
    private readonly IObjectResolver resolver;

    public QuestManager Manager { get; }

    /// <summary>Captures graph-emitted messages (QuestManager passes this as its <c>IEventEmitter</c>).</summary>
    public RecordingEmitter Emitter { get; } = new();

    /// <summary>The context registry QuestManager attaches quest Graph contexts to (9.0.2b).</summary>
    public GraphContextRegistry ContextRegistry { get; }

    public QuestManagerHarness()
    {
      var builder = new ContainerBuilder();
      var options = builder.RegisterMessagePipe();

      // Every type QuestManager publishes OR subscribes to. Once the broker is initialized an unregistered
      // type THROWS at GetPublisher/GetSubscriber (not a silent no-op), so all nine must be present or the
      // QuestManager constructor's EnsureProgressSubscription would throw.
      builder.RegisterMessageBroker<QuestStartedMessage>(options);
      builder.RegisterMessageBroker<QuestCompletedMessage>(options);
      builder.RegisterMessageBroker<QuestAbandonedMessage>(options);
      builder.RegisterMessageBroker<QuestClaimedMessage>(options);
      builder.RegisterMessageBroker<QuestObjectiveProgressMessage>(options);
      builder.RegisterMessageBroker<StartQuestRequestMessage>(options);
      builder.RegisterMessageBroker<StartCampaignRequestMessage>(options);
      builder.RegisterMessageBroker<ClaimQuestRequestMessage>(options);
      builder.RegisterMessageBroker<CampaignCompletedMessage>(options);

      resolver = builder.Build();

      // Clear any state a sibling fixture leaked, then wire this scope.
      GameMessageBroker.Reset();
      GameMessageBroker.Initialize(resolver);

      ContextRegistry = new GraphContextRegistry(Emitter);

      Manager = new QuestManager(
        new GraphEventRouter(),
        new NodeExecutorRegistry(),
        new NullServiceProvider(),
        Emitter,
        resolver.Resolve<IPublisher<QuestStartedMessage>>(),
        resolver.Resolve<IPublisher<QuestCompletedMessage>>(),
        resolver.Resolve<IPublisher<QuestAbandonedMessage>>(),
        resolver.Resolve<IPublisher<QuestClaimedMessage>>(),
        resolver.Resolve<IPublisher<CampaignCompletedMessage>>(),
        ContextRegistry);
    }

    /// <summary>The static-broker subscriber for <typeparamref name="T" /> (resolves from the harness scope).</summary>
    public ISubscriber<T> Subscriber<T>() => GameMessageBroker.GetSubscriber<T>();

    public void Dispose()
    {
      // 1. Manager first — it owns the MessagePipe subscription disposables; resetting the broker only drops
      //    the static resolver and would NOT dispose those subscriptions.
      Manager?.Dispose();
      // 2. Drop the scope, then reset all shared static state for the next fixture.
      resolver?.Dispose();
      GameMessageBroker.Reset();
      GraphDefinitionRegistry.Clear();
      NodeDebugEvents.ClearAllSubscribers();
    }
  }
}
