using System;
using System.Diagnostics;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Debug
{
  /// <summary>
  ///   Static event system for runtime graph debugging.
  ///   Emits events that editor tools can subscribe to.
  /// </summary>
  public static class NodeDebugEvents
  {
    public static event Action<INodeExecutionDebugEvent>? NodeExecuted;
    public static event Action<IGraphStateChangeDebugEvent>? StateChanged;
    public static event Action<IGraphExecutionDebugEvent>? GraphExecutionChanged;

    public static void RaiseNodeExecuted(
      string graphId,
      string nodeId,
      string nodeType,
      TimeSpan executionTime,
      object? payload = null,
      string? errorMessage = null)
    {
      var evt = new NodeExecutionDebugEvent(
        graphId,
        nodeId,
        nodeType,
        executionTime,
        payload,
        errorMessage);
      NodeExecuted?.Invoke(evt);
    }

    public static void RaiseStateChanged(
      string graphId,
      string stateKey,
      object? oldValue,
      object? newValue)
    {
      var evt = new GraphStateChangeDebugEvent(
        graphId,
        stateKey,
        oldValue,
        newValue);
      StateChanged?.Invoke(evt);
    }

    public static void RaiseGraphExecutionChanged(
      string graphId,
      string graphDomain,
      bool isStarting,
      string? triggerMessageType = null)
    {
      var evt = new GraphExecutionDebugEvent(
        graphId,
        graphDomain,
        isStarting,
        triggerMessageType);
      GraphExecutionChanged?.Invoke(evt);
    }
  }

  internal sealed class NodeExecutionDebugEvent : INodeExecutionDebugEvent
  {
    public string GraphId { get; }
    public string NodeId { get; }
    public string NodeType { get; }
    public DateTime TimeUtc { get; }
    public TimeSpan ExecutionTime { get; }
    public object? Payload { get; }
    public string? ErrorMessage { get; }

    public NodeExecutionDebugEvent(
      string graphId,
      string nodeId,
      string nodeType,
      TimeSpan executionTime,
      object? payload = null,
      string? errorMessage = null)
    {
      GraphId = graphId;
      NodeId = nodeId;
      NodeType = nodeType;
      TimeUtc = DateTime.UtcNow;
      ExecutionTime = executionTime;
      Payload = payload;
      ErrorMessage = errorMessage;
    }
  }

  internal sealed class GraphStateChangeDebugEvent : IGraphStateChangeDebugEvent
  {
    public string GraphId { get; }
    public string StateKey { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public DateTime TimeUtc { get; }

    public GraphStateChangeDebugEvent(
      string graphId,
      string stateKey,
      object? oldValue,
      object? newValue)
    {
      GraphId = graphId;
      StateKey = stateKey;
      OldValue = oldValue;
      NewValue = newValue;
      TimeUtc = DateTime.UtcNow;
    }
  }

  internal sealed class GraphExecutionDebugEvent : IGraphExecutionDebugEvent
  {
    public string GraphId { get; }
    public string GraphDomain { get; }
    public bool IsStarting { get; }
    public DateTime TimeUtc { get; }
    public string? TriggerMessageType { get; }

    public GraphExecutionDebugEvent(
      string graphId,
      string graphDomain,
      bool isStarting,
      string? triggerMessageType = null)
    {
      GraphId = graphId;
      GraphDomain = graphDomain;
      IsStarting = isStarting;
      TimeUtc = DateTime.UtcNow;
      TriggerMessageType = triggerMessageType;
    }
  }
}

