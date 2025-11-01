using System;

namespace MToolKit.Runtime.Settings.BoundSettings
{
  public class EnumBoundReactiveSetting<T> : AbstractBoundReactiveSetting<T> where T : Enum
  {
    public override ReactiveSetting<T> Setting { get; set; }
    public override void Bind(ReactiveSetting<T> reactiveSetting)
    {
      Setting = reactiveSetting;
    }
  }
}