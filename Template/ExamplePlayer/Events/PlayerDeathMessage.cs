
using MToolKit.Template.ExamplePlayer.Interface;

namespace MToolKit.Template.ExamplePlayer.Events
{
  
  /// <summary>
  /// Message published when a player dies, containing the cause of death
  /// </summary>
  public readonly struct PlayerDeathMessage : IPlayerMessage
  {
    public readonly string CauseOfDeath;
    
    public PlayerDeathMessage(string causeOfDeath)
    {
      CauseOfDeath = causeOfDeath;
    }
  }
}
