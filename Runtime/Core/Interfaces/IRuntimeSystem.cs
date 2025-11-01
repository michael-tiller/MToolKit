namespace MToolKit.Runtime.Core.Interfaces
{
    /// <summary>
    /// Interface for runtime systems that need to be initialized, ticked, and shutdown.
    /// </summary>
    public interface IRuntimeSystem
    {
        /// <summary>
        /// Initialize the runtime system.
        /// </summary>
        void Start();

        /// <summary>
        /// Tick the runtime system.
        /// </summary>
        /// <param name="deltaTime">The time since the last tick.</param>
        void Tick(float deltaTime);

        /// <summary>
        /// Late tick the runtime system.
        /// </summary>
        /// <param name="deltaTime">The time since the last tick.</param>
        void LateTick(float deltaTime);

        /// <summary>
        /// Fixed tick the runtime system.
        /// </summary>
        /// <param name="deltaTime">The time since the last tick.</param>
        void FixedTick(float deltaTime);

        /// <summary>
        /// Shutdown the runtime system.
        /// </summary>
        void Shutdown();
    }
}
