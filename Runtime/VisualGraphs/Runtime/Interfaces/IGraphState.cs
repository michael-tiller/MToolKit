using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
    /// <summary>
    ///   Per-graph state container for variables and data.
    /// </summary>
    public interface IGraphState
  {
    /// <summary>Try to get a value by key</summary>
    bool TryGet<T>(string key, out T value);

    /// <summary>Set a value by key</summary>
    void Set<T>(string key, T value);

    /// <summary>Check if key exists</summary>
    bool Contains(string key);

    /// <summary>Get read-only view of all data</summary>
    IReadOnlyDictionary<string, object> AsReadOnly();
  }
}