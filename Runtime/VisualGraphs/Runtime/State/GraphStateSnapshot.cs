using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs.Runtime.State
{
  /// <summary>
  ///   Serializable graph state snapshot for save/restore.
  /// </summary>
  [Serializable]
  public sealed class GraphStateSnapshot
  {
    /// <summary>Graph ID this snapshot belongs to</summary>
    public string GraphId;

    /// <summary>Last processed event sequence ID</summary>
    public long LastSequenceId;

    /// <summary>State data</summary>
    public Dictionary<string, object> Data = new();
  }
}