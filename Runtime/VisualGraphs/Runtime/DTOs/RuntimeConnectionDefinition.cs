using System;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Serializable runtime connection definition.
    /// </summary>
    [Serializable]
    public sealed class RuntimeConnectionDefinition
    {
        /// <summary>Source node ID</summary>
        [SerializeField] public string FromNodeId;
        
        /// <summary>Target node ID</summary>
        [SerializeField] public string ToNodeId;
        
        /// <summary>Port name on source node</summary>
        [SerializeField] public string PortName;
    }
}

