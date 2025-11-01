using System.Collections.Generic;
using R3;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Interfaces
{
public interface IInputRebinderService
{
    Subject<string> OnBindingConflictDetected { get; }
    Subject<(InputAction action, int bindingIndex)> OnRebindingStarted { get; }
    Subject<(InputAction action, int bindingIndex, bool completed)> OnRebindingCompleted { get; }
    Subject<(string conflictingPath, InputAction conflictingAction, int conflictingBindingIndex)> OnBindingConflictFound { get; }

    void RegisterAction(InputAction action);
    bool StartInteractiveRebinding(InputAction action, int bindingIndex, string[] excludeControls = null);
    void CancelRebinding();
    bool IsRebinding { get; }
    void RemoveBindingOverride(InputAction action, int bindingIndex);
    void ResetActionBindings(InputAction action);
    void ResetAllBindings();
    bool CheckBindingConflict(InputAction action, int bindingIndex, string newBindingPath);
    bool ResolveBindingConflict(InputAction conflictingAction, int conflictingBindingIndex);
    List<(InputAction action, int bindingIndex)> GetActionsUsingBindingPath(string bindingPath);
    IReadOnlyCollection<InputAction> RegisteredActions { get; }
}
}