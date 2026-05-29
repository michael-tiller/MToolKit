using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Serilog;
using Serilog.Core;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Global message broker that persists across scene changes and provides
  ///   a single point of message publishing/subscription for cross-scene communication.
  /// </summary>
  public static class GlobalAsyncMessageBroker
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(GlobalAsyncMessageBroker)).ForFeature("MessageBus"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private static IObjectResolver globalResolver;
    private static bool isInitialized;

    // Dictionary to store pending async requests with their expected response types
    // Using ConcurrentQueue to maintain FIFO order for proper request/response matching
    private static readonly ConcurrentDictionary<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> pendingRequestsByType = new();

#if UNITY_EDITOR
    // Track accessed publishers and subscribers for diagnostics
    private static readonly ConcurrentDictionary<Type, bool> accessedPublishers = new();
    private static readonly ConcurrentDictionary<Type, bool> accessedSubscribers = new();
#endif

    /// <summary>
    ///   Initialize the global message broker with the resolver from GlobalInstaller
    /// </summary>
    public static void Initialize(IObjectResolver resolver)
    {
      if (isInitialized)
      {
        log.ForMethod().Verbose("GlobalAsyncMessageBroker already initialized");
        return;
      }

      globalResolver = resolver;
      isInitialized = true;
      log.ForMethod().Verbose("GlobalAsyncMessageBroker initialized");
    }

    /// <summary>
    ///   Get a publisher for the specified message type.
    ///   Returns null if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="InvalidOperationException"/> if the type is not registered with the VContainer scope —
    ///   missing registrations are a developer error and silent no-ops cause recurring debugging sessions.
    /// </summary>
    public static IPublisher<T> GetPublisher<T>()
    {
      if (!isInitialized || globalResolver == null)
      {
        log.ForMethod().Verbose("GlobalAsyncMessageBroker not initialized; returning null publisher for {0}", typeof(T).Name);
        return null;
      }

      try
      {
        IPublisher<T> publisher = globalResolver.Resolve<IPublisher<T>>();
        if (publisher == null)
          throw BuildUnregisteredException<T>("IPublisher", null);
#if UNITY_EDITOR
        accessedPublishers.TryAdd(typeof(T), true);
#endif
        return publisher;
      }
      catch (InvalidOperationException)
      {
        throw;
      }
      catch (Exception ex)
      {
        throw BuildUnregisteredException<T>("IPublisher", ex);
      }
    }

    /// <summary>
    ///   Get a subscriber for the specified message type.
    ///   Returns null if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="InvalidOperationException"/> if the type is not registered with the VContainer scope —
    ///   missing registrations are a developer error and silent no-ops cause recurring debugging sessions.
    /// </summary>
    public static ISubscriber<T> GetSubscriber<T>()
    {
      if (!isInitialized || globalResolver == null)
      {
        log.ForMethod().Verbose("GlobalAsyncMessageBroker not initialized; returning null subscriber for {0}", typeof(T).Name);
        return null;
      }

      try
      {
        ISubscriber<T> subscriber = globalResolver.Resolve<ISubscriber<T>>();
        if (subscriber == null)
          throw BuildUnregisteredException<T>("ISubscriber", null);
#if UNITY_EDITOR
        accessedSubscribers.TryAdd(typeof(T), true);
#endif
        log.ForMethod().Verbose("Resolved subscriber for {0}", typeof(T).Name);
        return subscriber;
      }
      catch (InvalidOperationException)
      {
        throw;
      }
      catch (Exception ex)
      {
        throw BuildUnregisteredException<T>("ISubscriber", ex);
      }
    }

    private static InvalidOperationException BuildUnregisteredException<T>(string role, Exception inner)
    {
      string typeName = typeof(T).Name;
      string message =
        $"[GlobalAsyncMessageBroker] No {role}<{typeName}> is registered in the VContainer scope. " +
        $"Add `builder.RegisterMessageBroker<{typeName}>(options);` to GlobalInstaller.RegisterMessagePipe " +
        $"(or the relevant installer). Pub/sub on unregistered types is a silent no-op without this check.";
      log.ForMethod().Fatal(inner, message);
      return new InvalidOperationException(message, inner);
    }

    /// <summary>
    ///   Publish a message using the global broker.
    ///   No-ops silently if the broker is not initialized (shutdown/pre-init grace).
    ///   Throws <see cref="InvalidOperationException"/> if T is not registered with the VContainer scope.
    /// </summary>
    public static void Publish<T>(T message)
    {
      if (!IsAvailable())
      {
        log.ForMethod().Verbose("Skipping publish of {0} - broker not available (likely during shutdown or before initialization)", typeof(T).Name);
        return;
      }

      // GetPublisher throws InvalidOperationException for unregistered types — let it propagate so the
      // call site is the consistent failure point rather than a silent no-op.
      IPublisher<T> publisher = GetPublisher<T>();
      publisher.Publish(message);
      log.ForMethod().Verbose("Published {0} via global broker", typeof(T).Name);
    }

    /// <summary>
    ///   Async request/response pattern for request/response message pairs
    /// </summary>
    public static async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
      if (!isInitialized || globalResolver == null)
      {
        log.ForMethod().Error("GlobalAsyncMessageBroker not initialized");
        return default;
      }

      try
      {
        // Create a unique request ID
        string requestId = Guid.NewGuid().ToString();
        TaskCompletionSource<object> tcs = new();

        // Store the pending request with its expected response type
        Type responseType = typeof(TResponse);
        ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)> queue = pendingRequestsByType.GetOrAdd(responseType,
          _ => new ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>());
        queue.Enqueue((requestId, tcs));

        // Set up cancellation
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.Token.Register(() =>
        {
          // Remove the request from the queue if it's still pending
          if (pendingRequestsByType.TryGetValue(responseType, out ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)> existingQueue))
          {
            // Create a new queue without the cancelled request
            ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)> newQueue = new();
            while (existingQueue.TryDequeue(out (string RequestId, TaskCompletionSource<object> Tcs) item))
              if (item.RequestId != requestId)
                newQueue.Enqueue(item);
              else
                item.Tcs.TrySetCanceled();
            // Replace the old queue with the new one
            pendingRequestsByType[responseType] = newQueue;
          }
        });

        // Publish the request
        IPublisher<TRequest> publisher = GetPublisher<TRequest>();
        if (publisher != null)
        {
          publisher.Publish(request);
          log.ForMethod().Verbose("Published async request {0} with ID {1}, expecting response {2}", typeof(TRequest).Name, requestId, typeof(TResponse).Name);
        }
        else
        {
          log.ForMethod().Error("Failed to publish async request {0} - publisher not available", typeof(TRequest).Name);
          return default;
        }

        // Wait for response
        object result = await tcs.Task;

        // Cleanup - the request should already be removed by CompleteRequest

        if (result is TResponse response)
          return response;

        log.ForMethod().Error("Response type mismatch for request {0}", typeof(TRequest).Name);
        return default;
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Warning("Async request {0} was canceled", typeof(TRequest).Name);
        return default;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Error in async request {0}", typeof(TRequest).Name);
        return default;
      }
    }

    /// <summary>
    ///   Complete a pending async request with a response
    /// </summary>
    public static void CompleteRequest<TResponse>(TResponse response)
    {
      Type responseType = typeof(TResponse);

      // Get the queue for this response type
      if (pendingRequestsByType.TryGetValue(responseType, out ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)> queue))
        // Try to dequeue the first pending request (FIFO order)
        if (queue.TryDequeue(out (string RequestId, TaskCompletionSource<object> Tcs) request))
        {
          (string requestId, TaskCompletionSource<object> tcs) = request;
          if (tcs.TrySetResult(response))
          {
            log.ForMethod().Verbose("Completed async request with response {0} for request ID {1}", responseType.Name, requestId);
            return;
          }
        }

      log.ForMethod().Warning("No pending request found for response {0}", responseType.Name);
    }

    /// <summary>
    ///   Check if the global broker is available
    /// </summary>
    public static bool IsAvailable()
    {
      return isInitialized && globalResolver != null;
    }

    /// <summary>
    ///   Reset the global broker (useful for testing or scene transitions)
    /// </summary>
    public static void Reset()
    {
      globalResolver = null;
      isInitialized = false;

      // Cancel all pending requests
      foreach (KeyValuePair<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> kvp in pendingRequestsByType)
      {
        ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)> queue = kvp.Value;
        while (queue.TryDequeue(out (string RequestId, TaskCompletionSource<object> Tcs) request))
          request.Tcs.TrySetCanceled();
      }
      pendingRequestsByType.Clear();

#if UNITY_EDITOR
      accessedPublishers.Clear();
      accessedSubscribers.Clear();
#endif

      log.ForMethod().Verbose("GlobalAsyncMessageBroker reset");
    }

#if UNITY_EDITOR
    /// <summary>
    ///   Get diagnostic information about the broker (Editor only)
    /// </summary>
    public static MessageBrokerDiagnosticInfo GetDiagnosticInfo()
    {
      var info = new MessageBrokerDiagnosticInfo
      {
        BrokerName = "GlobalAsyncMessageBroker",
        IsInitialized = isInitialized,
        HasResolver = globalResolver != null,
        PendingRequestCount = 0,
        PendingRequestsByType = new Dictionary<string, int>(),
        AccessedPublishers = new List<string>(),
        AccessedSubscribers = new List<string>(),
        RegisteredMessageTypes = new List<MessageTypeInfo>()
      };

      // Count pending requests
      foreach (KeyValuePair<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> kvp in pendingRequestsByType)
      {
        int count = kvp.Value.Count;
        if (count > 0)
        {
          info.PendingRequestCount += count;
          info.PendingRequestsByType[kvp.Key.Name] = count;
        }
      }

      // Track accessed publishers
      foreach (Type type in accessedPublishers.Keys)
        info.AccessedPublishers.Add(type.Name);

      // Track accessed subscribers
      foreach (Type type in accessedSubscribers.Keys)
        info.AccessedSubscribers.Add(type.Name);

      // Try to introspect resolver for registered message types
      if (globalResolver != null)
        info.RegisteredMessageTypes = IntrospectRegisteredMessageTypes(globalResolver);

      return info;
    }

    /// <summary>
    ///   Introspect the resolver to find registered message types (Editor only)
    /// </summary>
    private static List<MessageTypeInfo> IntrospectRegisteredMessageTypes(IObjectResolver resolver)
    {
      var messageTypes = new List<MessageTypeInfo>();

      try
      {
        // Use reflection to try to access VContainer's internal registry
        var resolverType = resolver.GetType();
        var containerProperty = resolverType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);
        
        if (containerProperty != null)
        {
          var container = containerProperty.GetValue(resolver);
          if (container != null)
          {
            // Try to get registrations
            var registrationsProperty = container.GetType().GetProperty("Registrations", BindingFlags.Public | BindingFlags.Instance);
            if (registrationsProperty == null)
            {
              // Try alternative property names
              var allRegistrationsProperty = container.GetType().GetProperty("AllRegistrations", BindingFlags.Public | BindingFlags.Instance);
              if (allRegistrationsProperty != null)
                registrationsProperty = allRegistrationsProperty;
            }

            if (registrationsProperty != null)
            {
              var registrations = registrationsProperty.GetValue(container);
              if (registrations is IEnumerable enumerable)
              {
                foreach (var registration in enumerable)
                {
                  try
                  {
                    var implementationTypeProperty = registration.GetType().GetProperty("ImplementationType", BindingFlags.Public | BindingFlags.Instance);
                    var serviceTypeProperty = registration.GetType().GetProperty("ServiceType", BindingFlags.Public | BindingFlags.Instance);
                    
                    if (serviceTypeProperty != null)
                    {
                      var serviceType = serviceTypeProperty.GetValue(registration) as Type;
                      if (serviceType != null && serviceType.IsGenericType)
                      {
                        var genericTypeDef = serviceType.GetGenericTypeDefinition();
                        if (genericTypeDef == typeof(IPublisher<>) || genericTypeDef == typeof(ISubscriber<>))
                        {
                          var messageType = serviceType.GetGenericArguments()[0];
                          var messageTypeName = messageType.Name;
                          
                          var existing = messageTypes.FirstOrDefault(m => m.MessageTypeName == messageTypeName);
                          if (existing == null)
                          {
                            existing = new MessageTypeInfo
                            {
                              MessageTypeName = messageTypeName,
                              HasPublisher = false,
                              HasSubscriber = false
                            };
                            messageTypes.Add(existing);
                          }

                          if (genericTypeDef == typeof(IPublisher<>))
                            existing.HasPublisher = true;
                          else if (genericTypeDef == typeof(ISubscriber<>))
                            existing.HasSubscriber = true;
                        }
                      }
                    }
                  }
                  catch
                  {
                    // Skip registrations we can't introspect
                  }
                }
              }
            }
          }
        }
      }
      catch
      {
        // If introspection fails, that's okay - we'll just show accessed types
      }

      return messageTypes;
    }
#endif
  }
}