using System;
using System.Reflection;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KeyboardDevice = UnityEngine.InputSystem.Keyboard;
using GamepadDevice = UnityEngine.InputSystem.Gamepad;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputPromptForeignObservationReviewTests
  {
    [TestCase(false)]
    [TestCase(true)]
    public void WatchedForeignBindingRefreshesWithAnOwnedAssetOrPlayerInput(bool playerOwned)
    {
      using var fixture = new Fixture(playerOwned);
      var text = fixture.Text;
      Assert.That(text.text, Is.EqualTo("E"));
      fixture.Foreign.ApplyBindingOverride(0, "<Keyboard>/q");
      Assert.That(text.text, Is.EqualTo("Q"),
        "Binding observation includes the foreign execution action actually rendered by this view.");
      fixture.Foreign.RemoveBindingOverride(0);
      Assert.That(text.text, Is.EqualTo("E"));
      Assert.That(fixture.Text, Is.SameAs(text));
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Keyboard));
      fixture.Binding.Dispose();
      int updates = 0;
      using var observation = fixture.Service.Changed.Subscribe(_ => updates++);
      fixture.Foreign.ApplyBindingOverride(0, "<Keyboard>/w");
      Assert.That(updates, Is.Zero, "Disposing the view releases its foreign binding observation.");
      Assert.That(text.text, Is.EqualTo("E"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WatchedForeignActivityCannotChangeTheOwnersDevice(bool playerOwned)
    {
      using var fixture = new Fixture(playerOwned);
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Keyboard));
      int updates = 0;
      using var observation = fixture.Service.Changed.Subscribe(_ => updates++);
      InputSystem.QueueStateEvent(fixture.Gamepad, new GamepadState().WithButton(GamepadButton.South));
      InputSystem.Update();
      Assert.That(fixture.Foreign.activeControl?.device, Is.SameAs(fixture.Gamepad),
        "The simulated foreign action really performed before checking ownership.");
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Keyboard),
        "Watching a foreign prompt is not permission for its input to replace the owning player's context.");
      Assert.That(updates, Is.Zero, "Foreign activity must not produce a spurious owning-player device refresh.");
      Assert.That(fixture.Text.text, Is.EqualTo("E"));
    }

    [Test]
    public void WatchedForeignBindingRefreshesWithAnInputActionSourceOwner()
    {
      using var fixture = new Fixture(false, sourceOwned: true);
      var text = fixture.Text;
      fixture.Foreign.ApplyBindingOverride(0, "<Keyboard>/q");
      Assert.That(text.text, Is.EqualTo("Q"));
      fixture.Foreign.RemoveBindingOverride(0);
      Assert.That(text.text, Is.EqualTo("E"));
      Assert.That(fixture.Text, Is.SameAs(text));
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Keyboard));
      fixture.Binding.Dispose();
      int updates = 0;
      using var observation = fixture.Service.Changed.Subscribe(_ => updates++);
      fixture.Foreign.ApplyBindingOverride(0, "<Keyboard>/w");
      Assert.That(updates, Is.Zero, "The disposed foreign view no longer contributes binding relevance.");
    }

    [Test]
    public void WatchedForeignActivityCannotChangeAnInputActionSourceOwnersDevice()
    {
      using var fixture = new Fixture(false, sourceOwned: true);
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Keyboard));
      int updates = 0;
      using var observation = fixture.Service.Changed.Subscribe(_ => updates++);
      InputSystem.QueueStateEvent(fixture.Gamepad, new GamepadState().WithButton(GamepadButton.South));
      InputSystem.Update();
      Assert.That(fixture.Foreign.activeControl?.device, Is.SameAs(fixture.Gamepad));
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Keyboard),
        "An action source's runtime asset owns device activity even while its service watches a foreign view.");
      Assert.That(updates, Is.Zero);
      Assert.That(fixture.Text.text, Is.EqualTo("E"));
    }

    private sealed class OwnedActionSource : IInputActionSource
    {
      public OwnedActionSource(InputActionAsset runtimeActions) => RuntimeActions = runtimeActions;
      public InputActionAsset RuntimeActions { get; }
      public InputAction ResolveRuntimeAction(InputAction action) => action;
    }

    private sealed class Fixture : IDisposable
    {
      private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      private readonly KeyboardDevice previousKeyboard = KeyboardDevice.current;
      private readonly GamepadDevice previousGamepad = GamepadDevice.current;
      private readonly InputSettings.UpdateMode previousMode = InputSystem.settings.updateMode;
      private readonly bool previousFeature;
      private readonly InputActionAsset owner = ScriptableObject.CreateInstance<InputActionAsset>();
      private readonly InputActionAsset foreign = ScriptableObject.CreateInstance<InputActionAsset>();
      private readonly GameObject host = new("Foreign observed prompt", typeof(RectTransform));
      private GameObject playerHost;
      private PlayerInput player;
      private InputActionAsset runtimeOwner;
      public KeyboardDevice Keyboard { get; }
      public GamepadDevice Gamepad { get; }
      public InputAction Foreign { get; }
      public InputPromptService Service { get; }
      public TMP_Text Text { get; }
      public IDisposable Binding { get; }

      public Fixture(bool playerOwned, bool sourceOwned = false)
      {
        try
        {
          previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(InputSystem.settings, new object[] { PlayerUpdates });
          InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
          InputSystem.settings.SetInternalFeatureFlag(PlayerUpdates, true);
          Keyboard = InputSystem.AddDevice<KeyboardDevice>();
          Gamepad = InputSystem.AddDevice<GamepadDevice>();
          owner.name = "Owned prompt context";
          var own = owner.AddActionMap("Owner").AddAction("Activate", InputActionType.Button);
          own.AddBinding("<Keyboard>/b", groups: "Keyboard&Mouse");
          owner.AddControlScheme("Keyboard").WithBindingGroup("Keyboard&Mouse").WithRequiredDevice("<Keyboard>");
          owner.devices = new InputDevice[] { Keyboard };
          Foreign = foreign.AddActionMap("Foreign").AddAction("Inspect", InputActionType.Button);
          Foreign.AddBinding("<Keyboard>/e", groups: "Keyboard&Mouse");
          Foreign.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
          Foreign.Enable();
          if (playerOwned)
          {
            playerHost = new GameObject("Owned synthetic PlayerInput");
            playerHost.SetActive(false);
            player = playerHost.AddComponent<InputPromptReviewPlayerInput>();
            player.actions = owner;
            player.defaultActionMap = "Owner";
            player.defaultControlScheme = "Keyboard";
            player.neverAutoSwitchControlSchemes = true;
            player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            playerHost.SetActive(true);
            Assert.That(player.user.valid, Is.True, "Initialize the actual PlayerInput user before switching its scheme.");
            player.SwitchCurrentControlScheme("Keyboard", Keyboard);
            runtimeOwner = player.actions;
            Service = new InputPromptService(playerInput: player);
          }
          else
          {
            own.Enable();
            runtimeOwner = owner;
            Service = sourceOwned ? new InputPromptService(actionSource: new OwnedActionSource(owner))
              : new InputPromptService(actions: owner);
          }
          Text = host.AddComponent<TextMeshProUGUI>();
          Binding = Service.Bind(Text, () => Service.Resolve(Foreign, InputPromptStyle.Character,
            new InputPromptContext(bindingGroup: "Keyboard&Mouse", familyOverride: InputGlyphFamily.KeyboardAndMouse)));
          InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.B));
          InputSystem.Update();
        }
        catch { Dispose(); throw; }
      }

      public void Dispose()
      {
        Binding?.Dispose();
        Service?.Dispose();
        foreign.Disable();
        owner.Disable();
        Object.DestroyImmediate(host);
        if (playerHost != null) Object.DestroyImmediate(playerHost);
        if (runtimeOwner != null && runtimeOwner != owner) Object.DestroyImmediate(runtimeOwner);
        Object.DestroyImmediate(foreign);
        Object.DestroyImmediate(owner);
        InputSystem.settings.SetInternalFeatureFlag(PlayerUpdates, previousFeature);
        InputSystem.settings.updateMode = previousMode;
        if (Gamepad != null && Gamepad.added) InputSystem.RemoveDevice(Gamepad);
        if (Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
      }
    }
  }

  // Unity drives the inherited native lifecycle in EditMode, including real InputUser pairing.
  [ExecuteAlways]
  public sealed class InputPromptReviewPlayerInput : PlayerInput { }
}
