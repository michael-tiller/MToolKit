using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.VisualGraphs.Bootstrap
{
    /// <summary>
    /// Bridges R3 observable events to the graph event router.
    /// Subscribe to IEventMessage observable and route to graphs.
    /// </summary>
    public sealed class EventBusBridgeMB : MonoBehaviour
    {
        private static readonly Lazy<ILogger> _logLazy = new(() => 
            Log.Logger.ForContext<EventBusBridgeMB>().ForFeature("VisualGraphs"));
        private static ILogger log => _logLazy.Value ?? Serilog.Core.Logger.None;

        private GraphEventRouter _router;
        private IDisposable _subscription;
        private CancellationTokenSource _cts;

        [Inject]
        public void Construct(GraphEventRouter router)
        {
            _router = router;
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();
            
            // TODO: Subscribe to your R3 IEventMessage observable here
            // Example:
            // _subscription = eventBusObservable
            //     .Subscribe(OnEventReceived);
            
            log.Information("EventBusBridge started (subscription implementation needed)");
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void OnEventReceived(IEventMessage message)
        {
            if (message == null || _router == null) return;

            // Route event to graphs
            RouteEventAsync(message, _cts.Token).Forget();
        }

        private async UniTask RouteEventAsync(IEventMessage message, CancellationToken ct)
        {
            try
            {
                await _router.RouteAsync(message, ct);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Failed to route event {EventType} in domain {Domain}", 
                    message.Type, message.Domain);
            }
        }
    }
}

