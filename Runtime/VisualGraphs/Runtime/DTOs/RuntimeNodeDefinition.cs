using System;
using MToolKit.Runtime.Utilities.SerializableDictionary;

namespace MToolKit.Runtime.VisualGraphs.Runtime.DTOs
{
  /// <summary>
  ///   Concrete implementation of SerializableDictionary for node parameters.
  /// </summary>
  [Serializable]
  public sealed class NodeParametersDictionary : SerializableDictionary<string, object> { }

  /// <summary>
  ///   Serializable runtime node definition.
  /// </summary>
  [Serializable]
  public sealed class RuntimeNodeDefinition
  {
    /// <summary>Unique stable node ID (from authoring GUID)</summary>
    public string NodeId;

    /// <summary>Node type name (matches executor)</summary>
    public string NodeType;

    /// <summary>Extracted parameters from authoring node</summary>
    public NodeParametersDictionary Parameters = new();
  }
}