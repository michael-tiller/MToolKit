using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Bootstrap
{
  /// <summary>
  ///   Bridges MessagePipe to the VisualGraphs event router.
  ///   Subscribes to all message types that graphs care about and routes them to GraphEventRouter.
  /// </summary>
  public sealed class EventBusBridge : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<EventBusBridge>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private CancellationTokenSource cts;
    private GraphEventRouter router;
    private readonly List<IDisposable> subscriptions = new();

    [Inject]
    public void Construct(GraphEventRouter router)
    {
      this.router = router;
    }

    /// <summary>
    ///   Call this after graphs are loaded to subscribe to MessagePipe for all message types
    ///   that the loaded graphs care about.
    /// </summary>
    public void SubscribeToGraphMessages()
    {
      if (router == null)
      {
        log.Error("Cannot subscribe to graph messages: router not initialized");
        return;
      }

      // Clear any existing subscriptions
      foreach (var sub in subscriptions)
      {
        sub?.Dispose();
      }
      subscriptions.Clear();

      // Get all message types that graphs are subscribed to
      var messageTypes = router.GetSubscribedMessageTypes();

      if (messageTypes.Count == 0)
      {
        log.Warning("No graph subscriptions found - graphs may not receive MessagePipe events");
        return;
      }

      log.Information("Subscribing to {Count} message types for graph event routing", messageTypes.Count);

      // Subscribe to each message type via MessagePipe
      foreach (var messageType in messageTypes)
      {
        try
        {
          SubscribeToMessageType(messageType);
        }
        catch (Exception ex)
        {
          log.Error(ex, "Failed to subscribe to message type {MessageType}", messageType.Name);
        }
      }

      log.Information("EventBusBridge subscribed to {Count} message types", subscriptions.Count);
    }

    private void SubscribeToMessageType(Type messageType)
    {
      // Get the GetSubscriber<T> method from GameMessageBroker (VisualGraphs is a game system)
      var getSubscriberMethod = typeof(GameMessageBroker)
        .GetMethod(nameof(GameMessageBroker.GetSubscriber))
        ?.MakeGenericMethod(messageType);

      if (getSubscriberMethod == null)
      {
        log.Error("Failed to get GetSubscriber method for {MessageType}", messageType.Name);
        return;
      }

      // Call GetSubscriber<T>()
      var subscriber = getSubscriberMethod.Invoke(null, null);
      if (subscriber == null)
      {
        log.Warning("No subscriber available for {MessageType}", messageType.Name);
        return;
      }

      // Create a handler delegate using reflection
      var handlerMethod = GetType()
        .GetMethod(nameof(OnMessageReceivedGeneric), BindingFlags.NonPublic | BindingFlags.Instance)
        ?.MakeGenericMethod(messageType);

      if (handlerMethod == null)
      {
        log.Error("Failed to get handler method for {MessageType}", messageType.Name);
        return;
      }

      // Create Action<T> delegate
      var actionType = typeof(Action<>).MakeGenericType(messageType);
      var handler = Delegate.CreateDelegate(actionType, this, handlerMethod);

      // MessagePipe's ISubscriber<T> requires IMessageHandler<T>, not Action<T>
      // Create a wrapper that implements IMessageHandler<T>
      var messageHandlerType = typeof(MessagePipe.IMessageHandler<>).MakeGenericType(messageType);
      var handlerWrapperType = typeof(ActionMessageHandler<>).MakeGenericType(messageType);

      object handlerWrapper;
      try
      {
        handlerWrapper = Activator.CreateInstance(handlerWrapperType, handler);
        if (handlerWrapper == null)
        {
          log.Error("Failed to create ActionMessageHandler instance for {MessageType}", messageType.Name);
          return;
        }
      }
      catch (Exception ex)
      {
        log.Error(ex, "Exception creating ActionMessageHandler for {MessageType}: {Message}", messageType.Name, ex.Message);
        return;
      }

      // Get the Subscribe method that takes IMessageHandler<T> and optional filters
      var interfaceType = typeof(MessagePipe.ISubscriber<>).MakeGenericType(messageType);
      var filterArrayType = typeof(MessagePipe.MessageHandlerFilter<>).MakeGenericType(messageType).MakeArrayType();

      // Try to find Subscribe method - it might have filters as optional parameter
      var subscribeMethod = interfaceType.GetMethod("Subscribe", new[] { messageHandlerType, filterArrayType });

      // If not found, try with just the handler (filters might be optional)
      if (subscribeMethod == null)
      {
        subscribeMethod = interfaceType.GetMethod("Subscribe", new[] { messageHandlerType });
      }

      if (subscribeMethod == null)
      {
        log.Error("Failed to get Subscribe method for {MessageType}. Interface: {InterfaceType}, HandlerType: {HandlerType}",
          messageType.Name, interfaceType.FullName, messageHandlerType.FullName);
        return;
      }

      // Call Subscribe - use empty array for filters if method requires it, otherwise just pass handler
      IDisposable disposable;
      try
      {
        object result;
        var parameters = subscribeMethod.GetParameters();
        if (parameters.Length == 2)
        {
          // Method requires filters parameter - use empty array instead of null
          var emptyFilterArray = Array.CreateInstance(typeof(MessagePipe.MessageHandlerFilter<>).MakeGenericType(messageType), 0);
          result = subscribeMethod.Invoke(subscriber, new object[] { handlerWrapper, emptyFilterArray });
        }
        else
        {
          // Method only requires handler
          result = subscribeMethod.Invoke(subscriber, new object[] { handlerWrapper });
        }
        disposable = result as IDisposable;

        if (disposable == null)
        {
          log.Warning("Subscribe returned non-disposable result for {MessageType}. Result type: {ResultType}",
            messageType.Name, result?.GetType().FullName ?? "null");
          return;
        }
      }
      catch (Exception ex)
      {
        // Unwrap InvocationTargetException to get the real exception
        var innerException = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException : ex;
        log.Error(innerException ?? ex, "Exception calling Subscribe for {MessageType}: {Message}. Outer exception: {OuterMessage}",
          messageType.Name, innerException?.Message ?? ex.Message, ex.Message);
        return;
      }

      subscriptions.Add(disposable);
      log.Debug("Subscribed to {MessageType} from MessagePipe", messageType.Name);
    }

    private void OnMessageReceivedGeneric<T>(T message) where T : IGameMessage
    {
      OnMessageReceived(message, domain: null);
    }

    private void Start()
    {
      cts = new CancellationTokenSource();
      log.Debug("EventBusBridge started (call SubscribeToGraphMessages after graphs load)");
    }

    private void OnDestroy()
    {
      foreach (var sub in subscriptions)
      {
        sub?.Dispose();
      }
      subscriptions.Clear();

      cts?.Cancel();
      cts?.Dispose();
    }

    private void OnMessageReceived(IGameMessage message, string domain = null)
    {
      if (message == null || router == null) return;

      // Route MessagePipe message to graphs
      RouteMessageAsync(message, domain, cts.Token).Forget();
    }

    private async UniTask RouteMessageAsync(IGameMessage message, string domain, CancellationToken ct)
    {
      try
      {
        await router.RouteAsync(message, domain, ct);
      }
      catch (Exception ex)
      {
        log.Error(ex, "Failed to route message {MessageType} in domain {Domain}",
          message.GetType().Name, domain);
      }
    }

    /// <summary>
    /// Wraps an Action<T> to implement IMessageHandler<T> for MessagePipe
    /// </summary>
    private sealed class ActionMessageHandler<T> : MessagePipe.IMessageHandler<T>
    {
      private readonly Action<T> action;

      public ActionMessageHandler(Action<T> action)
      {
        this.action = action ?? throw new ArgumentNullException(nameof(action));
      }

      public void Handle(T message)
      {
        action(message);
      }
    }
  }
}