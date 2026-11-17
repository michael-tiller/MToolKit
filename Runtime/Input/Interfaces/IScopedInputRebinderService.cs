using System;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Interfaces
{
  /// <summary>Optional lifecycle ownership for runtime actions that outlive a UI session only while registered.</summary>
  public interface IScopedInputRebinderService
  {
    IDisposable RegisterScopedAction(InputAction action);
  }
}
