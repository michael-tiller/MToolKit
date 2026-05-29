namespace MToolKit.Runtime.MessageBus.Interfaces
{
  /// <summary>
  /// Opt-in interface for messages that carry a routing domain.
  /// EventBusBridge extracts this and passes it to GraphEventRouter,
  /// enabling EventEntryNode.DomainFilter to discriminate per-instance graph execution.
  /// </summary>
  public interface IDomainMessage : IGameMessage
  {
    string Domain { get; }
  }
}
