using System;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Execution
{
  /// <summary>
  ///   Context provided to node executors for controlling continuation and accessing services.
  /// </summary>
  public sealed class GraphNodeExecutionContext
  {
    private readonly IEventEmitter eventEmitter;
    private readonly NodeExecutionQueue queue;
    private readonly IServiceProvider services;

    public GraphNodeExecutionContext(
      NodeExecutionQueue queue,
      IServiceProvider services,
      IEventEmitter eventEmitter)
    {
      this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
      this.services = services ?? throw new ArgumentNullException(nameof(services));
      this.eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
    }

    /// <summary>Enqueue next node for execution (executor-controlled continuation)</summary>
    public void EnqueueNext(string nodeId)
    {
      queue.Enqueue(nodeId);
    }

    /// <summary>Resolve a service from DI container</summary>
    public T Resolve<T>() where T : class
    {
      return services.GetService(typeof(T)) as T;
    }

    /// <summary>Emit an event</summary>
    public void Emit(IEventMessage message)
    {
      eventEmitter.Emit(message);
    }
  }
}