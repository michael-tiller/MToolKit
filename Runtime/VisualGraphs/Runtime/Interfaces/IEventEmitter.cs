using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
  /// <summary>
  ///   Interface for emitting MessagePipe messages from graph executors.
  /// </summary>
  public interface IEventEmitter
  {
    /// <summary>Emit a MessagePipe message</summary>
    void Emit(IGameMessage message, string domain = null);
  }
}