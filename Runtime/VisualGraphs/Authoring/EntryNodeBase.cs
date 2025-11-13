using System;

namespace MToolKit.Runtime.VisualGraphs.Authoring
{
  /// <summary>
  ///   Base class for entry nodes. Entry nodes are automatically queued when a graph handles an event.
  /// </summary>
  public abstract class EntryNodeBase : VisualGraphNodeBase
  {
    /// <summary>
    ///   Output port - connects to the first node(s) to execute
    /// </summary>
    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Next;
  }

  /// <summary>
  ///   Marker for node connections. Not used at runtime, just for type safety in authoring.
  /// </summary>
  [Serializable]
  public struct NodeConnection { }
}