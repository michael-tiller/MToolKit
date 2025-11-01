#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MToolKit.Runtime.Editor
{
    /// <summary>
    /// Model representing the current state of a plugin for diagnostic purposes.
    /// </summary>
    [Serializable]
    public class PluginStateModel
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public GameObject GameObject { get; set; }
        public EPluginStatus Status { get; set; }
        public string CurrentPhase { get; set; }
        public bool IsRuntimeSystem { get; set; }
        public bool IsRunning { get; set; }
        public List<DependencyModel> Dependencies { get; set; } = new();
        public string ErrorMessage { get; set; }
        public DateTime InitializationTime { get; set; }
        public PluginPerformanceMetrics PerformanceMetrics { get; set; } = new();
    }
}

#endif
