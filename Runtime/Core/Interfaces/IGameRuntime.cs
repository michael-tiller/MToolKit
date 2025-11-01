namespace MToolKit.Runtime.Core.Interfaces
{
  /// <summary>
  /// Interface for a game runtime.
  /// </summary>
  public interface IGameRuntime
  {
    /// <summary>
    /// Start the game runtime.
    /// </summary>
    void Start();

    /// <summary>
    /// Tick the game runtime.
    /// </summary>
    /// <param name="deltaTime">The delta time.</param>
    void Tick(float deltaTime);

    /// <summary>
    /// Late tick the game runtime.
    /// </summary>
    /// <param name="deltaTime">The delta time.</param>
    void LateTick(float deltaTime);

    /// <summary>
    /// Fixed tick the game runtime.
    /// </summary>
    /// <param name="deltaTime">The delta time.</param>
    void FixedTick(float deltaTime);

    /// <summary>
    /// Shutdown the game runtime.
    /// </summary>
    void Shutdown();
  }
}