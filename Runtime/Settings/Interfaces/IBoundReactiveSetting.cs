using MToolKit.Runtime.Settings.BoundSettings;

namespace MToolKit.Runtime.Settings.Interfaces
{
  public interface IBoundReactiveSetting<T>
  {
    ReactiveSetting<T> Setting { get; }
    void Bind(ReactiveSetting<T> reactiveSetting);
  }
}