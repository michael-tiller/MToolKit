using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Messages
{
  /// <summary>
  ///   Emitted when a graph's state value changes.
  ///   Allows other graphs to react to state changes reactively.
  ///   Example: Graph A sets "player_has_key" = true, Graph B subscribes to GraphStateChangedMessage
  ///   and reacts when the state key matches "player_has_key".
  /// </summary>
  public readonly struct GraphStateChangedMessage : IGameMessage
  {
    public override string ToString()
    {
      return $"GraphStateChangedMessage: GraphId={GraphId}, StateKey={StateKey}, OldValue={OldValue}, NewValue={NewValue}";
    }

    /// <summary>
    ///   ID of the graph whose state changed
    /// </summary>
    public readonly string GraphId;

    /// <summary>
    ///   State key that changed
    /// </summary>
    public readonly string StateKey;

    /// <summary>
    ///   Previous value (null if key didn't exist)
    /// </summary>
    public readonly object OldValue;

    /// <summary>
    ///   New value
    /// </summary>
    public readonly object NewValue;

    public GraphStateChangedMessage(
      string graphId,
      string stateKey,
      object oldValue,
      object newValue)
    {
      GraphId = graphId;
      StateKey = stateKey;
      OldValue = oldValue;
      NewValue = newValue;
    }
  }
}

