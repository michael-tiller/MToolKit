using VContainer;

namespace MToolKit.Runtime.Core.Interfaces
{
  /// <summary>
  ///   Interface for a game plugin.
  /// </summary>
  public interface IGamePlugin
  {
    /// <summary>
    ///   Register the plugin with the container builder.
    /// </summary>
    /// <param name="builder">The container builder.</param>
    void Register(IContainerBuilder builder);
  }
}