using System;

namespace MToolKit.Runtime.Input.Interfaces
{
    /// <summary>
    /// Abstraction layer for input handling to provide loose coupling
    /// and enable easy switching between input systems or mocking for tests.
    /// </summary>
    public interface IInputService
    {
        /// <summary>
        /// Event fired when the pause action is triggered
        /// </summary>
        event Action OnPausePressed;
        
        /// <summary>
        /// Event fired when any key is pressed (for bootstrapper)
        /// </summary>
        event Action OnAnyKeyPressed;
        
        /// <summary>
        /// Initialize the input service with the given input action asset
        /// </summary>
        /// <param name="inputActionAsset">The input action asset to use</param>
        void Initialize(object inputActionAsset);
        
        /// <summary>
        /// Enable input processing
        /// </summary>
        void Enable();
        
        /// <summary>
        /// Disable input processing
        /// </summary>
        void Disable();
        
        /// <summary>
        /// Check if a specific action is currently pressed
        /// </summary>
        /// <param name="action">The action to check</param>
        /// <returns>True if the action is pressed</returns>
        bool IsActionPressed(object action);
        
        /// <summary>
        /// Check if a specific action was pressed this frame
        /// </summary>
        /// <param name="action">The action to check</param>
        /// <returns>True if the action was pressed this frame</returns>
        bool WasActionPressedThisFrame(object action);
        
        /// <summary>
        /// Check if a specific action was released this frame
        /// </summary>
        /// <param name="action">The action to check</param>
        /// <returns>True if the action was released this frame</returns>
        bool WasActionReleasedThisFrame(object action);
    }
}
