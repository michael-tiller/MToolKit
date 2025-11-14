#nullable enable
using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.State
{
  /// <summary>
  ///   Wrapper around IGraphState that emits debug events for state changes.
  /// </summary>
  public sealed class DebuggableGraphState : IGraphState
  {
    private readonly IGraphState inner;
    private readonly string graphId;

    public DebuggableGraphState(IGraphState inner, string graphId)
    {
      this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
      this.graphId = graphId ?? throw new ArgumentNullException(nameof(graphId));
    }

    public bool TryGet<T>(string key, out T value)
    {
      return inner.TryGet(key, out value);
    }

    public void Set<T>(string key, T value)
    {
      object? oldValue = null;
      if (inner.Contains(key))
      {
        inner.TryGet<object>(key, out oldValue);
      }

      inner.Set(key, value);

      // Emit debug event for state change
      NodeDebugEvents.RaiseStateChanged(graphId, key, oldValue, value);
    }

    public bool Contains(string key)
    {
      return inner.Contains(key);
    }

    public IReadOnlyDictionary<string, object> AsReadOnly()
    {
      return inner.AsReadOnly();
    }
  }
}

