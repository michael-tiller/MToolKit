using System.Linq;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputBindingSelectionReviewTests
  {
    private Keyboard previousKeyboard, keyboard;
    private Mouse previousMouse, mouse;
    private Gamepad previousGamepad, gamepad;
    private InputBindingResolver resolver;

    [SetUp]
    public void SetUp()
    {
      previousKeyboard = Keyboard.current;
      previousMouse = Mouse.current;
      previousGamepad = Gamepad.current;
      keyboard = InputSystem.AddDevice<Keyboard>();
      mouse = InputSystem.AddDevice<Mouse>();
      gamepad = InputSystem.AddDevice<Gamepad>();
      resolver = new InputBindingResolver();
    }

    [TearDown]
    public void TearDown()
    {
      if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
      if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
      if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
      if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
      if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
    }

    [TestCase(true, true, "e")]
    [TestCase(true, false, "leftButton")]
    [TestCase(false, true, "e")]
    [TestCase(false, false, "leftButton")]
    public void PreferredDesktopDeviceWinsRegardlessOfBindingOrder(
      bool mouseFirst, bool keyboardPreferred, string expectedControl)
    {
      using var map = new InputActionMap("Owned desktop player");
      map.devices = new InputDevice[] { keyboard, mouse };
      var action = map.AddAction("Use");
      action.AddBinding(mouseFirst ? "<Mouse>/leftButton" : "<Keyboard>/e", groups: "Workstation");
      action.AddBinding(mouseFirst ? "<Keyboard>/e" : "<Mouse>/leftButton", groups: "Workstation");
      InputDevice preferred = keyboardPreferred ? (InputDevice)keyboard : mouse;
      var selected = resolver.Resolve(action, new InputPromptContext(preferred, "Workstation"));
      AssertControls(selected, expectedControl);
      Assert.That(selected.Action, Is.SameAs(action));
      Assert.That(action.enabled, Is.False, "Presentation must not enable the execution action.");
    }

    [Test]
    public void PreferredDesktopDeviceUsesEffectiveUnboundOverridesAndReset()
    {
      using var action = new InputAction("Use");
      action.AddBinding("<Mouse>/leftButton");
      action.AddBinding("<Keyboard>/e");
      var context = new InputPromptContext(keyboard);
      action.ApplyBindingOverride(1, string.Empty);
      AssertControls(resolver.Resolve(action, context), "leftButton");
      action.ApplyBindingOverride(1, "<Keyboard>/f");
      AssertControls(resolver.Resolve(action, context), "f");
      action.RemoveBindingOverride(1);
      AssertControls(resolver.Resolve(action, context), "e");
      action.ApplyBindingOverride(0, string.Empty);
      AssertControls(resolver.Resolve(action, context), "e");
      action.ApplyBindingOverride(1, string.Empty);
      Assert.That(resolver.Resolve(action, context).IsBound, Is.False);
      Assert.That(action.enabled, Is.False);
    }

    [Test]
    public void LatestMouseContextRetainsAKeyboardOnlyHotkey()
    {
      using var action = new InputAction("Keyboard-only shortcut", binding: "<Keyboard>/e");
      // The automatic service uses this same snapshot after relevant mouse activity.
      // Device preference must retain the desktop companion when no exact candidate exists.
      AssertControls(resolver.Resolve(action, new InputPromptContext(mouse)), "e");
      Assert.That(action.enabled, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MixedKeyboardMouseChordRetainsBothParts(bool keyboardPreferred)
    {
      using var action = new InputAction("Modified click");
      action.AddCompositeBinding("OneModifier")
        .With("Modifier", "<Keyboard>/leftCtrl").With("Binding", "<Mouse>/leftButton");
      InputDevice preferred = keyboardPreferred ? (InputDevice)keyboard : mouse;
      var selected = resolver.Resolve(action, new InputPromptContext(preferred));
      AssertControls(selected, "leftCtrl", "leftButton");
      Assert.That(selected.CompositeName, Is.EqualTo("OneModifier"));
      Assert.That(action.enabled, Is.False);
    }

    [Test]
    public void DevicePreferenceStillHonorsMasksAndThePlayersAllowedDevices()
    {
      using var map = new InputActionMap("Owned desktop player");
      map.devices = new InputDevice[] { keyboard, mouse };
      var action = map.AddAction("Use");
      action.AddBinding("<Mouse>/leftButton", groups: "Pointer");
      action.AddBinding("<Keyboard>/e", groups: "Typing");
      action.bindingMask = InputBinding.MaskByGroup("Pointer");
      AssertControls(resolver.Resolve(action, new InputPromptContext(keyboard)), "leftButton");
      action.bindingMask = null;
      map.devices = new InputDevice[] { mouse };
      Assert.That(resolver.Resolve(action, new InputPromptContext(keyboard)).IsBound, Is.False);
      map.devices = new InputDevice[] { keyboard };
      Assert.That(resolver.Resolve(action, new InputPromptContext(mouse)).IsBound, Is.False);
      AssertControls(resolver.Resolve(action, new InputPromptContext(keyboard)), "e");
    }

    [Test]
    public void ExplicitBindingSlotStillWinsOverDevicePreference()
    {
      using var action = new InputAction("Pinned slot");
      action.AddBinding("<Mouse>/leftButton");
      action.AddBinding("<Keyboard>/e");
      var pointerSlot = resolver.Resolve(action, new InputPromptContext(keyboard, bindingIndex: 0));
      AssertControls(pointerSlot, "leftButton");
      Assert.That(pointerSlot.BindingIndex, Is.EqualTo(0));
      var keyboardSlot = resolver.Resolve(action, new InputPromptContext(mouse, bindingIndex: 1));
      AssertControls(keyboardSlot, "e");
      Assert.That(keyboardSlot.BindingIndex, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompositeAlternativesUseTheFirstEligibleRepresentativePerRole(bool sticks)
    {
      using var action = CreateAlternativeMove(sticks);
      var selected = resolver.Resolve(action, MoveContext(sticks));
      AssertControls(selected, MovePaths(sticks, false));
      Assert.That(selected.Controls.Select(control => control.PartName).Distinct().Count(), Is.EqualTo(4));
      Assert.That(selected.CompositeName, Is.EqualTo("2DVector"));
      Assert.That(action.enabled, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompositeAlternativesSkipEmptyOverridesAndMaskedFirstCandidates(bool sticks)
    {
      using var action = CreateAlternativeMove(sticks);
      for (int index = 1; index < action.bindings.Count; index += 2)
        action.ApplyBindingOverride(index, string.Empty);
      AssertControls(resolver.Resolve(action, MoveContext(sticks)), MovePaths(sticks, true));
      action.RemoveAllBindingOverrides();
      AssertControls(resolver.Resolve(action, MoveContext(sticks)), MovePaths(sticks, false));
      action.bindingMask = InputBinding.MaskByGroup("Alternative");
      AssertControls(resolver.Resolve(action, MoveContext(sticks)), MovePaths(sticks, true));
      action.bindingMask = null;
      InputDevice preferred = sticks ? (InputDevice)gamepad : keyboard;
      AssertControls(resolver.Resolve(action, new InputPromptContext(preferred, "Alternative")), MovePaths(sticks, true));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompositeAlternativesRemainUnboundWhenEveryPartIsUnbound(bool sticks)
    {
      using var action = CreateAlternativeMove(sticks);
      for (int index = 1; index < action.bindings.Count; index++)
        action.ApplyBindingOverride(index, string.Empty);
      Assert.That(resolver.Resolve(action, MoveContext(sticks)).IsBound, Is.False);
      Assert.That(action.enabled, Is.False);
    }

    [Test]
    public void ModifierAlternativesKeepACompleteChordAndDoNotRevealUnboundParts()
    {
      using var action = new InputAction("Alternative chord");
      action.AddCompositeBinding("OneModifier")
        .With("Modifier", "<Keyboard>/leftCtrl").With("Modifier", "<Keyboard>/rightCtrl")
        .With("Binding", "<Keyboard>/e").With("Binding", "<Keyboard>/q");
      action.ApplyBindingOverride(1, string.Empty);
      action.ApplyBindingOverride(3, string.Empty);
      AssertControls(resolver.Resolve(action, new InputPromptContext(keyboard)), "rightCtrl", "q");
      action.ApplyBindingOverride(4, string.Empty);
      Assert.That(resolver.Resolve(action, new InputPromptContext(keyboard)).IsBound, Is.False,
        "A remaining modifier cannot represent a chord whose binding alternatives are all unbound.");
    }

    private InputAction CreateAlternativeMove(bool sticks)
    {
      var action = new InputAction("Move", InputActionType.Value);
      var primary = MovePaths(sticks, false);
      var alternative = MovePaths(sticks, true);
      string device = sticks ? "<Gamepad>/" : "<Keyboard>/";
      var syntax = action.AddCompositeBinding("2DVector");
      var roles = new[] { "Up", "Down", "Left", "Right" };
      for (int index = 0; index < roles.Length; index++)
        syntax = syntax.With(roles[index], device + primary[index], groups: "Primary")
          .With(roles[index], device + alternative[index], groups: "Alternative");
      return action;
    }

    private InputPromptContext MoveContext(bool sticks)
      => new InputPromptContext(sticks ? (InputDevice)gamepad : keyboard);

    private static string[] MovePaths(bool sticks, bool alternative)
      => sticks ? new[] {
          (alternative ? "rightStick" : "leftStick") + "/up",
          (alternative ? "rightStick" : "leftStick") + "/down",
          (alternative ? "rightStick" : "leftStick") + "/left",
          (alternative ? "rightStick" : "leftStick") + "/right"
        } : alternative ? new[] { "upArrow", "downArrow", "leftArrow", "rightArrow" }
          : new[] { "w", "s", "a", "d" };

    private static void AssertControls(ResolvedInputBinding selected, params string[] expected)
    {
      Assert.That(selected.IsBound, Is.True);
      CollectionAssert.AreEqual(expected, selected.Controls.Select(control => control.ControlPath).ToArray());
    }
  }
}
