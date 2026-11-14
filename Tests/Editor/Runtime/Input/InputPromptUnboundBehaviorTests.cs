using System.Collections.Generic;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputPromptUnboundBehaviorTests
  {
    private readonly List<Object> owned = new();
    private InputGlyphCatalog catalog;
    private InputPromptResolver resolver;

    [SetUp]
    public void SetUp()
    {
      catalog = Own(ScriptableObject.CreateInstance<InputGlyphCatalog>());
      resolver = new InputPromptResolver(catalog);
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
      owned.Clear();
    }

    [TestCase(InputPromptUnboundBehavior.ShowUnbound, "Unbound")]
    [TestCase(InputPromptUnboundBehavior.KeyboardAndMouse, "E")]
    [TestCase(InputPromptUnboundBehavior.Hide, "")]
    public void UnboundSelectionUsesTheRequestedPolicyWithNonstandardBindingGroups(
      InputPromptUnboundBehavior behavior, string expected)
    {
      var original = new InputPromptContext(null, "OfficeDevices", InputGlyphFamily.PlayStation, 4, null,
        InputGlyphPlatform.MacOS);
      var copied = original.WithUnboundBehavior(behavior);
      Assert.That(new object[] { copied.Device, copied.BindingGroup, copied.FamilyOverride, copied.BindingIndex,
        copied.FontAsset, copied.PlatformOverride }, Is.EqualTo(new object[] { original.Device, original.BindingGroup,
        original.FamilyOverride, original.BindingIndex, original.FontAsset, original.PlatformOverride }));
      Assert.That(copied.WithPlatform(InputGlyphPlatform.Windows).UnboundBehavior, Is.EqualTo(behavior));
      using var action = new InputAction("Build");
      action.AddBinding("<Keyboard>/e", groups: "OfficeDevices");
      var context = ControllerContext(behavior);
      Assert.That(resolver.Resolve(action, InputPromptStyle.Character, context), Is.EqualTo(expected));
      Assert.That(action.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
      Assert.That(action.enabled, Is.False);
      Assert.That(new InputPromptContext().UnboundBehavior, Is.EqualTo(InputPromptUnboundBehavior.ShowUnbound));
    }

    [TestCase(InputPromptUnboundBehavior.KeyboardAndMouse)]
    [TestCase(InputPromptUnboundBehavior.Hide)]
    public void EligibleGamepadBindingWithMissingArtworkDoesNotFallBackOrHide(InputPromptUnboundBehavior behavior)
    {
      using var action = new InputAction("Confirm");
      action.AddBinding("<Keyboard>/e", groups: "OfficeDevices");
      action.AddBinding("<Gamepad>/buttonSouth", groups: "ConsoleDevices");
      catalog.SetEntries(new InputGlyphCatalog.Entry { ControlPath = "e", Symbol = "[DESKTOP]" });
      Assert.That(resolver.Resolve(action, InputPromptStyle.Glyph, ControllerContext(behavior)), Is.EqualTo("Cross"),
        "A selected controller control stays selected even when its glyph is absent.");
    }

    [Test]
    public void KeyboardFallbackWithoutAnEligibleDesktopBindingRemainsUnbound()
    {
      using var action = new InputAction("Unknown device action");
      action.AddBinding("<Joystick>/trigger", groups: "OtherDevices");
      Assert.That(resolver.Resolve(action, context: ControllerContext(InputPromptUnboundBehavior.KeyboardAndMouse)),
        Is.EqualTo("Unbound"));
    }

    [Test]
    public void DesktopMouseChordUsesEffectiveOverridesAndResetWithoutChangingRouting()
    {
      using var action = new InputAction("Desktop tool");
      action.AddCompositeBinding("OneModifier")
        .With("Modifier", "<Keyboard>/leftCtrl").With("Binding", "<Mouse>/rightButton");
      action.ChangeBinding(0).WithGroup("OfficeDevices");
      var context = ControllerContext(InputPromptUnboundBehavior.KeyboardAndMouse);
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName, context), Is.EqualTo("LCtrl + Right Click"));
      action.ApplyBindingOverride(2, "<Mouse>/middleButton");
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName, context), Is.EqualTo("LCtrl + Middle Click"));
      action.RemoveBindingOverride(2);
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName, context), Is.EqualTo("LCtrl + Right Click"));
      action.ApplyBindingOverride(2, string.Empty);
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName, context), Is.EqualTo("Unbound"),
        "An explicitly unbound required chord part must not reveal its authored path.");
      action.RemoveBindingOverride(2);
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName, context), Is.EqualTo("LCtrl + Right Click"));
      Assert.That(action.enabled, Is.False);
    }

    [TestCase(0, "Unbound")]
    [TestCase(1, "E")]
    public void ExplicitBindingIndexNeverFallsBackToAnotherSlot(int index, string expected)
    {
      using var action = new InputAction("Pinned slot");
      action.AddBinding("<Gamepad>/buttonSouth", groups: "ConsoleDevices");
      action.AddBinding("<Keyboard>/e", groups: "OfficeDevices");
      action.ApplyBindingOverride(0, string.Empty);
      var context = new InputPromptContext(bindingGroup: "ConsoleDevices", bindingIndex: index)
        .WithUnboundBehavior(InputPromptUnboundBehavior.KeyboardAndMouse);
      Assert.That(resolver.Resolve(action, InputPromptStyle.Character, context), Is.EqualTo(expected));
    }

    [TestCase("action")]
    [TestCase("map")]
    [TestCase("asset")]
    public void DesktopFallbackHonorsEveryNativeBindingMask(string owner)
    {
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      var map = asset.AddActionMap("Player");
      var action = map.AddAction("Restricted");
      action.AddBinding("<Keyboard>/e", groups: "OfficeDevices");
      var mask = InputBinding.MaskByGroup("ConsoleDevices");
      if (owner == "action") action.bindingMask = mask;
      else if (owner == "map") map.bindingMask = mask;
      else asset.bindingMask = mask;
      Assert.That(resolver.Resolve(action, context: ControllerContext(InputPromptUnboundBehavior.KeyboardAndMouse)),
        Is.EqualTo("Unbound"), "A presentation fallback cannot bypass the " + owner + " binding mask.");
    }

    [Test]
    public void DesktopFallbackStaysWithinThePlayersAllowedDevices()
    {
      var previousKeyboard = Keyboard.current;
      var previousGamepad = Gamepad.current;
      var keyboard = InputSystem.AddDevice<Keyboard>();
      var paired = InputSystem.AddDevice<Gamepad>();
      var foreign = InputSystem.AddDevice<Gamepad>();
      try
      {
        var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
        var action = asset.AddActionMap("Player").AddAction("Desktop tool");
        action.AddBinding("<Keyboard>/e", groups: "OfficeDevices");
        asset.devices = new InputDevice[] { paired };
        var context = new InputPromptContext(paired, "ConsoleDevices")
          .WithUnboundBehavior(InputPromptUnboundBehavior.KeyboardAndMouse);
        Assert.That(resolver.Resolve(action, InputPromptStyle.Character, context), Is.EqualTo("Unbound"));
        asset.devices = new InputDevice[] { paired, keyboard };
        Assert.That(resolver.Resolve(action, InputPromptStyle.Character, context), Is.EqualTo("E"));
        var foreignContext = new InputPromptContext(foreign, "ConsoleDevices")
          .WithUnboundBehavior(InputPromptUnboundBehavior.KeyboardAndMouse);
        Assert.That(resolver.Resolve(action, InputPromptStyle.Character, foreignContext), Is.EqualTo("Unbound"),
          "Clearing an input preference must not authorize another player's device context.");
        Assert.That(action.enabled, Is.False);
      }
      finally
      {
        if (foreign.added) InputSystem.RemoveDevice(foreign);
        if (paired.added) InputSystem.RemoveDevice(paired);
        if (keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      }
    }

    [Test]
    public void DesktopFallbackDoesNotReduceAMixedDeviceChordToItsKeyboardModifier()
    {
      using var action = new InputAction("Mixed chord");
      action.AddCompositeBinding("OneModifier")
        .With("Modifier", "<Keyboard>/leftCtrl").With("Binding", "<Gamepad>/buttonSouth");
      action.ChangeBinding(0).WithGroup("OfficeDevices");
      Assert.That(resolver.Resolve(action, context: ControllerContext(InputPromptUnboundBehavior.KeyboardAndMouse)),
        Is.EqualTo("Unbound"));
    }

    [Test]
    public void DesktopFallbackSkipsAMixedCompositeBeforeSelectingALaterValidKeyboardBinding()
    {
      using var action = new InputAction("Desktop alternative");
      action.AddCompositeBinding("OneModifier")
        .With("Modifier", "<Keyboard>/leftCtrl").With("Binding", "<Gamepad>/buttonSouth");
      action.ChangeBinding(0).WithGroup("OfficeDevices");
      action.AddBinding("<Keyboard>/e", groups: "OfficeDevices");
      Assert.That(resolver.Resolve(action, InputPromptStyle.Character,
        ControllerContext(InputPromptUnboundBehavior.KeyboardAndMouse)), Is.EqualTo("E"),
        "Desktop eligibility is checked before choosing a winning binding, so an earlier mixed chord cannot shadow a valid key.");
      Assert.That(action.enabled, Is.False);
    }

    [TestCase(InputPromptUnboundBehavior.ShowUnbound, "Unbound")]
    [TestCase(InputPromptUnboundBehavior.KeyboardAndMouse, "Unbound")]
    [TestCase(InputPromptUnboundBehavior.Hide, "")]
    public void EmptyExplicitPathUsesPolicyWithoutInventingAnActionBinding(InputPromptUnboundBehavior behavior, string expected)
    {
      var context = ControllerContext(behavior);
      Assert.That(resolver.ResolvePath(null, context: context), Is.EqualTo(expected));
      Assert.That(resolver.ResolvePath(string.Empty, context: context), Is.EqualTo(expected));
      Assert.That(resolver.ResolvePath("<Mouse>/rightButton", InputPromptStyle.LocalizedName, context),
        Is.EqualTo("Right Click"), "A nonempty explicit path remains the requested actual control.");
    }

    private static InputPromptContext ControllerContext(InputPromptUnboundBehavior behavior)
      => new InputPromptContext(bindingGroup: "ConsoleDevices", familyOverride: InputGlyphFamily.PlayStation)
        .WithUnboundBehavior(behavior).WithPlatform(InputGlyphPlatform.Windows);

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
  }
}
