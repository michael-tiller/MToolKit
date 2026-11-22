using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MToolKit.Runtime.Input.Interfaces;
using MToolKit.Runtime.Input.Prompts;
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
  public class InputRebinderService : IDisposable, IInputRebinderService, IScopedInputRebinderService, IInputBindingChangeSource
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputRebinderService>().ForFeature("Core.Services.InputRebinderService"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public Subject<(InputAction action, int bindingIndex, string newPath)> OnBindingChanged { get; } = new();
    public Observable<(InputAction action, int bindingIndex, string newPath)> BindingChanges => OnBindingChanged;
    public Subject<string> OnBindingConflictDetected { get; } = new();
    public Subject<(InputAction action, int bindingIndex)> OnRebindingStarted { get; } = new();
    public Subject<(InputAction action, int bindingIndex, bool completed)> OnRebindingCompleted { get; } = new();
    public Subject<(string conflictingPath, InputAction conflictingAction, int conflictingBindingIndex)> OnBindingConflictFound { get; } = new();

    [ShowInInspector]
    [ReadOnly]
    private readonly Dictionary<InputAction, ActionRegistration> registeredActions = new(ActionReferenceComparer.Instance);

    [ShowInInspector]
    [ReadOnly]
    private InputActionRebindingExtensions.RebindingOperation currentRebindingOperation;

    [ShowInInspector]
    [ReadOnly]
    private bool isRebinding;

    private List<(InputAction action, bool enabled)> capturedActionStates;
    private bool disposed;

    [ShowInInspector]
    [ReadOnly]
    private string hash => GetHashCode().ToString();

    /// <summary>
    ///   Register an input action for rebinding management
    /// </summary>
    /// <param name="action">The input action to register</param>
    public void RegisterAction(InputAction action)
    {
      ThrowIfDisposed();
      if (action == null)
      {
        log.ForMethod().Error("Cannot register null input action");
        return;
      }

      GetOrCreateRegistration(action).Permanent = true;
    }

    /// <summary>Registers this execution instance until the returned owner releases it.</summary>
    public IDisposable RegisterScopedAction(InputAction action)
    {
      ThrowIfDisposed();
      if (action == null) throw new ArgumentNullException(nameof(action));
      var registration = GetOrCreateRegistration(action);
      registration.Scopes++;
      return new ScopedRegistration(this, action, registration);
    }

    private ActionRegistration GetOrCreateRegistration(InputAction action)
    {
      if (registeredActions.TryGetValue(action, out var registration)) return registration;
      registration = new ActionRegistration();
      registeredActions.Add(action, registration);
      log.ForMethod().Verbose("Registered action {ActionName} for rebinding", action.name);
      return registration;
    }

    private void ReleaseScopedAction(InputAction action, ActionRegistration registration)
    {
      if (disposed || !registeredActions.TryGetValue(action, out var current)
        || !ReferenceEquals(current, registration)) return;
      registration.Scopes--;
      if (registration.Permanent || registration.Scopes > 0) return;

      var operation = currentRebindingOperation;
      try
      {
        // Cancel before the host can destroy its input asset; callbacks still see a valid action.
        if (operation != null && ReferenceEquals(operation.action, action)) operation.Cancel();
      }
      finally
      {
        if (operation != null && ReferenceEquals(currentRebindingOperation, operation)
          && ReferenceEquals(operation.action, action)) CleanupRebindingOperation();
        // A cancellation callback may have acquired a new scope or made this registration permanent.
        if (registeredActions.TryGetValue(action, out current) && ReferenceEquals(current, registration)
          && !registration.Permanent && registration.Scopes == 0) registeredActions.Remove(action);
      }
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
      ThrowIfDisposed();
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
      if (!registeredActions.ContainsKey(action)) RegisterAction(action);

      // Cancel any existing rebinding operation
      currentRebindingOperation?.Cancel();
      currentRebindingOperation?.Dispose();

      try
      {
        InputBinding binding = action.bindings[bindingIndex];
        if (binding.isComposite)
          return StartCompositeRebinding(action, bindingIndex, excludeControls);
        return StartSingleBindingRebinding(action, bindingIndex, excludeControls);
      }
      catch
      {
        CleanupRebindingOperation();
        throw;
      }
    }

    /// <summary>
    ///   Start rebinding for a single (non-composite) binding
    /// </summary>
    private bool StartSingleBindingRebinding(InputAction action, int bindingIndex, string[] excludeControls)
    {
      CaptureAndDisableActions(action);
      InputBinding previousBinding = action.bindings[bindingIndex];

      // Configure the rebinding operation
      currentRebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
        .OnCancel(operation =>
        {
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;
          log.ForMethod().Information("Rebinding cancelled for {ActionName} binding {BindingIndex}", action.name, bindingIndex);
          CleanupRebindingOperation();
          OnRebindingCompleted.OnNext((action, bindingIndex, false));
        })
        .OnComplete(operation =>
        {
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;
          string newPath = operation.action.bindings[bindingIndex].effectivePath;

          // Check for conflicts
          bool conflict = CheckBindingConflict(action, bindingIndex, newPath);
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;
          if (conflict)
          {
            log.ForMethod().Warning("Binding conflict detected for {ActionName} binding {BindingIndex} with path {NewPath}",
              action.name, bindingIndex, newPath);
            action.ApplyBindingOverride(bindingIndex, previousBinding);
            CleanupRebindingOperation();
            OnRebindingCompleted.OnNext((action, bindingIndex, false));
          }
          else
          {
            log.ForMethod().Information("Rebinding completed for {ActionName} binding {BindingIndex} to {NewPath}",
              action.name, bindingIndex, newPath);
            OnBindingChanged.OnNext((action, bindingIndex, newPath));
            if (!ReferenceEquals(currentRebindingOperation, operation)) return;
            CleanupRebindingOperation();
            OnRebindingCompleted.OnNext((action, bindingIndex, true));
          }
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
      CaptureAndDisableActions(action);
      InputBinding previousBinding = action.bindings[partIndex];

      // Configure the rebinding operation for this part
      currentRebindingOperation = action.PerformInteractiveRebinding(partIndex)
        .OnCancel(operation =>
        {
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;
          log.ForMethod().Information("Composite rebinding cancelled for {ActionName} part {PartIndex}", action.name, partIndex);
          CleanupRebindingOperation();
          OnRebindingCompleted.OnNext((action, partIndex, false));
        })
        .OnComplete(operation =>
        {
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;
          string newPath = operation.action.bindings[partIndex].effectivePath;

          // Check for conflicts
          bool conflict = CheckBindingConflict(action, partIndex, newPath);
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;
          if (conflict)
          {
            log.ForMethod().Warning("Binding conflict detected for {ActionName} part {PartIndex} with path {NewPath}",
              action.name, partIndex, newPath);
            action.ApplyBindingOverride(partIndex, previousBinding);
            CleanupRebindingOperation();
            OnRebindingCompleted.OnNext((action, partIndex, false));
            return;
          }

          log.ForMethod().Information("Composite part rebinding completed for {ActionName} part {PartIndex} to {NewPath}",
            action.name, partIndex, newPath);
          OnBindingChanged.OnNext((action, partIndex, newPath));
          if (!ReferenceEquals(currentRebindingOperation, operation)) return;

          // Check if there are more parts to rebind
          int nextPartIndex = partIndex + 1;
          if (nextPartIndex < action.bindings.Count && action.bindings[nextPartIndex].isPartOfComposite)
          {
            currentRebindingOperation.Dispose();
            currentRebindingOperation = null;
            StartCompositePartRebinding(action, nextPartIndex, excludeControls);
          }
          else
          {
            // All parts are done
            CleanupRebindingOperation();
            OnRebindingCompleted.OnNext((action, partIndex, true));
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
      for (int index = 0; index < action.bindings.Count; index++)
        OnBindingChanged.OnNext((action, index, action.bindings[index].effectivePath));

      log.ForMethod().Information("Reset bindings for action {ActionName}", action.name);
    }

    /// <summary>
    ///   Reset all registered actions to their original bindings
    /// </summary>
    public void ResetAllBindings()
    {
      var actions = new List<InputAction>(registeredActions.Keys);
      foreach (InputAction action in actions)
        if (registeredActions.ContainsKey(action)) ResetActionBindings(action);

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

      foreach (InputAction registeredAction in registeredActions.Keys)
      {
        if (registeredAction == action || registeredAction.actionMap != action.actionMap) continue;

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

      foreach (InputAction registeredAction in registeredActions.Keys)
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
    public IReadOnlyCollection<InputAction> RegisteredActions => registeredActions.Keys;

    /// <summary>
    ///   Clean up the current rebinding operation
    /// </summary>
    private void CleanupRebindingOperation()
    {
      var operation = currentRebindingOperation;
      currentRebindingOperation = null;
      isRebinding = false;
      operation?.Dispose();

      var states = capturedActionStates;
      capturedActionStates = null;
      if (states == null) return;
      foreach (var state in states)
        if (state.enabled) state.action.Enable();
        else state.action.Disable();
    }

    private void CaptureAndDisableActions(InputAction action)
    {
      if (capturedActionStates == null)
      {
        capturedActionStates = new List<(InputAction action, bool enabled)>();
        if (action.actionMap != null)
          foreach (InputAction mapAction in action.actionMap.actions)
            capturedActionStates.Add((mapAction, mapAction.enabled));
        else
          capturedActionStates.Add((action, action.enabled));
      }

      if (action.actionMap != null) action.actionMap.Disable();
      else action.Disable();
    }

    private void ThrowIfDisposed()
    {
      if (disposed) throw new ObjectDisposedException(nameof(InputRebinderService));
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      try { CleanupRebindingOperation(); }
      finally { registeredActions.Clear(); }
      OnBindingChanged?.Dispose();
      OnBindingConflictDetected?.Dispose();
      OnRebindingStarted?.Dispose();
      OnRebindingCompleted?.Dispose();
      OnBindingConflictFound?.Dispose();
    }

    private sealed class ActionRegistration
    {
      public bool Permanent;
      public int Scopes;
    }

    private sealed class ScopedRegistration : IDisposable
    {
      private InputRebinderService owner;
      private readonly InputAction action;
      private readonly ActionRegistration registration;
      public ScopedRegistration(InputRebinderService owner, InputAction action, ActionRegistration registration)
      { this.owner = owner; this.action = action; this.registration = registration; }
      public void Dispose()
      {
        var current = owner;
        if (current == null) return;
        owner = null;
        current.ReleaseScopedAction(action, registration);
      }
    }

    private sealed class ActionReferenceComparer : IEqualityComparer<InputAction>
    {
      public static readonly ActionReferenceComparer Instance = new();
      public bool Equals(InputAction first, InputAction second) => ReferenceEquals(first, second);
      public int GetHashCode(InputAction action) => RuntimeHelpers.GetHashCode(action);
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
