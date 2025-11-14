using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime
{
  /// <summary>
  ///   Routes MessagePipe messages to graph runners with O(1) indexed lookups by (type, domain).
  /// </summary>
  public sealed class GraphEventRouter : IGraphEventRouter
  {
    private readonly List<IGraphRunner> all = new();
    private readonly Dictionary<(Type messageType, string domain), List<IGraphRunner>> byTypeDomain = new();

    /// <summary>Register a graph runner and index its subscriptions</summary>
    public void RegisterRunner(IGraphRunner runner)
    {
      if (runner == null)
        throw new ArgumentNullException(nameof(runner));

      all.Add(runner);

      foreach (var sub in runner.Definition.Subscriptions)
      {
        if (sub.MessageType == null || !sub.MessageType.IsValid)
          continue;

        var key = (sub.MessageType.Type, sub.DomainFilter ?? string.Empty);

        if (!byTypeDomain.TryGetValue(key, out var list))
        {
          list = new List<IGraphRunner>();
          byTypeDomain[key] = list;
        }

        list.Add(runner);
      }
    }

    /// <summary>Route a MessagePipe message to all matching graph runners</summary>
    public async UniTask RouteAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
    {
      if (message == null) return;

      var messageType = message.GetType();
      var domainFilter = domain ?? string.Empty;
      var key = (messageType, domainFilter);

      // Try exact domain match first
      if (byTypeDomain.TryGetValue(key, out var list))
      {
        foreach (var runner in list)
        {
          if (ct.IsCancellationRequested) break;
          await runner.HandleMessageAsync(message, domain, ct);
        }
        return;
      }

      // Try wildcard domain match (empty domain = match all)
      key = (messageType, string.Empty);
      if (byTypeDomain.TryGetValue(key, out list))
        foreach (var runner in list)
        {
          if (ct.IsCancellationRequested) break;
          await runner.HandleMessageAsync(message, domain, ct);
        }
    }

    /// <summary>Get all registered runners</summary>
    public IEnumerable<IGraphRunner> GetRunners()
    {
      return all;
    }

    /// <summary>Get all unique message types that graphs are subscribed to</summary>
    public HashSet<Type> GetSubscribedMessageTypes()
    {
      var types = new HashSet<Type>();
      foreach (var key in byTypeDomain.Keys)
      {
        types.Add(key.messageType);
      }
      return types;
    }

    /// <summary>Clear all runners</summary>
    public void Clear()
    {
      all.Clear();
      byTypeDomain.Clear();
    }
  }
}