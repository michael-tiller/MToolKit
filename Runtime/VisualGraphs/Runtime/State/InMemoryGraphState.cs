using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.State
{
  /// <summary>
  ///   In-memory graph state implementation.
  /// </summary>
  public sealed class InMemoryGraphState : IGraphState
  {
    private readonly Dictionary<string, object> data = new();

    public bool TryGet<T>(string key, out T value)
    {
      if (data.TryGetValue(key, out var obj) && obj is T typedValue)
      {
        value = typedValue;
        return true;
      }
      value = default;
      return false;
    }

    public void Set<T>(string key, T value)
    {
      data[key] = value;
    }

    public bool Contains(string key)
    {
      return data.ContainsKey(key);
    }

    public IReadOnlyDictionary<string, object> AsReadOnly()
    {
      return data;
    }
  }
}