using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
  ///   Game message broker that handles game-specific communication within the game scene.
  ///   This broker is managed by GameInstaller and provides game-scoped message publishing/subscription.
  /// </summary>
  public static class GameMessageBroker
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(GameMessageBroker)).ForFeature("MessageBus"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private static IObjectResolver gameResolver;
    private static bool isInitialized;

    // Dictionary to store pending async requests with their expected response types
    // Using ConcurrentQueue to maintain FIFO order for proper request/response matching
    private static readonly ConcurrentDictionary<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> pendingRequestsByType = new();

    /// <summary>
    ///   Initialize the game message broker with the resolver from GameInstaller
    /// </summary>
    public static void Initialize(IObjectResolver resolver)
    {
      if (isInitialized)
      {
        log.ForMethod().Warning("GameMessageBroker already initialized");
        return;
      }

      gameResolver = resolver;
      isInitialized = true;
      log.ForMethod().Verbose("GameMessageBroker initialized");
    }

    /// <summary>
    ///   Get a publisher for the specified message type
    /// </summary>
    public static IPublisher<T> GetPublisher<T>()
    {
      if (!isInitialized || gameResolver == null)
      {
        log.ForMethod().Error("GameMessageBroker not initialized");
        return null;
      }

      try
      {
        IPublisher<T> publisher = gameResolver.Resolve<IPublisher<T>>();
        return publisher;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to resolve publisher for {0}", typeof(T).Name);
        return null;
      }
    }

    /// <summary>
    ///   Get a subscriber for the specified message type
    /// </summary>
    public static ISubscriber<T> GetSubscriber<T>()
    {
      if (!isInitialized || gameResolver == null)
      {
        log.ForMethod().Error("GameMessageBroker not initialized");
        return null;
      }

      try
      {
        ISubscriber<T> subscriber = gameResolver.Resolve<ISubscriber<T>>();
        log.ForMethod().Verbose("Resolved subscriber for {0}", typeof(T).Name);
        return subscriber;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to resolve subscriber for {0}", typeof(T).Name);
        return null;
      }
    }

    /// <summary>
    ///   Publish a message using the game broker
    /// </summary>
    public static void Publish<T>(T message)
    {
      IPublisher<T> publisher = GetPublisher<T>();
      if (publisher != null)
        try
        {
          publisher.Publish(message);
          log.ForMethod().Verbose("Published {0} via game broker", typeof(T).Name);
        }
        catch (Exception ex)
        {
          log.ForMethod().Fatal(ex, "Failed to publish {0} - publisher threw exception: {Exception}", typeof(T).Name, ex.Message);
        }
      else
        log.ForMethod().Warning("Failed to publish {0} - publisher not available", typeof(T).Name);
    }

    /// <summary>
    ///   Async request/response pattern for request/response message pairs
    /// </summary>
    public static async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
      if (!isInitialized || gameResolver == null)
      {
        log.ForMethod().Error("GameMessageBroker not initialized");
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
    ///   Check if the game broker is available
    /// </summary>
    public static bool IsAvailable()
    {
      return isInitialized && gameResolver != null;
    }

    /// <summary>
    ///   Reset the game broker (useful for testing or scene transitions)
    /// </summary>
    public static void Reset()
    {
      gameResolver = null;
      isInitialized = false;

      // Cancel all pending requests
      foreach (KeyValuePair<Type, ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)>> kvp in pendingRequestsByType)
      {
        ConcurrentQueue<(string RequestId, TaskCompletionSource<object> Tcs)> queue = kvp.Value;
        while (queue.TryDequeue(out (string RequestId, TaskCompletionSource<object> Tcs) request))
          request.Tcs.TrySetCanceled();
      }
      pendingRequestsByType.Clear();

      log.ForMethod().Verbose("GameMessageBroker reset");
    }
  }
}