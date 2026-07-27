using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Global message broker that persists across scene changes and provides
  ///   a single point of message publishing/subscription for cross-scene communication.
  ///
  ///   <para>A façade over <see cref="MessageBrokerCore{TScope}" /> closed on <see cref="GlobalScope" />.
  ///   The public surface is unchanged from when this file carried its own copy of the implementation —
  ///   including optional-parameter defaults and the <c>#if UNITY_EDITOR</c> boundary on
  ///   <see cref="GetDiagnosticInfo" />, both of which are baked in at call sites.</para>
  /// </summary>
  public static class GlobalAsyncMessageBroker
  {
    /// <summary>
    ///   Initialize the global message broker with the resolver from GlobalInstaller
    /// </summary>
    public static void Initialize(IObjectResolver resolver) => MessageBrokerCore<GlobalScope>.Initialize(resolver);

    /// <summary>
    ///   Get a publisher for the specified message type.
    ///   Returns null if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="System.InvalidOperationException" /> if the type is not registered with the
    ///   VContainer scope — missing registrations are a developer error and silent no-ops cause recurring
    ///   debugging sessions.
    /// </summary>
    public static IPublisher<T> GetPublisher<T>() => MessageBrokerCore<GlobalScope>.GetPublisher<T>();

    /// <summary>
    ///   Get a subscriber for the specified message type.
    ///   Returns null if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="System.InvalidOperationException" /> if the type is not registered with the
    ///   VContainer scope.
    /// </summary>
    public static ISubscriber<T> GetSubscriber<T>() => MessageBrokerCore<GlobalScope>.GetSubscriber<T>();

    /// <summary>
    ///   Publish a message using the global broker.
    ///   No-ops silently if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="System.InvalidOperationException" /> if T is not registered with the VContainer scope.
    /// </summary>
    public static void Publish<T>(T message) => MessageBrokerCore<GlobalScope>.Publish(message);

    /// <summary>
    ///   Async request/response pattern for request/response message pairs
    /// </summary>
    public static UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) =>
      MessageBrokerCore<GlobalScope>.RequestAsync<TRequest, TResponse>(request, cancellationToken);

    /// <summary>
    ///   Complete a pending async request with a response
    /// </summary>
    public static void CompleteRequest<TResponse>(TResponse response) => MessageBrokerCore<GlobalScope>.CompleteRequest(response);

    /// <summary>
    ///   Check if the global broker is available
    /// </summary>
    public static bool IsAvailable() => MessageBrokerCore<GlobalScope>.IsAvailable();

    /// <summary>
    ///   Reset the global broker (useful for testing or scene transitions).
    ///   Drops the resolver and cancels pending requests.
    /// </summary>
    public static void Reset() => MessageBrokerCore<GlobalScope>.Reset();

#if UNITY_EDITOR
    /// <summary>
    ///   Get diagnostic information about the broker (Editor only)
    /// </summary>
    public static MessageBrokerDiagnosticInfo GetDiagnosticInfo() => MessageBrokerCore<GlobalScope>.GetDiagnosticInfo();
#endif
  }
}
