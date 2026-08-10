using System;
using UnityEngine;

namespace MToolKit.Runtime.Input.Interfaces
{
  /// <summary>
  ///   Abstraction layer for input handling to provide loose coupling
  ///   and enable easy switching between input systems or mocking for tests.
  /// </summary>
  public interface IInputService
  {
    /// <summary>
    ///   Event fired when the pause action is triggered
    /// </summary>
    event Action OnPausePressed;

    /// <summary>
    ///   Event fired when any key is pressed (for bootstrapper)
    /// </summary>
    event Action OnAnyKeyPressed;

    /// <summary>
    ///   Initialize the input service with the given input action asset
    /// </summary>
    /// <param name="inputActionAsset">The input action asset to use</param>
    void Initialize(object inputActionAsset);

    /// <summary>
    ///   Enable input processing
    /// </summary>
    void Enable();

    /// <summary>
    ///   Disable input processing
    /// </summary>
    void Disable();

    /// <summary>
    ///   Check if a specific action is currently pressed
    /// </summary>
    /// <param name="action">The action to check</param>
    /// <returns>True if the action is pressed</returns>
    bool IsActionPressed(object action);

    /// <summary>
    ///   Check if a specific action was pressed this frame
    /// </summary>
    /// <param name="action">The action to check</param>
    /// <returns>True if the action was pressed this frame</returns>
    bool WasActionPressedThisFrame(object action);

    /// <summary>
    ///   Check if a specific action was released this frame
    /// </summary>
    /// <param name="action">The action to check</param>
    /// <returns>True if the action was released this frame</returns>
    bool WasActionReleasedThisFrame(object action);

    /// <summary>
    ///   Current pointer (mouse) position in screen space. <see cref="Vector2.zero"/> when no
    ///   pointer device is present. Consumers that need per-frame pointer sampling read this
    ///   instead of touching the input backend statics directly, so the source is mockable.
    /// </summary>
    Vector2 PointerPosition { get; }

    /// <summary>True on the frame the primary (left) pointer button was pressed.</summary>
    bool LeftButtonPressedThisFrame { get; }

    /// <summary>True on the frame the primary (left) pointer button was released.</summary>
    bool LeftButtonReleasedThisFrame { get; }

    /// <summary>True while the primary (left) pointer button is held down.</summary>
    bool LeftButtonHeld { get; }

    /// <summary>True on the frame the secondary (right) pointer button was pressed.</summary>
    bool RightButtonPressedThisFrame { get; }

    /// <summary>True on the frame the secondary (right) pointer button was released.</summary>
    bool RightButtonReleasedThisFrame { get; }

    /// <summary>True while the secondary (right) pointer button is held down.</summary>
    bool RightButtonHeld { get; }

    /// <summary>
    ///   True on the frame the cancel/escape input was pressed. Exposed as a first-class edge —
    ///   alongside the action API above — so modal/armed-tool consumers can sample "was cancel
    ///   pressed this frame" through the mockable seam without holding an <c>InputAction</c> handle.
    /// </summary>
    bool CancelPressedThisFrame { get; }
  }
}