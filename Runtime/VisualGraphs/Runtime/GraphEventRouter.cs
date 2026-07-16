using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Runtime
{
  /// <summary>
  ///   Routes MessagePipe messages to graph runners, indexed by (type, domain). Delivery is ADDITIVE:
  ///   an exact-domain match and the empty-domain ("any") wildcard both deliver, deduplicated by runner
  ///   reference identity and dispatched in overall registration order.
  /// </summary>
  public sealed class GraphEventRouter : IGraphEventRouter
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<GraphEventRouter>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    /// <summary>
    ///   Maximum re-entrant routing depth. A graph that publishes an event it also subscribes to (directly or
    ///   through a cycle of graphs) re-enters RouteAsync synchronously on the same call chain; without a budget
    ///   this recurses until the process dies (stack overflow / OOM — no managed exception is ever logged).
    ///   Legitimate event cascades are shallow; anything deeper than this is a feedback loop.
    /// </summary>
    public const int MaxRouteDepth = 16;

    /// <summary>
    ///   Dispatch-rate watchdog: max dispatches per runner within <see cref="RateWindowSeconds" />.
    ///   The depth budget only catches loops that recurse on one call stack; a graph that republishes
    ///   through a frame-deferred continuation re-enters at depth 0 every hop and livelocks the main
    ///   thread instead (observed: magic_amulet_events, 10 MB/min log storm). No legitimate content
    ///   graph dispatches anywhere near this often; on breach the runner is suspended for
    ///   <see cref="RateSuspendSeconds" /> with one Error log.
    /// </summary>
    public const int MaxDispatchesPerWindow = 100;
    public const float RateWindowSeconds = 1f;
    public const float RateSuspendSeconds = 5f;

    /// <summary>Clock for the rate watchdog (seconds, monotonic). Settable for tests; defaults to Unity realtime.</summary>
    public Func<float> TimeProvider { get; set; } = () => UnityEngine.Time.realtimeSinceStartup;

    private sealed class DispatchWindow
    {
      public float WindowStart;
      public int Count;
      public float SuspendedUntil;
    }

    private readonly List<IGraphRunner> all = new();
    private readonly Dictionary<(Type messageType, string domain), List<IGraphRunner>> byTypeDomain = new();
    private readonly Dictionary<IGraphRunner, DispatchWindow> dispatchWindows = new();
    private int routeDepth;

    /// <summary>Register a graph runner and index its subscriptions</summary>
    public void RegisterRunner(IGraphRunner runner)
    {
      if (runner == null)
        throw new ArgumentNullException(nameof(runner));

      all.Add(runner);

      var subscriptionCount = 0;
      foreach (var sub in runner.Definition.Subscriptions)
      {
        if (sub.MessageType == null || !sub.MessageType.IsValid)
        {
          log.Debug("Graph '{GraphId}' has invalid subscription (MessageType: {MessageType}, IsValid: {IsValid})",
            runner.GraphId, sub.MessageType?.Name ?? "null", sub.MessageType?.IsValid ?? false);
          continue;
        }

        var key = (sub.MessageType.Type, sub.DomainFilter ?? string.Empty);

        if (!byTypeDomain.TryGetValue(key, out var list))
        {
          list = new List<IGraphRunner>();
          byTypeDomain[key] = list;
        }

        list.Add(runner);
        subscriptionCount++;

        log.Verbose("Registered graph '{GraphId}' subscription: {MessageType} (domain: '{Domain}')",
          runner.GraphId, sub.MessageType.Name, sub.DomainFilter ?? "(any)");
      }

      log.Verbose("Registered graph '{GraphId}' with {SubscriptionCount} subscription(s) (total graphs: {TotalGraphs})",
        runner.GraphId, subscriptionCount, all.Count);
    }

    /// <summary>Route a MessagePipe message to all matching graph runners</summary>
    public async UniTask RouteAsync(IGameMessage message, string domain = null, CancellationToken ct = default)
    {
      if (message == null)
      {
        log.Warning("Attempted to route null message");
        return;
      }

      var messageType = message.GetType();
      var domainFilter = domain ?? string.Empty;

      if (routeDepth >= MaxRouteDepth)
      {
        log.Error("Event-graph feedback loop detected: routing depth reached {MaxRouteDepth} — dropping message '{MessageType}' (domain: '{Domain}'). A graph is publishing an event it also subscribes to (directly or via a graph cycle); fix the content graph's subscription/publish pair.",
          MaxRouteDepth, messageType.Name, domainFilter);
        return;
      }

      log.Verbose("Routing message '{MessageType}' (domain: '{Domain}') to graphs", messageType.Name, domainFilter);

      // Additive delivery: union the exact-domain bucket and the empty-domain ("any") wildcard bucket.
      // Deduplicate by runner reference identity, dispatched in overall registration order (the order
      // runners appear in `all`), NOT exact-bucket-then-wildcard-bucket concatenation.
      var matchedSet = new HashSet<IGraphRunner>();
      var exactMatched = false;
      var wildcardMatched = false;

      if (byTypeDomain.TryGetValue((messageType, domainFilter), out var exactList))
      {
        foreach (var runner in exactList)
          matchedSet.Add(runner);
        exactMatched = exactList.Count > 0;
      }

      if (domainFilter != string.Empty && byTypeDomain.TryGetValue((messageType, string.Empty), out var wildcardList))
      {
        foreach (var runner in wildcardList)
          matchedSet.Add(runner);
        wildcardMatched = wildcardList.Count > 0;
      }

      if (matchedSet.Count == 0)
      {
        log.Verbose("No graphs subscribed to message '{MessageType}' (domain: '{Domain}'). Available subscriptions: {Subscriptions}",
          messageType.Name, domainFilter, GetAvailableSubscriptionsForType(messageType));
        return;
      }

      var matchedGraphs = all.Where(matchedSet.Contains).ToList();
      var routingStrategy = exactMatched && wildcardMatched
        ? $"additive: exact domain ('{domainFilter}') + wildcard (any domain)"
        : exactMatched
          ? $"exact domain match ('{domainFilter}')"
          : "wildcard domain match (any domain)";

      log.Information("Routing message '{MessageType}' (domain: '{Domain}') to {GraphCount} graph(s) via {Strategy}: {GraphIds}",
        messageType.Name, domainFilter, matchedGraphs.Count, routingStrategy,
        string.Join(", ", matchedGraphs.Select(r => r.GraphId)));

      routeDepth++;
      try
      {
        foreach (var runner in matchedGraphs)
        {
          if (ct.IsCancellationRequested)
          {
            log.Verbose("Message routing cancelled for '{MessageType}'", messageType.Name);
            break;
          }

          if (!TryConsumeDispatchBudget(runner, messageType))
            continue;

          await runner.HandleMessageAsync(message, domain, ct);
        }
      }
      finally
      {
        routeDepth--;
      }
    }

    /// <summary>
    ///   Rate watchdog: returns false (dropping the dispatch) while a runner is suspended, and suspends
    ///   it when it exceeds <see cref="MaxDispatchesPerWindow" /> dispatches inside one rate window.
    /// </summary>
    private bool TryConsumeDispatchBudget(IGraphRunner runner, Type messageType)
    {
      var now = TimeProvider();

      if (!dispatchWindows.TryGetValue(runner, out var window))
      {
        window = new DispatchWindow { WindowStart = now };
        dispatchWindows[runner] = window;
      }

      if (now < window.SuspendedUntil)
        return false;

      if (now - window.WindowStart >= RateWindowSeconds)
      {
        window.WindowStart = now;
        window.Count = 0;
      }

      window.Count++;
      if (window.Count > MaxDispatchesPerWindow)
      {
        window.SuspendedUntil = now + RateSuspendSeconds;
        log.Error("Event-graph feedback loop detected: graph '{GraphId}' exceeded {MaxDispatches} dispatches in {Window}s (last message: '{MessageType}') — suspending its event delivery for {Suspend}s. A graph is republishing an event it also subscribes to (directly or via a graph cycle); fix the content graph's subscription/publish pair.",
          runner.GraphId, MaxDispatchesPerWindow, RateWindowSeconds, messageType.Name, RateSuspendSeconds);
        return false;
      }

      return true;
    }

    private string GetAvailableSubscriptionsForType(Type messageType)
    {
      var subscriptions = byTypeDomain.Keys
        .Where(k => k.messageType == messageType)
        .Select(k => string.IsNullOrEmpty(k.domain) ? "(any domain)" : $"'{k.domain}'")
        .ToList();

      return subscriptions.Count > 0
        ? string.Join(", ", subscriptions)
        : "none";
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
      dispatchWindows.Clear();
    }
  }
}