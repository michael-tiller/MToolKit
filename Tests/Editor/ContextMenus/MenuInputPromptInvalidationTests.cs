using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.ContextMenus.Input;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuInputPromptInvalidationTests
  {
    [TestCase(false)]
    [TestCase(true)]
    public void LargeMenuLeasePreservesPromptOutputWithoutRefreshingForEnableState(bool glyph)
    {
      using var fixture = new Fixture(glyph);
      using var owner = new MenuInputOwnership();
      string before = fixture.Text.text;
      using var lease = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit, fixture.Secondary });
      fixture.AssertGameplay(false);
      Assert.That(fixture.Submit.enabled, Is.True);
      Assert.That(fixture.Text.text, Is.EqualTo(before));
      Assert.That(fixture.RenderCalls, Is.Zero,
        "Suspending many actions changes routing, not their effective bindings or prompt output.");
      Assert.That(fixture.Notifications, Is.Zero, "A presenter also observes Changed; unchanged prompts must not rebuild it.");
      lease.Dispose();
      fixture.AssertGameplay(true);
      Assert.That(fixture.Submit.enabled, Is.False);
      Assert.That(fixture.Text.text, Is.EqualTo(before));
      Assert.That(fixture.RenderCalls, Is.Zero);
      Assert.That(fixture.Notifications, Is.Zero);
    }

    [Test]
    public void OverlappingMenuHostsRetainTheirSnapshotWithoutRepeatedPromptRefreshes()
    {
      using var fixture = new Fixture(true);
      using var owner = new MenuInputOwnership();
      using var first = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit });
      using var second = owner.Acquire(fixture.GameplayActions, new[] { fixture.Secondary });
      first.Dispose();
      fixture.AssertGameplay(false);
      Assert.That(fixture.Secondary.enabled, Is.True);
      second.Dispose();
      fixture.AssertGameplay(true);
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      Assert.That(fixture.RenderCalls, Is.Zero, "Nested ownership must not fan out the same unchanged glyph to every view.");
      Assert.That(fixture.Notifications, Is.Zero);
    }

    [Test]
    public void ActionMapEnableAndDisablePreserveTheSameBindingPrompt()
    {
      using var fixture = new Fixture(true);
      fixture.Gameplay.Disable();
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      fixture.Gameplay.Enable();
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      Assert.That(fixture.RenderCalls, Is.Zero, "Map routing changes have no effect on a resolved glyph.");
      Assert.That(fixture.Notifications, Is.Zero);
    }

    [Test]
    public void DeferredRestorationAfterSubmitDoesNotRebuildUnchangedPrompts()
    {
      using var fixture = new Fixture(true);
      using var owner = new MenuInputOwnership();
      using var lease = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit });
      fixture.ResetCounts();
      int submits = 0;
      fixture.Submit.performed += _ =>
      {
        submits++;
        lease.Dispose();
        fixture.AssertGameplay(false);
      };
      fixture.Press(Key.Enter);
      Assert.That(submits, Is.EqualTo(1));
      fixture.AssertGameplay(true);
      Assert.That(fixture.Submit.enabled, Is.False);
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      Assert.That(fixture.RenderCalls, Is.Zero, "The post-input restoration burst must not refresh every prompt once per action.");
      Assert.That(fixture.Notifications, Is.Zero);
    }

    [Test]
    public void RebindingAndCancelingAnOverrideRefreshDisabledActionPromptsSynchronously()
    {
      using var fixture = new Fixture(true);
      using var owner = new MenuInputOwnership();
      using var lease = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit });
      fixture.ResetCounts();
      var action = fixture.GameplayActions[0];
      action.ApplyBindingOverride(0, "<Keyboard>/q");
      Assert.That(action.enabled, Is.False);
      Assert.That(fixture.Text.text, Is.EqualTo("[Q]"));
      Assert.That(fixture.RenderCalls, Is.GreaterThan(0));
      fixture.ResetCounts();
      action.RemoveBindingOverride(0);
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      Assert.That(fixture.Notifications, Is.GreaterThan(0));
      Assert.That(action.enabled, Is.False);
    }

    [Test]
    public void BindingMaskChangesStillRefreshDisabledActionPromptsSynchronously()
    {
      using var fixture = new Fixture(true);
      using var owner = new MenuInputOwnership();
      using var lease = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit });
      fixture.ResetCounts();
      var action = fixture.GameplayActions[0];
      action.bindingMask = InputBinding.MaskByGroup("Pad");
      Assert.That(action.enabled, Is.False);
      Assert.That(fixture.Text.text, Is.EqualTo("Unbound"),
        "The active keyboard has no eligible binding after applying the controller-only mask.");
      Assert.That(fixture.Notifications, Is.GreaterThan(0));
      fixture.ResetCounts();
      action.bindingMask = null;
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      Assert.That(fixture.Notifications, Is.GreaterThan(0));
    }

    [Test]
    public void DeviceActivityAndExplicitContextStillRefreshWhileGameplayIsSuspended()
    {
      using var fixture = new Fixture(true);
      using var owner = new MenuInputOwnership();
      using var lease = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit });
      fixture.ResetCounts();
      fixture.PadState(new GamepadState().WithButton(GamepadButton.South));
      Assert.That(fixture.Service.Context.Device, Is.SameAs(fixture.Pad));
      Assert.That(fixture.Text.text, Is.EqualTo("[SOUTH]"));
      Assert.That(fixture.Notifications, Is.GreaterThan(0));
      fixture.ResetCounts();
      fixture.Service.SetContext(new InputPromptContext(device: fixture.Keyboard));
      Assert.That(fixture.Text.text, Is.EqualTo("[W]"));
      Assert.That(fixture.Notifications, Is.EqualTo(1));
      fixture.AssertGameplay(false);
      fixture.PadState(new GamepadState());
    }

    [Test]
    public void DisposedPromptServicesDoNotObserveLateLeaseRestoration()
    {
      using var fixture = new Fixture(true);
      using var owner = new MenuInputOwnership();
      using var lease = owner.Acquire(fixture.GameplayActions, new[] { fixture.Submit });
      fixture.Service.Dispose();
      fixture.ResetCounts();
      Assert.DoesNotThrow(lease.Dispose);
      fixture.AssertGameplay(true);
      Assert.That(fixture.RenderCalls, Is.Zero);
      Assert.That(fixture.Notifications, Is.Zero);
    }

    private sealed class Names : IInputPromptLocalizer
    {
      public string GetName(string key, string fallback) => fallback;
    }

    private sealed class Fixture : IDisposable
    {
      private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      private readonly Keyboard previousKeyboard = UnityEngine.InputSystem.Keyboard.current;
      private readonly Gamepad previousPad = Gamepad.current;
      private readonly InputSettings settings = InputSystem.settings;
      private readonly InputSettings.UpdateMode previousMode;
      private readonly bool previousFeature;
      public readonly InputActionAsset Asset = ScriptableObject.CreateInstance<InputActionAsset>();
      private readonly InputGlyphCatalog catalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
      private readonly InputPromptSettings promptSettings = ScriptableObject.CreateInstance<InputPromptSettings>();
      private readonly GameObject host = new("Menu prompt", typeof(RectTransform));
      private IDisposable binding, observation;
      public readonly List<InputAction> GameplayActions = new();
      public readonly Keyboard Keyboard;
      public readonly Gamepad Pad;
      public readonly InputActionMap Gameplay;
      public readonly InputAction Submit, Secondary, OriginallyDisabled;
      public readonly InputPromptService Service;
      public readonly TextMeshProUGUI Text;
      public int RenderCalls, Notifications;

      public Fixture(bool glyph)
      {
        previousMode = settings.updateMode;
        previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(settings, new object[] { PlayerUpdates });
        try
        {
          settings.SetInternalFeatureFlag(PlayerUpdates, true);
          settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
          Keyboard = InputSystem.AddDevice<Keyboard>(); Pad = InputSystem.AddDevice<Gamepad>();
          Asset.devices = new InputDevice[] { Keyboard, Pad };
          Gameplay = Asset.AddActionMap("Gameplay");
          for (int i = 0; i < 32; i++)
          {
            var action = Gameplay.AddAction("Gameplay " + i, InputActionType.Button);
            action.AddBinding("<Keyboard>/w", groups: "Desktop");
            action.AddBinding("<Gamepad>/buttonSouth", groups: "Pad");
            GameplayActions.Add(action);
          }
          OriginallyDisabled = Gameplay.AddAction("Inactive gameplay", InputActionType.Button, "<Keyboard>/z");
          GameplayActions.Add(OriginallyDisabled);
          var menu = Asset.AddActionMap("Menu");
          Submit = menu.AddAction("Submit", InputActionType.Button);
          Submit.AddBinding("<Keyboard>/enter", groups: "Desktop");
          Submit.AddBinding("<Gamepad>/buttonSouth", groups: "Pad");
          Secondary = menu.AddAction("Secondary", InputActionType.Button, "<Keyboard>/tab");
          for (int i = 0; i < 32; i++) GameplayActions[i].Enable();
          _ = Submit.controls.Count; _ = Secondary.controls.Count;
          catalog.SetEntries(
            new InputGlyphCatalog.Entry { Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "w", Symbol = "[W]", DisplayName = "Move W" },
            new InputGlyphCatalog.Entry { Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "q", Symbol = "[Q]", DisplayName = "Move Q" },
            new InputGlyphCatalog.Entry { Family = InputGlyphFamily.Generic, ControlPath = "buttonSouth", Symbol = "[SOUTH]", DisplayName = "South" });
          promptSettings.Configure(catalog);
          Service = new InputPromptService(promptSettings, actions: Asset, localizer: new Names());
          // Establish the real owning keyboard before counting invalidations, including for the deferred-submit test.
          Press(Key.W);
          Text = host.AddComponent<TextMeshProUGUI>();
          binding = Service.Bind(Text, () =>
          {
            RenderCalls++;
            return Service.Resolve(GameplayActions[0], glyph ? InputPromptStyle.Glyph : InputPromptStyle.LocalizedName);
          });
          observation = Service.Changed.Subscribe(_ => Notifications++);
          Assert.That(Text.text, Is.EqualTo(glyph ? "[W]" : "Move W"));
          ResetCounts();
        }
        catch { Dispose(); throw; }
      }

      public void AssertGameplay(bool restored)
      {
        for (int i = 0; i < 32; i++) Assert.That(GameplayActions[i].enabled, Is.EqualTo(restored));
        Assert.That(OriginallyDisabled.enabled, Is.False, "Lease restoration preserves a disabled sibling.");
      }
      public void ResetCounts() { RenderCalls = 0; Notifications = 0; }
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
          observation?.Dispose(); binding?.Dispose(); Service?.Dispose();
          Asset.Disable();
          if (Keyboard != null && Keyboard.added) InputSystem.RemoveDevice(Keyboard);
          if (Pad != null && Pad.added) InputSystem.RemoveDevice(Pad);
          Asset.Disable(); Object.DestroyImmediate(Asset);
          Object.DestroyImmediate(host); Object.DestroyImmediate(promptSettings); Object.DestroyImmediate(catalog);
        }
        finally
        {
          settings.SetInternalFeatureFlag(PlayerUpdates, previousFeature);
          settings.updateMode = previousMode;
          if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
          if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
        }
      }
    }
  }
}
