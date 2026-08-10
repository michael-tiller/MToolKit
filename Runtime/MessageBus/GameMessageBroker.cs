using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Game message broker that handles game-specific communication within the game scene.
  ///   This broker is managed by GameInstaller and provides game-scoped message publishing/subscription.
  ///
  ///   <para>A façade over <see cref="MessageBrokerCore{TScope}" /> closed on <see cref="GameScope" />. The
  ///   public surface is unchanged from when this file carried its own copy of the implementation — including
  ///   optional-parameter defaults and the <c>#if UNITY_EDITOR</c> boundary on
  ///   <see cref="GetDiagnosticInfo" />, both of which are baked in at call sites.</para>
  /// </summary>
  public static class GameMessageBroker
  {
    /// <summary>
    ///   Initialize the game message broker with the resolver from GameInstaller
    /// </summary>
    public static void Initialize(IObjectResolver resolver) => MessageBrokerCore<GameScope>.Initialize(resolver);

    public static void SetLifecycleReporter(IBrokerLifecycleReporter reporter) => MessageBrokerCore<GameScope>.SetLifecycleReporter(reporter);

    public static void ClearLifecycleReporter() => MessageBrokerCore<GameScope>.ClearLifecycleReporter();

#if UNITY_EDITOR
    internal static void ResetLifecycleForTests() => MessageBrokerCore<GameScope>.ResetLifecycleForTests();
#endif

    /// <summary>
    ///   Get a publisher for the specified message type.
    ///   Returns null if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="System.InvalidOperationException" /> if the type is not registered with the
    ///   VContainer scope — missing registrations are a developer error and silent no-ops cause recurring
    ///   debugging sessions.
    /// </summary>
    public static IPublisher<T> GetPublisher<T>() => MessageBrokerCore<GameScope>.GetPublisher<T>();

    /// <summary>
    ///   Get a subscriber for the specified message type.
    ///   Returns null if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="System.InvalidOperationException" /> if the type is not registered with the
    ///   VContainer scope.
    /// </summary>
    public static ISubscriber<T> GetSubscriber<T>() => MessageBrokerCore<GameScope>.GetSubscriber<T>();

    /// <summary>
    ///   Publish a message using the game broker.
    ///   No-ops silently if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="System.InvalidOperationException" /> if T is not registered with the VContainer scope.
    /// </summary>
    public static void Publish<T>(T message) => MessageBrokerCore<GameScope>.Publish(message);

    /// <summary>
    ///   Async request/response pattern for request/response message pairs
    /// </summary>
    public static UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) =>
      MessageBrokerCore<GameScope>.RequestAsync<TRequest, TResponse>(request, cancellationToken);

    /// <summary>
    ///   Complete a pending async request with a response
    /// </summary>
    public static void CompleteRequest<TResponse>(TResponse response) => MessageBrokerCore<GameScope>.CompleteRequest(response);

    /// <summary>
    ///   Check if the game broker is available
    /// </summary>
    public static bool IsAvailable() => MessageBrokerCore<GameScope>.IsAvailable();

    /// <summary>
    ///   Register a pre-interceptor: runs before <paramref name="op" />'s message type reaches the bus, and
    ///   may veto it by calling <c>Cancel</c> on a message implementing <c>ICancellableMessage</c>.
    ///   Dispose the returned token to unregister.
    ///
    ///   <para><paramref name="owner" /> is opaque attribution used only when the op throws — MTK never
    ///   interprets it. <paramref name="op" /> MUST be synchronous; an <c>async void</c> lambda is
    ///   unsupported, because the reentrancy guard tracks physical threads and such an op can resume on
    ///   another thread after the guard is gone.</para>
    /// </summary>
    public static IDisposable InterceptPre<T>(Action<T> op, HandlerPriority priority = HandlerPriority.Normal, string owner = null) =>
      MessageBrokerCore<GameScope>.InterceptPre(op, priority, owner);

    /// <summary>
    ///   Register a post-interceptor: runs after all subscribers, and after a cancellation. It can observe
    ///   cancellation — and nothing else; it receives only the message, so it cannot see whether a subscriber
    ///   threw. Same synchronous-op requirement as <see cref="InterceptPre{T}" />.
    /// </summary>
    public static IDisposable InterceptPost<T>(Action<T> op, HandlerPriority priority = HandlerPriority.Normal, string owner = null) =>
      MessageBrokerCore<GameScope>.InterceptPost(op, priority, owner);

    /// <summary>
    ///   Reset the game broker (useful for testing or scene transitions).
    ///   Drops the resolver and cancels pending requests. Deliberately does NOT touch interceptor
    ///   registrations — see <see cref="ResetInterceptors" />.
    /// </summary>
    public static void Reset() => MessageBrokerCore<GameScope>.Reset();

    /// <summary>
    ///   Dispose and clear every interceptor registration on this scope, without tearing down the broker.
    ///   Separate from <see cref="Reset" /> so a future scene-transition Reset cannot silently destroy
    ///   registrations belonging to callers that have no idea interceptors exist.
    /// </summary>
    public static void ResetInterceptors() => MessageBrokerCore<GameScope>.ResetInterceptors();

#if UNITY_EDITOR
    /// <summary>
    ///   Get diagnostic information about the broker (Editor only)
    /// </summary>
    public static MessageBrokerDiagnosticInfo GetDiagnosticInfo() => MessageBrokerCore<GameScope>.GetDiagnosticInfo();
#endif
  }
}
