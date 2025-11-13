using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime
{
  /// <summary>
  ///   Routes events to graph runners with O(1) indexed lookups by (type, domain).
  /// </summary>
  public sealed class GraphEventRouter
  {
    private readonly List<IGraphRunner> all = new();
    private readonly Dictionary<(string type, string domain), List<IGraphRunner>> byTypeDomain = new();

    /// <summary>Register a graph runner and index its subscriptions</summary>
    public void RegisterRunner(IGraphRunner runner)
    {
      if (runner == null)
        throw new ArgumentNullException(nameof(runner));

      all.Add(runner);

      foreach (var sub in runner.Definition.Subscriptions)
      {
        var key = (sub.EventType, sub.EventDomain ?? string.Empty);

        if (!byTypeDomain.TryGetValue(key, out var list))
        {
          list = new List<IGraphRunner>();
          byTypeDomain[key] = list;
        }

        list.Add(runner);
      }
    }

    /// <summary>Route an event to all matching graph runners</summary>
    public async UniTask RouteAsync(IEventMessage message, CancellationToken ct = default)
    {
      if (message == null) return;

      var domain = message.Domain ?? string.Empty;
      var key = (message.Type, domain);

      // Try exact domain match first
      if (byTypeDomain.TryGetValue(key, out var list))
      {
        foreach (var runner in list)
        {
          if (ct.IsCancellationRequested) break;
          await runner.HandleEventAsync(message, ct);
        }
        return;
      }

      // Try wildcard domain match (empty domain = match all)
      key = (message.Type, string.Empty);
      if (byTypeDomain.TryGetValue(key, out list))
        foreach (var runner in list)
        {
          if (ct.IsCancellationRequested) break;
          await runner.HandleEventAsync(message, ct);
        }
    }

    /// <summary>Get all registered runners</summary>
    public IEnumerable<IGraphRunner> GetRunners()
    {
      return all;
    }

    /// <summary>Clear all runners</summary>
    public void Clear()
    {
      all.Clear();
      byTypeDomain.Clear();
    }
  }
}