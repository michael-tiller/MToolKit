using MToolKit.Runtime.Settings.Interfaces;

namespace MToolKit.Runtime.Settings.BoundSettings
{
  public abstract class AbstractBoundReactiveSetting<T> : IBoundReactiveSetting<T>
  {
    public abstract ReactiveSetting<T> Setting { get; set; }
    public abstract void Bind(ReactiveSetting<T> reactiveSetting);
  }
}