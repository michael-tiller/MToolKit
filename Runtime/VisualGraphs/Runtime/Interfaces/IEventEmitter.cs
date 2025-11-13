namespace MToolKit.Runtime.VisualGraphs.Runtime.Interfaces
{
    /// <summary>
    ///   Interface for emitting events from graph executors.
    /// </summary>
    public interface IEventEmitter
  {
    /// <summary>Emit an event</summary>
    void Emit(IEventMessage message);
  }
}