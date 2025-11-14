using System;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Debug
{
  /// <summary>
  ///   Debug event emitted when a node executes at runtime.
  /// </summary>
  public interface INodeExecutionDebugEvent
  {
    string GraphId { get; }
    string NodeId { get; }
    string NodeType { get; }
    DateTime TimeUtc { get; }
    TimeSpan ExecutionTime { get; }
    object? Payload { get; }
    string? ErrorMessage { get; }
  }

  /// <summary>
  ///   Debug event emitted when graph state changes.
  /// </summary>
  public interface IGraphStateChangeDebugEvent
  {
    string GraphId { get; }
    string StateKey { get; }
    object? OldValue { get; }
    object? NewValue { get; }
    DateTime TimeUtc { get; }
  }

  /// <summary>
  ///   Debug event emitted when a graph starts/stops execution.
  /// </summary>
  public interface IGraphExecutionDebugEvent
  {
    string GraphId { get; }
    string GraphDomain { get; }
    bool IsStarting { get; }
    DateTime TimeUtc { get; }
    string? TriggerMessageType { get; }
  }
}

