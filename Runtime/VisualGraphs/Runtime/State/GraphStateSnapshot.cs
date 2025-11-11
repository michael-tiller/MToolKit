using System;
using System.Collections.Generic;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Serializable graph state snapshot for save/restore.
    /// </summary>
    [Serializable]
    public sealed class GraphStateSnapshot
    {
        /// <summary>Graph ID this snapshot belongs to</summary>
        [SerializeField] public string GraphId;
        
        /// <summary>State data</summary>
        [SerializeField] public Dictionary<string, object> Data = new();
        
        /// <summary>Last processed event sequence ID</summary>
        [SerializeField] public long LastSequenceId;
    }
}

