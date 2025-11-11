using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// In-memory graph state implementation.
    /// </summary>
    public sealed class InMemoryGraphState : IGraphState
    {
        private readonly Dictionary<string, object> _data = new();

        public bool TryGet<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            _data[key] = value;
        }

        public bool Contains(string key)
        {
            return _data.ContainsKey(key);
        }

        public IReadOnlyDictionary<string, object> AsReadOnly()
        {
            return _data;
        }
    }
}

