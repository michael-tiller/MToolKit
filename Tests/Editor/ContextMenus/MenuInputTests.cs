using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.ContextMenus.Input;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuInputTests
  {
    [Test]
    public void LeaseRestoresOnlyPreviouslyEnabledActionsWithoutEnablingTheirWholeMap()
    {
      using var fixture = new Fixture();
      var attack = fixture.Gameplay.AddAction("Attack", binding: "<Keyboard>/space");
      var move = fixture.Gameplay.AddAction("Move", binding: "<Keyboard>/w");
      attack.Enable();
      using var owner = new MenuInputOwnership();
      var lease = owner.Acquire(new[] { attack, move }, Array.Empty<InputAction>());
      Assert.That(attack.enabled, Is.False);
      Assert.That(move.enabled, Is.False);
      lease.Dispose();
      Assert.That(attack.enabled, Is.True);
      Assert.That(move.enabled, Is.False, "Closing a menu must not enable unrelated, previously disabled gameplay.");
      lease.Dispose();
      Assert.That(attack.enabled, Is.True);
    }

    [Test]
    public void ReplacementLeasesRetainTheOriginalSnapshotUntilTheLastMenuCloses()
    {
      using var fixture = new Fixture();
      var attack = fixture.Gameplay.AddAction("Attack");
      var submit = fixture.Menu.AddAction("Submit");
      attack.Enable();
      using var owner = new MenuInputOwnership();
      var first = owner.Acquire(new[] { attack, submit }, new[] { submit });
      var second = owner.Acquire(new[] { attack }, new[] { submit });
      first.Dispose();
      Assert.That(attack.enabled, Is.False);
      Assert.That(submit.enabled, Is.True, "The outgoing menu must not disable the incoming menu's shared action.");
      second.Dispose();
      Assert.That(attack.enabled, Is.True);
      Assert.That(submit.enabled, Is.False);
    }

    [Test]
    public void DefaultRoutersShareGameplayOwnershipAcrossIndependentMenuHosts()
    {
      using var fixture = new Fixture();
      var attack = fixture.Gameplay.AddAction("Attack", InputActionType.Button, "<Keyboard>/space");
      var firstSubmit = fixture.Menu.AddAction("FirstSubmit", InputActionType.Button, "<Keyboard>/enter");
      var secondSubmit = fixture.Menu.AddAction("SecondSubmit", InputActionType.Button, "<Keyboard>/q");
      attack.Enable();
      var firstBindings = new MenuInputBindings(); firstBindings.Configure(submit: fixture.Reference(firstSubmit));
      var secondBindings = new MenuInputBindings(); secondBindings.Configure(submit: fixture.Reference(secondSubmit));
      using var first = new MenuInputRouter(new Target(), firstBindings, gameplayActions: new[] { attack });
      using var second = new MenuInputRouter(new Target(), secondBindings, gameplayActions: new[] { attack });
      first.Dispose();
      Assert.That(attack.enabled, Is.False, "Closing one host cannot restore gameplay while another menu host still owns it.");
      Assert.That(secondSubmit.enabled, Is.True);
      second.Dispose();
      Assert.That(attack.enabled, Is.True);
      Assert.That(firstSubmit.enabled, Is.False); Assert.That(secondSubmit.enabled, Is.False);
    }

    [Test]
    public void RuntimeActionIdentityAndEffectiveRebindingDriveKeyboardConfirmation()
    {
      using var fixture = new Fixture();
      var authored = fixture.Menu.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
      var runtime = Object.Instantiate(fixture.Asset);
      runtime.devices = fixture.Asset.devices;
      try
      {
        var execution = runtime.FindAction(authored.id);
        execution.ApplyBindingOverride(0, "<Keyboard>/q");
        var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(authored));
        var target = new Target();
        using var router = new MenuInputRouter(target, bindings, new Source(fixture.Asset, runtime));
        fixture.Press(Key.Enter);
        Assert.That(target.Confirms, Is.Zero, "An authored reference must not enable or execute the original action.");
        fixture.Press(Key.Q);
        Assert.That(target.Confirms, Is.EqualTo(1));
        Assert.That(authored.enabled, Is.False);
      }
      finally { Object.DestroyImmediate(runtime); }
    }

    [Test]
    public void MissingRuntimeCloneFailsBeforeAnAuthoredActionCanBeEnabled()
    {
      using var fixture = new Fixture();
      var submit = fixture.Menu.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(submit));
      Assert.Throws<InvalidOperationException>(() => new MenuInputRouter(new Target(), bindings, new Source(fixture.Asset, null)));
      Assert.That(submit.enabled, Is.False);
    }

    [Test]
    public void OneActionCannotSilentlyInvokeBothSubmitAndSecondaryOperations()
    {
      using var fixture = new Fixture();
      var submit = fixture.Menu.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
      var reference = fixture.Reference(submit);
      var bindings = new MenuInputBindings(); bindings.Configure(submit: reference, secondary: new[] { reference });
      Assert.Throws<ArgumentException>(() => new MenuInputRouter(new Target(), bindings));
      Assert.That(submit.enabled, Is.False, "Invalid configuration must fail before acquiring input ownership.");
    }

    [Test]
    public void NativeUguiSubmitOwnershipSuppressesDuplicateSemanticConfirmation()
    {
      using var fixture = new Fixture();
      var submit = fixture.Menu.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
      var bindings = new MenuInputBindings();
      bindings.Configure(submit: fixture.Reference(submit), useNativeUguiNavigation: true);
      int nativeSubmits = 0;
      submit.performed += _ => nativeSubmits++;
      var target = new Target();
      using var router = new MenuInputRouter(target, bindings);
      fixture.Press(Key.Enter);
      Assert.That(nativeSubmits, Is.EqualTo(1));
      Assert.That(target.Confirms, Is.Zero, "Native Selectable Submit and the semantic router cannot both invoke an action.");
    }

    [Test]
    public void PassThroughPointerClickUsesTheLatestPointAndDoesNotConfirmAgainOnRelease()
    {
      using var fixture = new Fixture();
      var click = fixture.Menu.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
      var point = fixture.Menu.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position");
      var bindings = new MenuInputBindings();
      bindings.Configure(point: fixture.Reference(point), click: fixture.Reference(click), routePointerClick: true);
      var target = new Target();
      Vector2 pointAtConfirmation = default;
      target.OnConfirm = () => pointAtConfirmation = target.LastPoint;
      using var router = new MenuInputRouter(target, bindings);
      InputSystem.QueueStateEvent(fixture.Mouse, new MouseState { position = new Vector2(120, 200), buttons = 1 });
      InputSystem.Update();
      InputSystem.QueueStateEvent(fixture.Mouse, new MouseState { position = new Vector2(120, 200) });
      InputSystem.Update();
      Assert.That(target.Confirms, Is.EqualTo(1));
      Assert.That(pointAtConfirmation, Is.EqualTo(new Vector2(120, 200)), "Hit testing must see the same pointer state as confirmation.");
    }

    [Test]
    public void GamepadStickAndDpadRemainDistinctWithoutPollingAndStickReleaseClearsDirection()
    {
      using var fixture = new Fixture();
      var navigate = fixture.Menu.AddAction("Navigate", InputActionType.Value);
      navigate.expectedControlType = "Vector2";
      navigate.AddBinding("<Gamepad>/leftStick"); navigate.AddBinding("<Gamepad>/dpad");
      var bindings = new MenuInputBindings(); bindings.Configure(navigate: fixture.Reference(navigate));
      var target = new Target();
      using var router = new MenuInputRouter(target, bindings);
      fixture.PadState(new GamepadState { leftStick = Vector2.right });
      Assert.That(target.LastAnalog, Is.True);
      Assert.That(target.LastDirection.x, Is.GreaterThan(.9f));
      fixture.PadState(new GamepadState());
      Assert.That(target.LastDirection, Is.EqualTo(Vector2.zero));
      fixture.PadState(new GamepadState().WithButton(GamepadButton.DpadDown));
      Assert.That(target.LastAnalog, Is.False, "D-pad steps must not be interpreted as analog ring distance.");
      Assert.That(target.LastDirection.y, Is.LessThan(-.9f));
    }

    [Test]
    public void BackSecondaryRingAndPagesUseTheirBoundActionsAndRetireSubscriptions()
    {
      using var fixture = new Fixture();
      var back = fixture.Menu.AddAction("Back", InputActionType.Button, "<Gamepad>/buttonEast");
      var secondary = fixture.Menu.AddAction("Secondary", InputActionType.Button, "<Gamepad>/buttonWest");
      var ring = fixture.Menu.AddAction("Ring", InputActionType.Button, "<Gamepad>/rightShoulder");
      var forward = fixture.Menu.AddAction("Next", InputActionType.Button, "<Keyboard>/pageDown");
      var backward = fixture.Menu.AddAction("Previous", InputActionType.Button, "<Keyboard>/pageUp");
      var bindings = new MenuInputBindings();
      bindings.Configure(back: fixture.Reference(back), switchRing: fixture.Reference(ring),
        secondary: new[] { fixture.Reference(secondary) }, pageForward: fixture.Reference(forward), pageBackward: fixture.Reference(backward));
      var target = new Target();
      var router = new MenuInputRouter(target, bindings);
      fixture.PadState(new GamepadState().WithButton(GamepadButton.East));
      fixture.PadState(new GamepadState().WithButton(GamepadButton.West));
      fixture.PadState(new GamepadState().WithButton(GamepadButton.RightShoulder));
      fixture.Press(Key.PageDown); fixture.Press(Key.PageUp);
      Assert.That(target.Backs, Is.EqualTo(1)); Assert.That(target.SecondaryIndex, Is.Zero);
      Assert.That(target.RingChanges, Is.EqualTo(1)); Assert.That(target.Pages, Is.EqualTo(new[] { 1, -1 }));
      router.Dispose();
      back.Enable();
      fixture.PadState(new GamepadState());
      fixture.PadState(new GamepadState().WithButton(GamepadButton.East));
      Assert.That(target.Backs, Is.EqualTo(1), "Disposal must unsubscribe from externally retained runtime actions.");
    }

    [Test]
    public void HoldReleaseIsOneSemanticGestureAndDisposalDoesNotConfirmIt()
    {
      using var fixture = new Fixture();
      var hold = fixture.Menu.AddAction("Hold", InputActionType.Button, "<Gamepad>/leftShoulder");
      var bindings = new MenuInputBindings(); bindings.Configure(hold: fixture.Reference(hold));
      var target = new Target();
      var router = new MenuInputRouter(target, bindings);
      fixture.PadState(new GamepadState().WithButton(GamepadButton.LeftShoulder));
      fixture.PadState(new GamepadState());
      Assert.That(target.HoldsBegun, Is.EqualTo(1)); Assert.That(target.HoldsEnded, Is.EqualTo(1));
      fixture.PadState(new GamepadState().WithButton(GamepadButton.LeftShoulder));
      router.Dispose();
      Assert.That(target.HoldsEnded, Is.EqualTo(1), "Closing/disabling a held menu is cancellation, never release-to-confirm.");
    }

    [Test]
    public void DisablingAStillHeldActionCancelsRatherThanExecutingRelease()
    {
      using var fixture = new Fixture();
      var hold = fixture.Menu.AddAction("Hold", InputActionType.Button, "<Gamepad>/leftShoulder");
      var bindings = new MenuInputBindings(); bindings.Configure(hold: fixture.Reference(hold));
      var target = new Target();
      using var router = new MenuInputRouter(target, bindings);
      fixture.PadState(new GamepadState().WithButton(GamepadButton.LeftShoulder));
      hold.Disable();
      Assert.That(target.HoldsBegun, Is.EqualTo(1));
      Assert.That(target.HoldsEnded, Is.Zero, "Map disable or device loss must never be interpreted as release-to-confirm.");
      Assert.That(target.Cancels, Is.EqualTo(1));
    }

    [Test]
    public void HeldGameplayButtonRestoresOnlyAfterReleaseEvenWithInitialStateChecksEnabled()
    {
      using var fixture = new Fixture();
      var attack = fixture.Gameplay.AddAction("Attack", InputActionType.Button, "<Keyboard>/enter");
      attack.wantsInitialStateCheck = true;
      int shots = 0; attack.performed += _ => shots++; attack.Enable();
      using var owner = new MenuInputOwnership();
      var lease = owner.Acquire(new[] { attack }, Array.Empty<InputAction>());
      InputSystem.QueueStateEvent(fixture.Keyboard, new KeyboardState(Key.Enter)); InputSystem.Update();
      lease.Dispose();
      Assert.That(attack.enabled, Is.False, "An already held menu control must not become a new gameplay press.");
      InputSystem.Update();
      Assert.That(shots, Is.Zero);
      InputSystem.QueueStateEvent(fixture.Keyboard, new KeyboardState()); InputSystem.Update();
      Assert.That(attack.enabled, Is.True);
      Assert.That(attack.wantsInitialStateCheck, Is.True, "Restoration must not rewrite the action's configuration.");
      fixture.Press(Key.Enter);
      Assert.That(shots, Is.EqualTo(1));
    }

    [Test]
    public void LosingTheGuardedDeviceRestoresTheOriginalActionAndRetiresTheReleaseMonitor()
    {
      using var fixture = new Fixture();
      var attack = fixture.Gameplay.AddAction("Attack", InputActionType.Button, "<Keyboard>/enter");
      attack.Enable();
      using var owner = new MenuInputOwnership();
      var lease = owner.Acquire(new[] { attack }, Array.Empty<InputAction>());
      InputSystem.QueueStateEvent(fixture.Keyboard, new KeyboardState(Key.Enter)); InputSystem.Update();
      lease.Dispose();
      Assert.That(attack.enabled, Is.False);
      InputSystem.RemoveDevice(fixture.Keyboard);
      Assert.That(attack.enabled, Is.True, "A disconnected device cannot leave a gameplay action permanently suspended.");
    }

    [Test]
    public void ClosingFromSubmitCannotReplayThatPhysicalPressIntoGameplay()
    {
      using var fixture = new Fixture();
      var submit = fixture.Menu.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
      var attack = fixture.Gameplay.AddAction("Attack", InputActionType.Button, "<Keyboard>/enter");
      attack.wantsInitialStateCheck = true;
      int shots = 0; attack.performed += _ => shots++; attack.Enable();
      var target = new Target();
      var bindings = new MenuInputBindings(); bindings.Configure(submit: fixture.Reference(submit));
      MenuInputRouter router = null;
      target.OnConfirm = () => router.Dispose();
      router = new MenuInputRouter(target, bindings, gameplayActions: new[] { attack });
      fixture.Press(Key.Enter);
      Assert.That(target.Confirms, Is.EqualTo(1)); Assert.That(shots, Is.Zero);
      Assert.That(attack.enabled, Is.True, "The exact previously enabled action must be restored after input dispatch completes.");
      fixture.Press(Key.Enter);
      Assert.That(shots, Is.EqualTo(1), "The next fresh gameplay press must work normally.");
      router.Dispose();
    }

    [TestCase(InputActionType.Value)]
    [TestCase(InputActionType.PassThrough)]
    public void NavigationHandoffWaitsForProcessedStickNeutralBeforeRestoringTheAction(InputActionType type)
    {
      using var fixture = new Fixture();
      var move = fixture.Menu.AddAction("Native UI move", type, "<Gamepad>/leftStick");
      move.wantsInitialStateCheck = true;
      int moves = 0;
      var observed = new List<Vector2>();
      move.performed += value =>
      {
        var direction = value.ReadValue<Vector2>();
        observed.Add(direction);
        if (direction.sqrMagnitude > 0) moves++;
      };
      move.Enable();
      fixture.PadState(new GamepadState { leftStick = new Vector2(.8f, .5f) });
      moves = 0;
      observed.Clear();
      using var owner = new MenuInputOwnership();
      owner.DeferRestorationUntilNeutral(move);
      Assert.That(move.enabled, Is.False, "Native UI cannot replay a menu's still-held stick on restored focus.");
      InputSystem.Update();
      Assert.That(moves, Is.Zero);
      fixture.PadState(new GamepadState { leftStick = new Vector2(.6f, .2f) });
      Assert.That(move.enabled, Is.False);
      float noise = InputSystem.settings.defaultDeadzoneMin * .25f;
      fixture.PadState(new GamepadState { leftStick = new Vector2(noise, noise) });
      Assert.That(fixture.Pad.leftStick.ReadValue(), Is.EqualTo(Vector2.zero), "The control's configured deadzone defines neutral.");
      Assert.That(move.enabled, Is.True);
      InputSystem.Update();
      Assert.That(moves, Is.Zero, "The retired gesture must not become a new initial-state navigation event. Callback values: "
        + string.Join(", ", observed));
      foreach (var direction in observed)
        Assert.That(direction, Is.EqualTo(Vector2.zero), "A restoration callback may report neutral, never the retired actuation.");
      TestContext.WriteLine("Neutral restoration callbacks: " + observed.Count + "; values: " + string.Join(", ", observed));
      Assert.That(move.wantsInitialStateCheck, Is.True);
      fixture.PadState(new GamepadState { leftStick = Vector2.up });
      Assert.That(moves, Is.EqualTo(1), "A fresh navigation gesture remains available.");
    }

    [Test]
    public void SharedSemanticAndNativeMoveStaysEnabledUntilTheFinalMenuLeaseRetires()
    {
      using var fixture = new Fixture();
      var move = fixture.Menu.AddAction("Shared move", InputActionType.Value, "<Gamepad>/leftStick");
      move.Enable();
      using var owner = new MenuInputOwnership();
      var menu = owner.Acquire(null, new[] { move });
      fixture.PadState(new GamepadState { leftStick = Vector2.up });
      owner.DeferRestorationUntilNeutral(move);
      Assert.That(move.enabled, Is.True, "A handoff must not disable an action still used by the semantic menu.");
      Assert.That(move.ReadValue<Vector2>().sqrMagnitude, Is.GreaterThan(0));
      menu.Dispose();
      Assert.That(move.enabled, Is.False, "The last menu lease starts neutral quarantine.");
      fixture.PadState(new GamepadState());
      Assert.That(move.enabled, Is.True);
    }

    [Test]
    public void NativeMoveHandoffPreservesAnOriginallyDisabledAction()
    {
      using var fixture = new Fixture();
      var move = fixture.Menu.AddAction("Disabled UI move", InputActionType.Value, "<Gamepad>/leftStick");
      using var owner = new MenuInputOwnership();
      owner.DeferRestorationUntilNeutral(move);
      fixture.PadState(new GamepadState { leftStick = Vector2.up });
      fixture.PadState(new GamepadState());
      Assert.That(move.enabled, Is.False);
    }

    [Test]
    public void NeutralNativeMoveRestoresImmediatelyWithoutChangingItsMap()
    {
      using var fixture = new Fixture();
      var move = fixture.Menu.AddAction("UI move", InputActionType.Value, "<Gamepad>/leftStick");
      var disabledSibling = fixture.Menu.AddAction("Disabled sibling", InputActionType.Button, "<Gamepad>/buttonNorth");
      move.Enable();
      using var owner = new MenuInputOwnership();
      owner.DeferRestorationUntilNeutral(move);
      Assert.That(move.enabled, Is.True);
      Assert.That(disabledSibling.enabled, Is.False);
    }

    [Test]
    public void LosingTheQuarantinedNavigationDeviceRestoresTheOriginalAction()
    {
      using var fixture = new Fixture();
      var move = fixture.Menu.AddAction("UI move", InputActionType.Value, "<Gamepad>/leftStick");
      move.Enable();
      using var owner = new MenuInputOwnership();
      fixture.PadState(new GamepadState { leftStick = Vector2.up });
      owner.DeferRestorationUntilNeutral(move);
      Assert.That(move.enabled, Is.False);
      InputSystem.RemoveDevice(fixture.Pad);
      Assert.That(move.enabled, Is.True);
    }
    private sealed class Target : IMenuInputTarget
    {
      public int Confirms, Backs, Cancels, RingChanges, HoldsBegun, HoldsEnded, SecondaryIndex = -1;
      public Vector2 LastDirection, LastPoint;
      public bool LastAnalog;
      public readonly List<int> Pages = new();
      public Action OnConfirm;
      public void Navigate(Vector2 direction, bool analog) { LastDirection = direction; LastAnalog = analog; }
      public void Point(Vector2 screenPosition) => LastPoint = screenPosition;
      public void Confirm() { Confirms++; OnConfirm?.Invoke(); }
      public void Back() => Backs++;
      public void Cancel() => Cancels++;
      public void InvokeSecondary(int index) => SecondaryIndex = index;
      public void Page(int direction) => Pages.Add(direction);
      public void SwitchRing() => RingChanges++;
      public void BeginHold() => HoldsBegun++;
      public void EndHold() => HoldsEnded++;
    }

    private sealed class Source : IInputActionSource
    {
      private readonly InputActionAsset authored;
      public Source(InputActionAsset authored, InputActionAsset runtime) { this.authored = authored; RuntimeActions = runtime; }
      public InputActionAsset RuntimeActions { get; }
      public InputAction ResolveRuntimeAction(InputAction action)
        => action?.actionMap?.asset == authored && RuntimeActions != null ? RuntimeActions.FindAction(action.id) : action;
    }

    private sealed class Fixture : IDisposable
    {
      private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      private readonly Keyboard previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
      private readonly Gamepad previousPad = Gamepad.current;
      private readonly Mouse previousMouse = UnityEngine.InputSystem.Mouse.current;
      private readonly InputSettings settings = InputSystem.settings;
      private readonly InputSettings.UpdateMode previousMode;
      private readonly bool previousFeature;
      public readonly InputActionAsset Asset = ScriptableObject.CreateInstance<InputActionAsset>();
      public readonly Keyboard Keyboard;
      public readonly Gamepad Pad;
      public readonly Mouse Mouse;
      public readonly InputActionMap Gameplay, Menu;
      private readonly List<InputActionReference> references = new();
      public Fixture()
      {
        previousMode = settings.updateMode;
        previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(settings, new object[] { PlayerUpdates });
        try
        {
          // InputActionState deliberately ignores Editor updates. Run real player updates in this scoped
          // EditMode fixture, matching the established input-prompt tests rather than manufacturing callbacks.
          settings.SetInternalFeatureFlag(PlayerUpdates, true);
          settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
          Keyboard = InputSystem.AddDevice<Keyboard>();
          Pad = InputSystem.AddDevice<Gamepad>();
          Mouse = InputSystem.AddDevice<Mouse>();
          Asset.devices = new InputDevice[] { Keyboard, Pad, Mouse };
          Gameplay = Asset.AddActionMap("Gameplay"); Menu = Asset.AddActionMap("Menu");
        }
        catch { Dispose(); throw; }
      }
      public InputActionReference Reference(InputAction action) { var reference = InputActionReference.Create(action); references.Add(reference); return reference; }
      public void Press(Key key)
      {
        InputSystem.QueueStateEvent(Keyboard, new KeyboardState()); InputSystem.Update();
        InputSystem.QueueStateEvent(Keyboard, new KeyboardState(key)); InputSystem.Update();
        InputSystem.QueueStateEvent(Keyboard, new KeyboardState()); InputSystem.Update();
      }
      public void PadState(GamepadState state) { InputSystem.QueueStateEvent(Pad, state); InputSystem.Update(); }
      public void Dispose()
      {
        try
        {
          Asset.Disable();
          // Device-loss restoration may still hold a release guard after a failed assertion. Retire
          // those guards while their action asset is alive, then disable and destroy only our own assets.
          if (Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
          if (Pad != null && Pad.added) InputSystem.RemoveDevice(Pad);
          if (Mouse != null && Mouse.added) InputSystem.RemoveDevice(Mouse);
          Asset.Disable();
          foreach (var reference in references) Object.DestroyImmediate(reference);
          Object.DestroyImmediate(Asset);
        }
        finally
        {
          settings.SetInternalFeatureFlag(PlayerUpdates, previousFeature);
          settings.updateMode = previousMode;
          if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
          if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
          if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
      }
    }
  }
}
