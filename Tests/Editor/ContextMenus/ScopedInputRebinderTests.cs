using System.Collections.Generic;
using System.Reflection;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Input.Interfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class ScopedInputRebinderTests
  {
    private readonly List<InputActionAsset> assets = new();
    private InputRebinderService rebinder;
    private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
    private InputSettings.UpdateMode previousMode;
    private bool previousFeature;
    [SetUp]
    public void Setup()
    {
      previousMode = InputSystem.settings.updateMode;
      previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.NonPublic | BindingFlags.Instance)
        .Invoke(InputSystem.settings, new object[] { PlayerUpdates });
      InputSystem.settings.SetInternalFeatureFlag(PlayerUpdates, true);
      InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
      rebinder = new InputRebinderService();
    }
    [TearDown]
    public void Cleanup()
    {
      rebinder?.Dispose(); rebinder = null;
      foreach (var asset in assets) if (asset != null) { asset.Disable(); Object.DestroyImmediate(asset); }
      assets.Clear();
      InputSystem.settings.SetInternalFeatureFlag(PlayerUpdates, previousFeature);
      InputSystem.settings.updateMode = previousMode;
    }
    private InputAction Action()
    {
      var asset = ScriptableObject.CreateInstance<InputActionAsset>(); assets.Add(asset);
      return asset.AddActionMap("Menu").AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
    }
    private InputAction Clone(InputAction action)
    {
      var asset = Object.Instantiate(action.actionMap.asset); assets.Add(asset);
      return asset.FindAction(action.id);
    }
    private IScopedInputRebinderService Scoped => (IScopedInputRebinderService)rebinder;

    [Test]
    public void Scoped_registrations_are_reference_counted_and_disposal_is_idempotent()
    {
      var action = Action();
      var first = Scoped.RegisterScopedAction(action);
      var second = Scoped.RegisterScopedAction(action);
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      first.Dispose(); first.Dispose();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      second.Dispose(); second.Dispose();
      Assert.That(rebinder.RegisteredActions, Is.Empty);
    }

    [Test]
    public void Scoped_release_preserves_a_permanent_application_registration()
    {
      var action = Action(); rebinder.RegisterAction(action);
      var registration = Scoped.RegisterScopedAction(action);
      registration.Dispose();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      foreach (var registered in rebinder.RegisteredActions) Assert.That(ReferenceEquals(registered, action), Is.True);
    }

    [Test]
    public void Same_named_permanent_instances_with_the_same_action_id_remain_distinct()
    {
      var first = Action(); var second = Clone(first);
      Assert.That(first.id, Is.EqualTo(second.id));
      rebinder.RegisterAction(first); rebinder.RegisterAction(second);
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(2), "Name or GUID collision discarded a different actual execution action");
    }

    [Test]
    public void Global_reset_and_conflict_lookup_see_every_live_scoped_execution_instance()
    {
      var first = Action(); var second = Clone(first);
      using var firstOwner = Scoped.RegisterScopedAction(first);
      using var secondOwner = Scoped.RegisterScopedAction(second);
      first.ApplyBindingOverride(0, "<Keyboard>/q"); second.ApplyBindingOverride(0, "<Keyboard>/q");
      Assert.That(rebinder.GetActionsUsingBindingPath("<Keyboard>/q").Count, Is.EqualTo(2));
      rebinder.ResetAllBindings();
      Assert.That(first.bindings[0].overridePath, Is.Null);
      Assert.That(second.bindings[0].overridePath, Is.Null, "Reset modified an old same-name clone instead of the live instance");
    }

    [Test]
    public void Last_scoped_release_cancels_the_owned_interactive_rebind_before_removing_it()
    {
      var action = Action(); action.Enable();
      var registration = Scoped.RegisterScopedAction(action);
      Assert.That(rebinder.StartInteractiveRebinding(action, 0), Is.True);
      Assert.That(rebinder.IsRebinding, Is.True);
      registration.Dispose();
      Assert.That(rebinder.IsRebinding, Is.False, "A rebinding operation outlived its last action owner");
      Assert.That(rebinder.RegisteredActions, Is.Empty);
      Assert.That(action.enabled, Is.True, "Cancellation failed to restore its pre-rebind input state");
    }

    [Test]
    public void Starting_and_canceling_rebind_does_not_promote_a_scoped_action_to_permanent()
    {
      var action = Action();
      var registration = Scoped.RegisterScopedAction(action);
      Assert.That(rebinder.StartInteractiveRebinding(action, 0), Is.True);
      rebinder.CancelRebinding();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      registration.Dispose();
      Assert.That(rebinder.RegisteredActions, Is.Empty, "StartInteractiveRebinding made a transient menu clone permanent");
    }
  }
}
