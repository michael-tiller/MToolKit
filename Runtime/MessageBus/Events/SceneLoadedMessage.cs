// MessageBus/Messages/SceneLoadedMessage.cs

using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.MessageBus.Events
{
  public readonly struct SceneLoadedMessage : IGameMessage
  {
    public readonly string Name;

    public SceneLoadedMessage(string name)
    {
      Name = name;
    }
  }
}