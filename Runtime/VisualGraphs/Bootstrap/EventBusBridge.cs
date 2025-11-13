using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Bootstrap
{
  /// <summary>
  ///   Bridges R3 observable events to the graph event router.
  ///   Subscribe to IEventMessage observable and route to graphs.
  /// </summary>
  public sealed class EventBusBridge : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<EventBusBridge>().ForFeature("VisualGraphs"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private CancellationTokenSource cts;

    private GraphEventRouter router;
    private IDisposable subscription;

    private void Start()
    {
      cts = new CancellationTokenSource();

      // TODO: Subscribe to your R3 IEventMessage observable here
      // Example:
      // _subscription = eventBusObservable
      //     .Subscribe(OnEventReceived);

      log.Information("EventBusBridge started (subscription implementation needed)");
    }

    private void OnDestroy()
    {
      subscription?.Dispose();
      cts?.Cancel();
      cts?.Dispose();
    }

    [Inject]
    public void Construct(GraphEventRouter router)
    {
      this.router = router;
    }

    private void OnEventReceived(IEventMessage message)
    {
      if (message == null || router == null) return;

      // Route event to graphs
      RouteEventAsync(message, cts.Token).Forget();
    }

    private async UniTask RouteEventAsync(IEventMessage message, CancellationToken ct)
    {
      try
      {
        await router.RouteAsync(message, ct);
      }
      catch (Exception ex)
      {
        log.Error(ex, "Failed to route event {EventType} in domain {Domain}",
          message.Type, message.Domain);
      }
    }
  }
}