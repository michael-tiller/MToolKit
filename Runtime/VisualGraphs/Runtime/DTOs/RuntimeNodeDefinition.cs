using System;
using MToolKit.Runtime.Utilities.SerializableDictionary;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs
{
    /// <summary>
    /// Concrete implementation of SerializableDictionary for node parameters.
    /// </summary>
    [Serializable]
    public sealed class NodeParametersDictionary : SerializableDictionary<string, object> { }

    /// <summary>
    /// Serializable runtime node definition.
    /// </summary>
    [Serializable]
    public sealed class RuntimeNodeDefinition
    {
        /// <summary>Unique stable node ID (from authoring GUID)</summary>
        [SerializeField] public string NodeId;

        /// <summary>Node type name (matches executor)</summary>
        [SerializeField] public string NodeType;

        /// <summary>Extracted parameters from authoring node</summary>
        [SerializeField] public NodeParametersDictionary Parameters = new();
    }
}

