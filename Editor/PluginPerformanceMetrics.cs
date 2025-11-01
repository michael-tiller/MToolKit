#if UNITY_EDITOR

using System;

namespace MToolKit.Runtime.Editor
{
    /// <summary>
    /// Model representing performance metrics for a plugin in diagnostic purposes.
    /// </summary>
    [Serializable]
    public class PluginPerformanceMetrics
    {
        public int UpdateCallsPerSecond { get; set; }
        public float AverageUpdateTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}

#endif
