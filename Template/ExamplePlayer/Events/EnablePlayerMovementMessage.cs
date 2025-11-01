
using MToolKit.Template.ExamplePlayer.Interface;

namespace MToolKit.Template.ExamplePlayer.Events
{
  /// <summary>
  /// Message published when the player movement is enabled or disabled.
  /// </summary>
  public readonly struct EnablePlayerMovementMessage : IPlayerMessage
  {
    public readonly bool Enable;
    
    public EnablePlayerMovementMessage(bool enable)
    {
      Enable = enable;
    }
  }
}
