using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
  /// <summary>
  ///   Routes MessagePipe messages to graph runners with O(1) indexed lookups by (type, domain).
  /// </summary>
  public interface IGraphEventRouter
  {
    /// <summary>Register a graph runner and index its subscriptions</summary>
    void RegisterRunner(IGraphRunner runner);

    /// <summary>Route a MessagePipe message to all matching graph runners</summary>
    UniTask RouteAsync(IGameMessage message, string domain = null, CancellationToken ct = default);

    /// <summary>Get all registered runners</summary>
    IEnumerable<IGraphRunner> GetRunners();

    /// <summary>Get all unique message types that graphs are subscribed to</summary>
    HashSet<Type> GetSubscribedMessageTypes();

    /// <summary>Clear all runners</summary>
    void Clear();
  }
}

