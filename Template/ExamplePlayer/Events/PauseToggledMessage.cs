
using MToolKit.Template.ExamplePlayer.Interface;

namespace MToolKit.Template.ExamplePlayer.Events
{
  public readonly struct PauseToggledMessage : IPlayerMessage
  {
    public readonly bool IsPaused;
    
    public PauseToggledMessage(bool isPaused)
    {
      IsPaused = isPaused;
    }
  }
}
