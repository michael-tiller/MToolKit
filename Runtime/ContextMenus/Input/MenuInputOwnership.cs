using System;
using System.Collections.Generic;
using Serilog;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace MToolKit.ContextMenus.Input
{
  /// <summary>Input leases preserve individual enabled states across replacement sessions and independent menu hosts.</summary>
  public sealed class MenuInputOwnership : IDisposable
  {
    /// <summary>Domain-scoped arbiter for menus sharing gameplay actions. Dispose leases, not this shared coordinator.</summary>
    public static MenuInputOwnership Shared { get; } = new MenuInputOwnership();

    private readonly Dictionary<InputAction, Entry> entries = new();
    private readonly HashSet<Entry> pending = new();
    private readonly List<Entry> restorationBuffer = new();
    private bool subscribed, updating, disposed;
    private int dispatchDepth;

    /// <summary>
    /// Suspends declared gameplay actions and enables menu actions without toggling their entire maps.
    /// Acquire the incoming menu's lease before disposing the outgoing menu's lease during replacement.
    /// </summary>
    public IDisposable Acquire(IEnumerable<InputAction> gameplayActions, IEnumerable<InputAction> menuActions)
    {
      if (disposed) throw new ObjectDisposedException(nameof(MenuInputOwnership));
      var gameplay = Distinct(gameplayActions);
      var menu = Distinct(menuActions);
      var uses = new List<Use>(gameplay.Count + menu.Count);
      foreach (var action in gameplay)
        uses.Add(new Use(GetOrCreate(action), true, menu.Remove(action)));
      foreach (var action in menu) uses.Add(new Use(GetOrCreate(action), false, true));

      // Capture all original states before the first Disable can synchronously cancel another action.
      foreach (var use in uses)
      {
        if (use.Gameplay) use.Entry.Gameplay++;
        if (use.Menu) use.Entry.Menu++;
        pending.Remove(use.Entry);
        RemoveReleaseGuards(use.Entry);
      }
      if (uses.Count > 0) Subscribe();
      var lease = new Lease(this, uses);
      try
      {
        foreach (var use in uses) ApplyActive(use.Entry);
        return lease;
      }
      catch
      {
        lease.Dispose();
        throw;
      }
    }

    /// <summary>Quarantines restoration of a navigation action until its old gesture is neutral.</summary>
    public void DeferRestorationUntilNeutral(InputAction action)
    {
      if (action == null) return;
      if (disposed) throw new ObjectDisposedException(nameof(MenuInputOwnership));
      var entry = GetOrCreate(action);
      entry.RequireNeutral = true;
      // A shared semantic menu must retain the action until its final lease ends.
      if (entry.Gameplay > 0 || entry.Menu > 0) return;
      using var handoff = Acquire(new[] { action }, null);
    }

    internal DispatchScope EnterDispatch()
    {
      dispatchDepth++;
      return new DispatchScope(this);
    }

    private static HashSet<InputAction> Distinct(IEnumerable<InputAction> actions)
    {
      var result = new HashSet<InputAction>();
      if (actions != null)
        foreach (var action in actions)
          if (action != null) result.Add(action);
      return result;
    }

    private Entry GetOrCreate(InputAction action)
    {
      if (!entries.TryGetValue(action, out var entry))
      {
        entry = new Entry(this, action, action.enabled);
        entries.Add(action, entry);
      }
      return entry;
    }

    private void ApplyActive(Entry entry)
    {
      if (entry.Menu > 0)
      {
        if (!entry.Action.enabled) entry.Action.Enable();
      }
      else if (entry.Gameplay > 0 && entry.Action.enabled) entry.Action.Disable();
    }

    private void Release(IReadOnlyList<Use> uses)
    {
      if (disposed) return;
      foreach (var use in uses)
      {
        if (use.Gameplay) use.Entry.Gameplay--;
        if (use.Menu) use.Entry.Menu--;
      }
      foreach (var use in uses)
      {
        var entry = use.Entry;
        if (entry.Gameplay > 0 || entry.Menu > 0) ApplyActive(entry);
        else Retire(entry);
      }
      DetachWhenIdle();
    }

    private void Retire(Entry entry, bool checkHeldControls = true)
    {
      // The resolved Input System binding state caches initial-state-check flags. Temporarily changing
      // InputAction.wantsInitialStateCheck cannot safely prevent replay. Keep only actually held buttons
      // suspended until their state-change monitors observe release; do not poll all controls per frame.
      if (checkHeldControls && QuarantineHeldControls(entry)) return;
      // Enabling a gameplay action inside a menu callback can expose the event still being dispatched.
      // Disabled-at-open actions can be retired immediately because their restoration cannot execute input.
      if (entry.WasEnabled && (updating || dispatchDepth > 0)) pending.Add(entry);
      else Restore(entry);
    }

    private void Restore(Entry entry)
    {
      if (entry.Gameplay > 0 || entry.Menu > 0) return;
      pending.Remove(entry);
      try
      {
        if (entry.WasEnabled && !entry.Action.enabled)
          entry.Action.Enable();
        else if (!entry.WasEnabled && entry.Action.enabled) entry.Action.Disable();
      }
      catch (ObjectDisposedException)
      {
        // The consuming input service may already have retired its runtime clone during scene shutdown.
        Log.Debug("Menu input action {Action} was disposed before its ownership lease retired", entry.Action.name);
      }
      finally
      {
        if (entry.Gameplay == 0 && entry.Menu == 0)
        {
          RemoveReleaseGuards(entry);
          entries.Remove(entry.Action);
        }
      }
    }

    private bool QuarantineHeldControls(Entry entry)
    {
      if (!entry.WasEnabled) return false;
      if (entry.RequireNeutral)
      {
        // Disabling a Value navigation action also cancels its cached native UI move.
        if (entry.Action.enabled) entry.Action.Disable();
      }
      else if (entry.Action.enabled || entry.Action.type != InputActionType.Button) return false;
      if (entry.HeldControls.Count > 0) return true;
      foreach (var control in entry.Action.controls)
        if ((entry.RequireNeutral || control is ButtonControl) && IsHeld(control))
        {
          entry.HeldControls.Add(control);
          InputState.AddChangeMonitor(control, entry);
        }
      return entry.HeldControls.Count > 0;
    }

    private static bool IsHeld(InputControl control)
    {
      if (!control.device.added || !control.device.enabled) return false;
      if (control is ButtonControl button)
        return button.EvaluateMagnitude() >= button.pressPointOrDefault * InputSystem.settings.buttonReleaseThreshold;
      // Vector2Control.ReadValue applies its configured processors, including stick deadzones.
      if (control is Vector2Control vector) return vector.ReadValue().sqrMagnitude > 0;
      return control.EvaluateMagnitude() > InputSystem.settings.defaultDeadzoneMin;
    }

    private void ReleaseGuardChanged(Entry entry)
    {
      if (entry.Gameplay > 0 || entry.Menu > 0) { RemoveReleaseGuards(entry); return; }
      foreach (var control in entry.HeldControls)
        if (IsHeld(control)) return;
      RemoveReleaseGuards(entry);
      Retire(entry, false);
      DetachWhenIdle();
    }

    private static void RemoveReleaseGuards(Entry entry)
    {
      foreach (var control in entry.HeldControls)
        if (control.device.added) InputState.RemoveChangeMonitor(control, entry);
      entry.HeldControls.Clear();
    }

    private void Subscribe()
    {
      if (subscribed) return;
      subscribed = true;
      InputSystem.onBeforeUpdate += BeforeUpdate;
      InputSystem.onAfterUpdate += AfterUpdate;
      InputSystem.onDeviceChange += DeviceChanged;
    }

    private void BeforeUpdate() => updating = true;

    private void DeviceChanged(InputDevice device, InputDeviceChange change)
    {
      if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected
        && change != InputDeviceChange.Disabled) return;
      // Device changes are infrequent lifecycle events. A snapshot permits a restoration to alter the registry.
      foreach (var entry in new List<Entry>(entries.Values))
        if (entry.HeldControls.Count > 0) ReleaseGuardChanged(entry);
    }

    private void AfterUpdate()
    {
      updating = false;
      if (pending.Count > 0)
      {
        restorationBuffer.Clear();
        restorationBuffer.AddRange(pending);
        foreach (var entry in restorationBuffer)
          if (entry.Gameplay == 0 && entry.Menu == 0) Restore(entry);
        restorationBuffer.Clear();
      }
      DetachWhenIdle();
    }

    private void DetachWhenIdle()
    {
      if (!subscribed || entries.Count > 0) return;
      InputSystem.onBeforeUpdate -= BeforeUpdate;
      InputSystem.onAfterUpdate -= AfterUpdate;
      InputSystem.onDeviceChange -= DeviceChanged;
      subscribed = false;
      updating = false;
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      restorationBuffer.Clear();
      restorationBuffer.AddRange(entries.Values);
      foreach (var entry in restorationBuffer)
      {
        entry.Gameplay = 0; entry.Menu = 0;
        Retire(entry);
      }
      restorationBuffer.Clear();
      DetachWhenIdle();
    }

    internal readonly struct DispatchScope : IDisposable
    {
      private readonly MenuInputOwnership owner;
      internal DispatchScope(MenuInputOwnership owner) => this.owner = owner;
      public void Dispose() => owner.dispatchDepth--;
    }

    private sealed class Entry : IInputStateChangeMonitor
    {
      private readonly MenuInputOwnership owner;
      public readonly InputAction Action;
      public readonly bool WasEnabled;
      public readonly List<InputControl> HeldControls = new();
      public bool RequireNeutral;
      public int Gameplay, Menu;
      public Entry(MenuInputOwnership owner, InputAction action, bool wasEnabled)
      { this.owner = owner; Action = action; WasEnabled = wasEnabled; }
      public void NotifyControlStateChanged(InputControl control, double time, InputEventPtr eventPtr, long monitorIndex)
        => owner.ReleaseGuardChanged(this);
      public void NotifyTimerExpired(InputControl control, double time, long monitorIndex, int timerIndex) { }
    }

    private readonly struct Use
    {
      public readonly Entry Entry;
      public readonly bool Gameplay, Menu;
      public Use(Entry entry, bool gameplay, bool menu) { Entry = entry; Gameplay = gameplay; Menu = menu; }
    }

    private sealed class Lease : IDisposable
    {
      private MenuInputOwnership owner;
      private readonly IReadOnlyList<Use> uses;
      public Lease(MenuInputOwnership owner, IReadOnlyList<Use> uses) { this.owner = owner; this.uses = uses; }
      public void Dispose()
      {
        var previous = owner; owner = null;
        previous?.Release(uses);
      }
    }
  }
}
