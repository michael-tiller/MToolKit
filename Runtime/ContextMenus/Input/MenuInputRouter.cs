using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using MToolKit.Runtime.Input.Interfaces;
using MToolKit.Runtime.Input.Prompts;
using Serilog;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MToolKit.ContextMenus.Input
{
  /// <summary>Routes rebound Input System callbacks to presentation-independent menu operations.</summary>
  public sealed class MenuInputRouter : IDisposable
  {
    private readonly IMenuInputTarget target;
    private readonly MenuInputOwnership ownership;
    private readonly List<Subscription> subscriptions = new();
    private readonly List<InputAction> menuActions = new();
    private readonly List<IDisposable> registrations = new();
    private readonly HashSet<InputAction> uniqueActions = new();
    private readonly InputAction point, hold;
    private IDisposable lease;
    private bool disposed, holding, lastNavigationWasAnalog;

    public MenuInputRouter(IMenuInputTarget target, MenuInputBindings bindings, IInputActionSource actionSource = null,
      MenuInputOwnership ownership = null, IEnumerable<InputAction> gameplayActions = null,
      IInputRebinderService rebinder = null)
    {
      this.target = target ?? throw new ArgumentNullException(nameof(target));
      if (bindings == null) throw new ArgumentNullException(nameof(bindings));
      if (actionSource != null && actionSource.RuntimeActions == null)
        throw new InvalidOperationException("Menu input requires the initialized IInputActionSource runtime asset; authored references cannot execute before it is ready.");
      this.ownership = ownership ?? MenuInputOwnership.Shared;

      InputAction Resolve(InputActionReference reference)
      {
        var authored = reference != null ? reference.action : null;
        var runtime = actionSource != null ? actionSource.ResolveRuntimeAction(authored) : authored;
        if (runtime != null)
        {
          if (!uniqueActions.Add(runtime))
            throw new ArgumentException($"Menu input action '{runtime.actionMap?.name}/{runtime.name}' is assigned to more than one operation. Use distinct actions for independent operations.", nameof(bindings));
          menuActions.Add(runtime);
        }
        return runtime;
      }

      // Resolve and validate every identity before subscribing, registering or enabling any action.
      var navigate = Resolve(bindings.Navigate);
      point = Resolve(bindings.Point);
      var click = Resolve(bindings.Click);
      var submit = Resolve(bindings.Submit);
      var back = Resolve(bindings.Back);
      var cancel = Resolve(bindings.Cancel);
      var next = Resolve(bindings.PageForward);
      var previous = Resolve(bindings.PageBackward);
      var ring = Resolve(bindings.SwitchRing);
      hold = Resolve(bindings.Hold);
      var secondary = new InputAction[bindings.Secondary.Count];
      for (int i = 0; i < secondary.Length; i++) secondary[i] = Resolve(bindings.Secondary[i]);

      var suspended = new List<InputAction>();
      if (gameplayActions != null)
        foreach (var action in gameplayActions)
        {
          var runtime = actionSource != null ? actionSource.ResolveRuntimeAction(action) : action;
          if (runtime != null) suspended.Add(runtime);
        }

      try
      {
        if (!bindings.UseNativeUguiNavigation)
        {
          Subscribe(navigate, OnNavigate, OnNavigateCanceled);
          SubscribeButton(submit, target.Confirm);
          SubscribeButton(back, target.Back);
          SubscribeButton(cancel, target.Cancel);
        }
        Subscribe(point, context => target.Point(context.ReadValue<Vector2>()));
        if (bindings.RoutePointerClick)
          Subscribe(click, context => { if (IsActuated(context)) OnPointerClick(context); });
        SubscribeButton(next, () => target.Page(1));
        SubscribeButton(previous, () => target.Page(-1));
        SubscribeButton(ring, target.SwitchRing);
        Subscribe(hold, OnHoldPerformed, OnHoldCanceled);
        for (int i = 0; i < secondary.Length; i++)
        {
          int index = i;
          SubscribeButton(secondary[i], () => target.InvokeSecondary(index));
        }
        // Scoped registration preserves permanent application owners while releasing host-owned clones.
        if (rebinder is IScopedInputRebinderService scoped)
          foreach (var action in menuActions) registrations.Add(scoped.RegisterScopedAction(action));
        else if (rebinder != null)
          foreach (var action in menuActions) rebinder.RegisterAction(action);
        lease = this.ownership.Acquire(suspended, menuActions);
      }
      catch
      {
        try { Dispose(); }
        catch (Exception exception) { Log.Error(exception, "Menu input cleanup failed after initialization failed"); }
        throw;
      }
    }

    /// <summary>
    /// Adopts the activation gesture when a host created this router from the Hold action's earlier performed callback.
    /// The host calls this once after opening; no device polling or synthetic button press is required.
    /// </summary>
    public void AdoptHeldActivation()
    {
      if (disposed) throw new ObjectDisposedException(nameof(MenuInputRouter));
      if (hold == null) throw new InvalidOperationException("A held menu activation requires a configured Hold action.");
      if (holding) return;
      holding = true;
      using var dispatch = ownership.EnterDispatch();
      target.BeginHold();
    }

    private void Subscribe(InputAction action, Action<InputAction.CallbackContext> performed,
      Action<InputAction.CallbackContext> canceled = null)
    {
      if (action == null) return;
      Action<InputAction.CallbackContext> Wrap(Action<InputAction.CallbackContext> callback)
      {
        if (callback == null) return null;
        return context =>
        {
          if (disposed) return;
          using var dispatch = ownership.EnterDispatch();
          try { callback(context); }
          catch (Exception exception)
          {
            Log.Error(exception, "Menu input callback failed for {Action} in {Phase}", action.name, context.phase);
          }
        };
      }
      var subscription = new Subscription(action, Wrap(performed), Wrap(canceled));
      subscriptions.Add(subscription);
      subscription.Attach();
    }

    private void SubscribeButton(InputAction action, Action operation)
      => Subscribe(action, context => { if (IsActuated(context)) operation(); });

    private static bool IsActuated(InputAction.CallbackContext context)
    {
      float value = context.ReadValue<float>();
      // UI pointer actions are commonly PassThrough and perform again on release.
      return value > 0 && (context.action.type != InputActionType.PassThrough
        || value >= InputSystem.settings.defaultButtonPressPoint);
    }

    private static bool IsAnalog(InputControl control)
    {
      for (var current = control; current != null && !(current is InputDevice); current = current.parent)
      {
        if (current is DpadControl) return false;
        if (current is StickControl) return true;
      }
      return false;
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
      lastNavigationWasAnalog = IsAnalog(context.control);
      var direction = context.ReadValue<Vector2>();
      if (lastNavigationWasAnalog || direction != Vector2.zero) target.Navigate(direction, lastNavigationWasAnalog);
    }

    private void OnNavigateCanceled(InputAction.CallbackContext context)
    {
      if (lastNavigationWasAnalog) target.Navigate(Vector2.zero, true);
      lastNavigationWasAnalog = false;
    }

    private void OnPointerClick(InputAction.CallbackContext context)
    {
      UpdatePointerPosition(context);
      if (disposed) return;
      if (target is IMenuPointerConfirmationTarget pointer) pointer.ConfirmPointer();
      else target.Confirm();
    }

    private void UpdatePointerPosition(InputAction.CallbackContext context)
    {
      // Input System action order is not a pointer-position ordering guarantee within one native event.
      if (point != null)
      {
        var position = point.ReadValue<Vector2>();
        foreach (var control in point.controls)
          if (control.device == context.control?.device && control is Vector2Control vector)
          { position = vector.ReadValue(); break; }
        target.Point(position);
      }
    }

    private void OnHoldPerformed(InputAction.CallbackContext context)
    {
      if (holding || !IsActuated(context)) return;
      holding = true;
      target.BeginHold();
    }

    private void OnHoldCanceled(InputAction.CallbackContext context)
    {
      if (!holding) return;
      holding = false;
      // Disabling an action or losing a device also cancels it. A still-actuated control is not a release.
      if (context.control == null || !context.control.device.added
        || context.control.EvaluateMagnitude() >= InputSystem.settings.defaultButtonPressPoint)
        target.Cancel();
      else if (context.control.device is Pointer && target is IMenuPointerConfirmationTarget pointer)
      {
        UpdatePointerPosition(context);
        if (!disposed) pointer.EndPointerHold();
      }
      else target.EndHold();
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      holding = false;
      // Unsubscribe before lease restoration can synchronously cancel a held action.
      foreach (var subscription in subscriptions) subscription.Detach();
      subscriptions.Clear();
      Exception failure = null;
      // A final scoped release cancels interactive rebinding and restores its captured state.
      // Restore menu/gameplay ownership afterwards so cancellation cannot re-enable a retired menu action.
      for (int i = registrations.Count - 1; i >= 0; i--)
      {
        try { registrations[i]?.Dispose(); }
        catch (Exception exception)
        {
          failure ??= exception;
          Log.Error(exception, "Menu input scoped registration cleanup failed");
        }
      }
      registrations.Clear();
      var inputLease = lease; lease = null;
      try { inputLease?.Dispose(); }
      catch (Exception exception)
      {
        failure ??= exception;
        Log.Error(exception, "Menu input ownership cleanup failed");
      }
      if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private readonly struct Subscription
    {
      private readonly InputAction action;
      private readonly Action<InputAction.CallbackContext> performed, canceled;
      public Subscription(InputAction action, Action<InputAction.CallbackContext> performed,
        Action<InputAction.CallbackContext> canceled)
      { this.action = action; this.performed = performed; this.canceled = canceled; }
      public void Attach()
      {
        if (performed != null) action.performed += performed;
        if (canceled != null) action.canceled += canceled;
      }
      public void Detach()
      {
        if (performed != null) action.performed -= performed;
        if (canceled != null) action.canceled -= canceled;
      }
    }
  }
}
