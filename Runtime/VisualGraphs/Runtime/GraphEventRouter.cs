using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Routes events to graph runners with O(1) indexed lookups by (type, domain).
    /// </summary>
    public sealed class GraphEventRouter
    {
        private readonly Dictionary<(string type, string domain), List<IGraphRunner>> _byTypeDomain = new();
        private readonly List<IGraphRunner> _all = new();

        /// <summary>Register a graph runner and index its subscriptions</summary>
        public void RegisterRunner(IGraphRunner runner)
        {
            if (runner == null)
                throw new ArgumentNullException(nameof(runner));

            _all.Add(runner);

            foreach (var sub in runner.Definition.Subscriptions)
            {
                var key = (sub.EventType, sub.EventDomain ?? string.Empty);
                
                if (!_byTypeDomain.TryGetValue(key, out var list))
                {
                    list = new List<IGraphRunner>();
                    _byTypeDomain[key] = list;
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
            if (_byTypeDomain.TryGetValue(key, out var list))
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
            if (_byTypeDomain.TryGetValue(key, out list))
            {
                foreach (var runner in list)
                {
                    if (ct.IsCancellationRequested) break;
                    await runner.HandleEventAsync(message, ct);
                }
            }
        }

        /// <summary>Get all registered runners</summary>
        public IEnumerable<IGraphRunner> GetRunners()
        {
            return _all;
        }

        /// <summary>Clear all runners</summary>
        public void Clear()
        {
            _all.Clear();
            _byTypeDomain.Clear();
        }
    }
}

