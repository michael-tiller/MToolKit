using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Input
{
  /// <summary>
  ///   Centralized service for managing input rebinding using Unity's interactive rebinding extensions
  ///   Provides conflict detection and binding management with proper interactive rebinding
  /// </summary>
  [Serializable]
  public class InputRebinderService : IDisposable, IInputRebinderService
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputRebinderService>().ForFeature("Core.Services.InputRebinderService"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public Subject<(InputAction action, int bindingIndex, string newPath)> OnBindingChanged { get; } = new();
    public Subject<string> OnBindingConflictDetected { get; } = new();
    public Subject<(InputAction action, int bindingIndex)> OnRebindingStarted { get; } = new();
    public Subject<(InputAction action, int bindingIndex, bool completed)> OnRebindingCompleted { get; } = new();
    public Subject<(string conflictingPath, InputAction conflictingAction, int conflictingBindingIndex)> OnBindingConflictFound { get; } = new();

    [ShowInInspector]
    [ReadOnly]
    private readonly Dictionary<string, InputAction> registeredActions = new();

    [ShowInInspector]
    [ReadOnly]
    private readonly Dictionary<string, List<InputBinding>> originalBindings = new();

    [ShowInInspector]
    [ReadOnly]
    private InputActionRebindingExtensions.RebindingOperation currentRebindingOperation;

    [ShowInInspector]
    [ReadOnly]
    private bool isRebinding;

    [ShowInInspector]
    [ReadOnly]
    private string hash => GetHashCode().ToString();

    /// <summary>
    ///   Register an input action for rebinding management
    /// </summary>
    /// <param name="action">The input action to register</param>
    public void RegisterAction(InputAction action)
    {
      if (action == null)
      {
        log.ForMethod().Error("Cannot register null input action");
        return;
      }

      string key = GetActionKey(action);
      if (registeredActions.ContainsKey(key))
      {
        log.ForMethod().Warning("Action {ActionName} is already registered", action.name);
        return;
      }

      registeredActions[key] = action;

      // Store original bindings for reset functionality
      List<InputBinding> bindings = new();
      foreach (InputBinding binding in action.bindings)
        bindings.Add(binding);
      originalBindings[key] = bindings;

      log.ForMethod().Verbose("Registered action {ActionName} for rebinding", action.name);
    }

    /// <summary>
    ///   Start an interactive rebinding operation for a specific action and binding index
    ///   Handles both regular bindings and composite bindings properly
    /// </summary>
    /// <param name="action">The action to rebind</param>
    /// <param name="bindingIndex">The binding index to rebind</param>
    /// <param name="excludeControls">Controls to exclude from rebinding (e.g., "Mouse" to exclude mouse movement)</param>
    /// <returns>True if rebinding was started successfully</returns>
    public bool StartInteractiveRebinding(InputAction action, int bindingIndex, string[] excludeControls = null)
    {
      if (action == null)
      {
        log.ForMethod().Error("Cannot start interactive rebinding: action is null");
        return false;
      }

      if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
      {
        log.ForMethod().Error("Invalid binding index {BindingIndex} for action {ActionName}", bindingIndex, action.name);
        return false;
      }

      if (isRebinding)
      {
        log.ForMethod().Warning("Cannot start rebinding: another rebinding operation is already in progress");
        return false;
      }

      // Register the action if not already registered
      RegisterAction(action);

      // Cancel any existing rebinding operation
      currentRebindingOperation?.Cancel();
      currentRebindingOperation?.Dispose();

      // Check if this is a composite binding
      InputBinding binding = action.bindings[bindingIndex];
      if (binding.isComposite)
        // For composite bindings, we need to rebind each part in a sequence
        return StartCompositeRebinding(action, bindingIndex, excludeControls);
      // For regular bindings, use standard rebinding
      return StartSingleBindingRebinding(action, bindingIndex, excludeControls);
    }

    /// <summary>
    ///   Start rebinding for a single (non-composite) binding
    /// </summary>
    private bool StartSingleBindingRebinding(InputAction action, int bindingIndex, string[] excludeControls)
    {
      // Disable the action map to prevent interference
      action.actionMap.Disable();

      // Configure the rebinding operation
      currentRebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
        .OnCancel(_ =>
        {
          log.ForMethod().Information("Rebinding cancelled for {ActionName} binding {BindingIndex}", action.name, bindingIndex);
          OnRebindingCompleted.OnNext((action, bindingIndex, false));
          CleanupRebindingOperation();
        })
        .OnComplete(operation =>
        {
          string newPath = operation.action.bindings[bindingIndex].effectivePath;

          // Check for conflicts
          if (CheckBindingConflict(action, bindingIndex, newPath))
          {
            log.ForMethod().Warning("Binding conflict detected for {ActionName} binding {BindingIndex} with path {NewPath}",
              action.name, bindingIndex, newPath);
            // Revert the binding
            action.RemoveBindingOverride(bindingIndex);
            OnRebindingCompleted.OnNext((action, bindingIndex, false));
          }
          else
          {
            log.ForMethod().Information("Rebinding completed for {ActionName} binding {BindingIndex} to {NewPath}",
              action.name, bindingIndex, newPath);
            OnBindingChanged.OnNext((action, bindingIndex, newPath));
            OnRebindingCompleted.OnNext((action, bindingIndex, true));
          }

          CleanupRebindingOperation();
        });

      // Add exclusions if specified
      if (excludeControls != null)
        foreach (string control in excludeControls)
          currentRebindingOperation.WithControlsExcluding(control);

      // Start the rebinding operation
      currentRebindingOperation.Start();
      isRebinding = true;

      OnRebindingStarted.OnNext((action, bindingIndex));

      log.ForMethod().Information("Started interactive rebinding for {ActionName} binding {BindingIndex}",
        action.name, bindingIndex);
      return true;
    }

    /// <summary>
    ///   Start rebinding for a composite binding (rebinds each part in sequence)
    /// </summary>
    private bool StartCompositeRebinding(InputAction action, int bindingIndex, string[] excludeControls)
    {
      // Find the first part of the composite
      int firstPartIndex = bindingIndex + 1;
      if (firstPartIndex >= action.bindings.Count || !action.bindings[firstPartIndex].isPartOfComposite)
      {
        log.ForMethod().Error("Invalid composite binding structure for {ActionName} binding {BindingIndex}", action.name, bindingIndex);
        return false;
      }

      // Start rebinding the first part
      return StartCompositePartRebinding(action, firstPartIndex, excludeControls);
    }

    /// <summary>
    ///   Start rebinding for a specific part of a composite binding
    /// </summary>
    private bool StartCompositePartRebinding(InputAction action, int partIndex, string[] excludeControls)
    {
      // Disable the action map to prevent interference
      action.actionMap.Disable();

      // Configure the rebinding operation for this part
      currentRebindingOperation = action.PerformInteractiveRebinding(partIndex)
        .OnCancel(_ =>
        {
          log.ForMethod().Information("Composite rebinding cancelled for {ActionName} part {PartIndex}", action.name, partIndex);
          OnRebindingCompleted.OnNext((action, partIndex, false));
          CleanupRebindingOperation();
        })
        .OnComplete(operation =>
        {
          string newPath = operation.action.bindings[partIndex].effectivePath;

          // Check for conflicts
          if (CheckBindingConflict(action, partIndex, newPath))
          {
            log.ForMethod().Warning("Binding conflict detected for {ActionName} part {PartIndex} with path {NewPath}",
              action.name, partIndex, newPath);
            // Revert the binding
            action.RemoveBindingOverride(partIndex);
            OnRebindingCompleted.OnNext((action, partIndex, false));
            CleanupRebindingOperation();
            return;
          }

          log.ForMethod().Information("Composite part rebinding completed for {ActionName} part {PartIndex} to {NewPath}",
            action.name, partIndex, newPath);
          OnBindingChanged.OnNext((action, partIndex, newPath));

          // Check if there are more parts to rebind
          int nextPartIndex = partIndex + 1;
          if (nextPartIndex < action.bindings.Count && action.bindings[nextPartIndex].isPartOfComposite)
          {
            // Continue with the next part
            StartCompositePartRebinding(action, nextPartIndex, excludeControls);
          }
          else
          {
            // All parts are done
            OnRebindingCompleted.OnNext((action, partIndex, true));
            CleanupRebindingOperation();
          }
        });

      // Add exclusions if specified
      if (excludeControls != null)
        foreach (string control in excludeControls)
          currentRebindingOperation.WithControlsExcluding(control);

      // Start the rebinding operation
      currentRebindingOperation.Start();
      isRebinding = true;

      OnRebindingStarted.OnNext((action, partIndex));

      log.ForMethod().Information("Started composite part rebinding for {ActionName} part {PartIndex}",
        action.name, partIndex);
      return true;
    }

    /// <summary>
    ///   Cancel the current rebinding operation
    /// </summary>
    public void CancelRebinding()
    {
      if (currentRebindingOperation != null)
        currentRebindingOperation.Cancel();
    }

    /// <summary>
    ///   Check if a rebinding operation is currently in progress
    /// </summary>
    public bool IsRebinding => isRebinding;

    /// <summary>
    ///   Remove a binding override for a specific action and binding index
    /// </summary>
    /// <param name="action">The action to reset</param>
    /// <param name="bindingIndex">The binding index to reset</param>
    public void RemoveBindingOverride(InputAction action, int bindingIndex)
    {
      if (action == null)
      {
        log.ForMethod().Error("Cannot remove binding override: action is null");
        return;
      }

      if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
      {
        log.ForMethod().Error("Invalid binding index {BindingIndex} for action {ActionName}", bindingIndex, action.name);
        return;
      }

      action.RemoveBindingOverride(bindingIndex);

      OnBindingChanged.OnNext((action, bindingIndex, action.bindings[bindingIndex].effectivePath));

      log.ForMethod().Information("Removed binding override for {ActionName} binding {BindingIndex}",
        action.name, bindingIndex);
    }

    /// <summary>
    ///   Reset all bindings for an action to their original values
    /// </summary>
    /// <param name="action">The action to reset</param>
    public void ResetActionBindings(InputAction action)
    {
      if (action == null)
      {
        log.ForMethod().Error("Cannot reset bindings: action is null");
        return;
      }

      // Simply remove all overrides - Unity will fall back to original bindings
      action.RemoveAllBindingOverrides();

      log.ForMethod().Information("Reset bindings for action {ActionName}", action.name);
    }

    /// <summary>
    ///   Reset all registered actions to their original bindings
    /// </summary>
    public void ResetAllBindings()
    {
      foreach (InputAction action in registeredActions.Values)
        ResetActionBindings(action);

      log.ForMethod().Information("Reset all action bindings");
    }

    /// <summary>
    ///   Check if a binding conflicts with existing bindings
    /// </summary>
    /// <param name="action">The action being rebound</param>
    /// <param name="bindingIndex">The binding index being changed</param>
    /// <param name="newBindingPath">The new binding path</param>
    /// <returns>True if there's a conflict</returns>
    public bool CheckBindingConflict(InputAction action, int bindingIndex, string newBindingPath)
    {
      if (string.IsNullOrEmpty(newBindingPath))
        return false;

      foreach (InputAction registeredAction in registeredActions.Values)
      {
        if (registeredAction == action) continue;

        for (int i = 0; i < registeredAction.bindings.Count; i++)
        {
          InputBinding binding = registeredAction.bindings[i];
          if (binding.effectivePath == newBindingPath)
          {
            string conflictMessage = $"Binding conflict: {newBindingPath} is already used by {registeredAction.name}";
            OnBindingConflictDetected.OnNext(conflictMessage);
            OnBindingConflictFound.OnNext((newBindingPath, registeredAction, i));
            return true;
          }
        }
      }

      return false;
    }

    /// <summary>
    ///   Resolve a binding conflict by removing the conflicting binding from another action
    /// </summary>
    /// <param name="conflictingAction">The action that has the conflicting binding</param>
    /// <param name="conflictingBindingIndex">The binding index that conflicts</param>
    /// <returns>True if the conflict was resolved</returns>
    public bool ResolveBindingConflict(InputAction conflictingAction, int conflictingBindingIndex)
    {
      if (conflictingAction == null || conflictingBindingIndex < 0 || conflictingBindingIndex >= conflictingAction.bindings.Count)
      {
        log.ForMethod().Error("Invalid parameters for conflict resolution");
        return false;
      }

      string bindingPath = conflictingAction.bindings[conflictingBindingIndex].effectivePath;
      conflictingAction.RemoveBindingOverride(conflictingBindingIndex);

      log.ForMethod().Information("Resolved binding conflict by removing {BindingPath} from {ActionName} binding {BindingIndex}",
        bindingPath, conflictingAction.name, conflictingBindingIndex);

      OnBindingChanged.OnNext((conflictingAction, conflictingBindingIndex, conflictingAction.bindings[conflictingBindingIndex].effectivePath));
      return true;
    }

    /// <summary>
    ///   Get all actions that have a specific binding path
    /// </summary>
    /// <param name="bindingPath">The binding path to search for</param>
    /// <returns>List of (action, bindingIndex) tuples that use this path</returns>
    public List<(InputAction action, int bindingIndex)> GetActionsUsingBindingPath(string bindingPath)
    {
      List<(InputAction action, int bindingIndex)> result = new();

      if (string.IsNullOrEmpty(bindingPath))
        return result;

      foreach (InputAction registeredAction in registeredActions.Values)
        for (int i = 0; i < registeredAction.bindings.Count; i++)
        {
          InputBinding binding = registeredAction.bindings[i];
          if (binding.effectivePath == bindingPath)
            result.Add((registeredAction, i));
        }

      return result;
    }

    /// <summary>
    ///   Get all registered actions
    /// </summary>
    public IReadOnlyCollection<InputAction> RegisteredActions => registeredActions.Values;

    /// <summary>
    ///   Clean up the current rebinding operation
    /// </summary>
    private void CleanupRebindingOperation()
    {
      currentRebindingOperation?.Dispose();
      currentRebindingOperation = null;
      isRebinding = false;

      // Re-enable all action maps
      foreach (InputAction action in registeredActions.Values)
        action.actionMap.Enable();
    }

    private string GetActionKey(InputAction action)
    {
      return $"{action.actionMap?.name}_{action.name}";
    }

    public void Dispose()
    {
      CleanupRebindingOperation();
      OnBindingChanged?.Dispose();
      OnBindingConflictDetected?.Dispose();
      OnRebindingStarted?.Dispose();
      OnRebindingCompleted?.Dispose();
      OnBindingConflictFound?.Dispose();
    }
  }

  /// <summary>
  ///   Enum for specifying allowed control types during rebinding
  /// </summary>
  [Flags]
  public enum EInputControlType
  {
    Keyboard = 1 << 0,
    Mouse = 1 << 1,
    Gamepad = 1 << 2,
    All = Keyboard | Mouse | Gamepad
  }
}