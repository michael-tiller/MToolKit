using System;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Context provided to node executors for controlling continuation and accessing services.
    /// </summary>
    public sealed class GraphNodeExecutionContext
    {
        private readonly NodeExecutionQueue _queue;
        private readonly IServiceProvider _services;
        private readonly IEventEmitter _eventEmitter;

        public GraphNodeExecutionContext(
            NodeExecutionQueue queue,
            IServiceProvider services,
            IEventEmitter eventEmitter)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
        }

        /// <summary>Enqueue next node for execution (executor-controlled continuation)</summary>
        public void EnqueueNext(string nodeId)
        {
            _queue.Enqueue(nodeId);
        }

        /// <summary>Resolve a service from DI container</summary>
        public T Resolve<T>() where T : class
        {
            return _services.GetService(typeof(T)) as T;
        }

        /// <summary>Emit an event</summary>
        public void Emit(IEventMessage message)
        {
            _eventEmitter.Emit(message);
        }
    }
}

