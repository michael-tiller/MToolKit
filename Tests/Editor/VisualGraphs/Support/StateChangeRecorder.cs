using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Subscribes to <see cref="NodeDebugEvents.StateChanged" /> and records state-mutation debug events
  ///   for assertions (sibling of <see cref="DebugEventRecorder" />, which covers node/graph execution).
  ///   Dispose (or a fixture's ClearAllSubscribers) detaches the handler.
  /// </summary>
  public sealed class StateChangeRecorder : IDisposable
  {
    public StateChangeRecorder()
    {
      NodeDebugEvents.StateChanged += OnStateChanged;
    }

    public List<(string graphId, string key, object oldValue, object newValue)> Changes { get; } = new();

    public void Dispose()
    {
      NodeDebugEvents.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(IGraphStateChangeDebugEvent e)
    {
      Changes.Add((e.GraphId, e.StateKey, e.OldValue, e.NewValue));
    }
  }
}
