// MessageBus/Messages/FadeBlackoutMessage.cs
using MToolKit.Template.ExamplePlayer.Interface;

namespace MToolKit.Runtime.ExamplePlayer.Events
{
  public readonly struct FadeBlackoutMessage : IPlayerMessage
  {
    public readonly float Alpha;
    public readonly float Duration;
    public FadeBlackoutMessage(float alpha, float duration)
    {
      Alpha = alpha;
      Duration = duration;
    }
  }
}