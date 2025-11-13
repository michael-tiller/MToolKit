using System;

namespace MToolKit.Runtime.VisualGraphs.Runtime.DTOs
{
  /// <summary>
  ///   Serializable runtime connection definition.
  /// </summary>
  [Serializable]
  public sealed class RuntimeConnectionDefinition
  {
    /// <summary>Source node ID</summary>
    public string FromNodeId;

    /// <summary>Target node ID</summary>
    public string ToNodeId;

    /// <summary>Port name on source node</summary>
    public string PortName;
  }
}