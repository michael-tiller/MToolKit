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

    /// <summary>Remove every registration of the supplied runner instance and its dispatch bookkeeping.</summary>
    /// <param name="runner">Runner instance to unregister; null is a no-op.</param>
    /// <remarks>
    /// The legacy fallback snapshots retained registrations, then rebuilds their indexes through
    /// Clear and RegisterRunner. Survivor order and duplicate registrations are preserved.
    /// Custom routers can override this method to preserve additional bookkeeping during removal.
    /// </remarks>
    void UnregisterRunner(IGraphRunner runner)
    {
      if (runner == null) return;
      var retained = new List<IGraphRunner>();
      var found = false;
      foreach (var registered in GetRunners())
      {
        if (object.ReferenceEquals(registered, runner)) found = true;
        else retained.Add(registered);
      }
      if (!found) return;

      Clear();
      foreach (var registered in retained) RegisterRunner(registered);
    }

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

