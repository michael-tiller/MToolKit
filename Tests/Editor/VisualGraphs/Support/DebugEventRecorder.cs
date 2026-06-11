using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Subscribes to the static <see cref="NodeDebugEvents" /> surface and records node-execution and
  ///   graph-execution events for assertions. Used scoped to NODE execution (which nodes ran); do not use
  ///   it to assert global start/end pairing — several GraphRunner paths raise start then early-return
  ///   before end. Dispose (or a fixture's ClearAllSubscribers) detaches the handlers.
  /// </summary>
  public sealed class DebugEventRecorder : IDisposable
  {
    public DebugEventRecorder()
    {
      NodeDebugEvents.NodeExecuted += OnNodeExecuted;
      NodeDebugEvents.GraphExecutionChanged += OnGraphExecutionChanged;
    }

    public List<(string graphId, string nodeId, string nodeType, string error)> NodeExecuted { get; } = new();
    public List<(string graphId, bool isStarting)> GraphExecution { get; } = new();

    public void Dispose()
    {
      NodeDebugEvents.NodeExecuted -= OnNodeExecuted;
      NodeDebugEvents.GraphExecutionChanged -= OnGraphExecutionChanged;
    }

    private void OnNodeExecuted(INodeExecutionDebugEvent e)
    {
      NodeExecuted.Add((e.GraphId, e.NodeId, e.NodeType, e.ErrorMessage));
    }

    private void OnGraphExecutionChanged(IGraphExecutionDebugEvent e)
    {
      GraphExecution.Add((e.GraphId, e.IsStarting));
    }
  }
}
