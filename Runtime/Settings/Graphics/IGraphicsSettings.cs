using MToolKit.Runtime.Settings.BoundSettings;

namespace MToolKit.Runtime.Settings.Graphics
{
  public interface IGraphicsSettings
  {
    ReactiveSetting<int> ResolutionIndex { get; }
    ReactiveSetting<int> QualityIndex { get; }
    ReactiveSetting<bool> Fullscreen { get; }
    ReactiveSetting<bool> VerticalSync { get; }
  }
}