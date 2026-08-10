using System;
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
  ///   The single implementation behind <see cref="GameMessageBroker" /> and
  ///   <see cref="GlobalAsyncMessageBroker" />, which were byte-for-byte the same 450-line file twice
  ///   (differing only in log strings, one log level, and where the shared editor DTOs were declared).
  ///
  ///   <para>Statics on a closed generic type are distinct per <typeparamref name="TScope" /> on CLR, Mono and
  ///   IL2CPP alike — generic sharing never merges static storage. That reproduces exactly the
  ///   two-independent-instances property the two separate classes used to provide.</para>
  /// </summary>
  /// <typeparam name="TScope"><see cref="GameScope" /> or <see cref="GlobalScope" />.</typeparam>
  internal static class MessageBrokerCore<TScope>
  {
    private static readonly (string Name, Type LogContext, Serilog.Events.LogEventLevel DoubleInit) scope =
      BrokerScopeInfo.For(typeof(TScope));

    private static readonly Lazy<ILogger> logLazy =
      new(() => Log.Logger.ForContext(scope.LogContext).ForFeature("MessageBus"));

    private static ILogger log => logLazy.Value ?? Logger.None;

    private static IObjectResolver resolver;
    private static bool isInitialized;
    private static bool hasStarted;
    private static IBrokerLifecycleReporter lifecycleReporter;

    /// <summary>Per-scope interceptor storage — this is what keeps game and global registrations apart.</summary>
    private static readonly InterceptorRegistry interceptors = new();

    /// <summary>
    ///   Message types whose interception pipeline is already on this thread's stack. Declared bare and
    ///   created lazily per access: a [ThreadStatic] field initializer runs ONLY on the thread that runs the
    ///   type initializer — every other thread would see null.
    /// </summary>
    [ThreadStatic] private static HashSet<Type> inFlight;

    // Dictionary to store pending async requests with their expected response types
    // Using ConcurrentQueue to maintain FIFO order for proper request/response matching
    private static readonly ConcurrentDictionary<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> pendingRequestsByType = new();

#if UNITY_EDITOR
    // Track accessed publishers and subscribers for diagnostics
    private static readonly ConcurrentDictionary<Type, bool> accessedPublishers = new();
    private static readonly ConcurrentDictionary<Type, bool> accessedSubscribers = new();
#endif

    internal static void Initialize(IObjectResolver objectResolver)
    {
      if (isInitialized)
      {
        log.ForMethod().Write(scope.DoubleInit, "{0} already initialized", scope.Name);
        return;
      }

      resolver = objectResolver;
      isInitialized = true;
      hasStarted = true;
      log.ForMethod().Verbose("{0} initialized", scope.Name);
    }

    internal static void SetLifecycleReporter(IBrokerLifecycleReporter reporter) => lifecycleReporter = reporter;

    internal static void ClearLifecycleReporter() => lifecycleReporter = null;

#if UNITY_EDITOR
    internal static void ResetLifecycleForTests()
    {
      resolver = null;
      isInitialized = false;
      hasStarted = false;
      lifecycleReporter = null;
      loggedViolations.Clear();
    }
#endif

    private static BrokerLifecyclePhase CurrentLifecyclePhase =>
      isInitialized && resolver != null
        ? BrokerLifecyclePhase.Initialized
        : hasStarted ? BrokerLifecyclePhase.Shutdown : BrokerLifecyclePhase.PreInit;

    private static readonly HashSet<string> loggedViolations = new();

    private static void ReportUnavailableAccess(string operation, Type messageType)
    {
      BrokerLifecycleAccess access = new(
        scope.Name,
        operation,
        messageType?.FullName ?? "<null>",
        CurrentLifecyclePhase);
      try
      {
        lifecycleReporter?.Report(access);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "{0} lifecycle reporter failed while recording {1}<{2}>", scope.Name, operation, messageType?.Name);
      }

      string key = $"{scope.Name}|{operation}|{messageType?.FullName}|{access.Phase}";
      if (loggedViolations.Add(key))
      {
        log.ForMethod().Warning(
          "{0} broker access before/after its lifecycle: {1}<{2}> at \"{3}\"",
          scope.Name,
          operation,
          messageType?.Name,
          access.Phase);
      }
    }

    internal static IPublisher<T> GetPublisher<T>()
    {
      if (!isInitialized || resolver == null)
      {
        ReportUnavailableAccess("GetPublisher", typeof(T));
        return null;
      }

      try
      {
        IPublisher<T> publisher = resolver.Resolve<IPublisher<T>>();
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

    internal static ISubscriber<T> GetSubscriber<T>()
    {
      if (!isInitialized || resolver == null)
      {
        ReportUnavailableAccess("GetSubscriber", typeof(T));
        return null;
      }

      try
      {
        ISubscriber<T> subscriber = resolver.Resolve<ISubscriber<T>>();
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
        $"[{scope.Name}] No {role}<{typeName}> is registered in the VContainer scope. " +
        $"Add `builder.RegisterMessageBroker<{typeName}>(options);` to your {scope.Name} installer's " +
        $"RegisterMessagePipe (or the relevant plugin installer). " +
        $"Pub/sub on unregistered types is a silent no-op without this check.";
      log.ForMethod().Fatal(inner, message);
      return new InvalidOperationException(message, inner);
    }

    internal static IDisposable InterceptPre<T>(Action<T> op, HandlerPriority priority, string owner) =>
      interceptors.AddPre(op, priority, owner);

    internal static IDisposable InterceptPost<T>(Action<T> op, HandlerPriority priority, string owner) =>
      interceptors.AddPost(op, priority, owner);

    internal static void ResetInterceptors() => interceptors.Reset();

    internal static void Publish<T>(T message)
    {
      // Check if broker is available before attempting to publish.
      // This prevents error logs during shutdown or before initialization.
      // An unavailable broker bypasses the pipeline entirely — no pre ops, no subscribers, no post ops.
      if (!IsAvailable())
      {
        ReportUnavailableAccess("Publish", typeof(T));
        return;
      }

      inFlight ??= new HashSet<Type>();
      if (!inFlight.Add(typeof(T)))
      {
        // Already inside this type's pipeline on this thread. The nested message is a real publish and IS
        // delivered to subscribers, but no interceptor runs for it — otherwise an op that publishes its own
        // message type recurses without bound.
        PublishCore(message);
        return;
      }

      try
      {
        if (!interceptors.RunPre(message))
        {
          interceptors.RunPost(message);
          return;
        }

        // finally, not sequential: post ops must run even when a subscriber throws or publisher resolution
        // fails, or the teardown guarantee is a lie. The subscriber's exception still reaches the caller,
        // after post ops have run.
        try
        {
          PublishCore(message);
        }
        finally
        {
          interceptors.RunPost(message);
        }
      }
      finally
      {
        // Without this, one throwing op leaves typeof(T) stuck in the set and permanently disables
        // interception for that type on this thread.
        inFlight.Remove(typeof(T));
      }
    }

    private static void PublishCore<T>(T message)
    {
      // GetPublisher throws InvalidOperationException for unregistered types — let it propagate so the
      // call site is the consistent failure point rather than a silent no-op.
      IPublisher<T> publisher = GetPublisher<T>();
      publisher.Publish(message);
      log.ForMethod().Verbose("Published {0} via {1}", typeof(T).Name, scope.Name);
    }

    internal static async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
      if (!isInitialized || resolver == null)
      {
        ReportUnavailableAccess("RequestAsync", typeof(TRequest));
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

        // Clean up - the request should already be removed by CompleteRequest

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

    internal static void CompleteRequest<TResponse>(TResponse response)
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

    internal static bool IsAvailable()
    {
      return isInitialized && resolver != null;
    }

    internal static void Reset()
    {
      resolver = null;
      isInitialized = false;
      hasStarted = true;

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

      loggedViolations.Clear();
      log.ForMethod().Verbose("{0} reset", scope.Name);
    }

#if UNITY_EDITOR
    internal static MessageBrokerDiagnosticInfo GetDiagnosticInfo()
    {
      var info = new MessageBrokerDiagnosticInfo
      {
        BrokerName = scope.Name,
        IsInitialized = isInitialized,
        HasResolver = resolver != null,
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
      if (resolver != null)
        info.RegisteredMessageTypes = IntrospectRegisteredMessageTypes(resolver);

      return info;
    }

    /// <summary>
    ///   Introspect the resolver to find registered message types (Editor only)
    /// </summary>
    private static List<MessageTypeInfo> IntrospectRegisteredMessageTypes(IObjectResolver objectResolver)
    {
      var messageTypes = new List<MessageTypeInfo>();

      try
      {
        // Use reflection to try to access VContainer's internal registry
        var resolverType = objectResolver.GetType();
        var containerProperty = resolverType.GetProperty("Container", BindingFlags.Public | BindingFlags.Instance);

        if (containerProperty != null)
        {
          var container = containerProperty.GetValue(objectResolver);
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
              if (registrations is System.Collections.IEnumerable enumerable)
              {
                foreach (var registration in enumerable)
                {
                  try
                  {
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
