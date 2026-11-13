using System;
using System.Runtime.CompilerServices;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputPromptServiceTests
  {
    [Test]
    public void DisposingBindingReleasesItsRetiredForeignAction()
    {
      using var service = new InputPromptService();
      var retired = BindAndRetireAction(service);
      GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
      Assert.That(retired.IsAlive, Is.False, "A global prompt service must not retain every retired UI action.");
      GC.KeepAlive(service);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference BindAndRetireAction(InputPromptService service)
    {
      var action = new InputAction("Temporary prompt", binding: "<Keyboard>/e");
      var host = new GameObject("Temporary binding", typeof(RectTransform));
      var text = host.AddComponent<TextMeshProUGUI>();
      var binding = service.Bind(text, () => service.Resolve(action));
      binding.Dispose();
      Object.DestroyImmediate(host);
      action.Dispose();
      return new WeakReference(action);
    }

    [Test]
    public void ExplicitPathContextNotifiesWhenItsControllerDisconnects()
    {
      var previous = Gamepad.current;
      var pad = InputSystem.AddDevice<Gamepad>();
      using var service = new InputPromptService();
      service.SetContext(new InputPromptContext(device: pad));
      int updates = 0;
      using var observation = service.Changed.Subscribe(_ => updates++);
      try
      {
        InputSystem.RemoveDevice(pad);
        Assert.That(updates, Is.GreaterThan(0), "Path-only prompts have no action event to compensate for missing device notifications.");
      }
      finally
      {
        if (pad.added) InputSystem.RemoveDevice(pad);
        if (previous != null && previous.added) previous.MakeCurrent();
      }
    }

    [Test]
    public void NativeSpriteNameCollisionFallsBackToReadableName()
    {
      string name = "InputGlyphCollision_" + Guid.NewGuid().ToString("N");
      var shader = Shader.Find("TextMeshPro/Sprite");
      var first = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
      var second = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
      var catalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
      var settings = ScriptableObject.CreateInstance<InputPromptSettings>();
      var firstMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };
      var secondMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };
      foreach (var asset in new[] { first, second })
      {
        asset.name = name;
        var serialized = new UnityEditor.SerializedObject(asset);
        serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.spriteSheet = Texture2D.whiteTexture;
        asset.material = asset == first ? firstMaterial : secondMaterial;
        var glyph = new TMP_SpriteGlyph { index = 0 };
        asset.spriteGlyphTable.Add(glyph);
        asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xfffe, glyph) { name = "keyboard-enter" });
        asset.UpdateLookupTables();
      }
      MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetHashCode(name), first);
      catalog.SetEntries(new InputGlyphCatalog.Entry { Family = InputGlyphFamily.KeyboardAndMouse,
        ControlPath = "enter", SpriteAsset = second, SpriteName = "keyboard-enter", DisplayName = "Enter" });
      settings.Configure(catalog);
      try
      {
        using var service = new InputPromptService(settings);
        Assert.That(service.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName), Is.EqualTo("Enter"),
          "TMP keeps the first asset for a native name; emitting the second one's tag would render the wrong atlas.");
      }
      finally
      {
        Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        Object.DestroyImmediate(firstMaterial); Object.DestroyImmediate(secondMaterial);
        Object.DestroyImmediate(catalog); Object.DestroyImmediate(settings);
      }
    }

    [Test]
    public void InputServiceActionSourceMapsOnlyItsAuthoredActionsToItsManagedClone()
    {
      var authored = ScriptableObject.CreateInstance<InputActionAsset>();
      var action = authored.AddActionMap("Player").AddAction("Interact", binding: "<Keyboard>/e");
      using var input = new MToolKit.Runtime.Input.InputService();
      try
      {
        input.Initialize(authored);
        var source = input as IInputActionSource;
        Assert.That(source, Is.Not.Null, "Presentation needs the InputService-owned runtime instance.");
        Assert.That(source.RuntimeActions, Is.Not.SameAs(authored));
        Assert.That(source.ResolveRuntimeAction(action), Is.SameAs(source.RuntimeActions.FindAction(action.id)));
        using var unrelated = new InputAction("Other", binding: "<Keyboard>/q");
        Assert.That(source.ResolveRuntimeAction(unrelated), Is.SameAs(unrelated));
        Assert.That(source.ResolveRuntimeAction(source.RuntimeActions.FindAction(action.id)),
          Is.SameAs(source.RuntimeActions.FindAction(action.id)));
      }
      finally { Object.DestroyImmediate(authored); }
    }

    [Test]
    public void ActualInputSwitchesDeviceOnlyWithinTheRelevantPlayerAsset()
    {
      var asset = ScriptableObject.CreateInstance<InputActionAsset>();
      var otherAsset = ScriptableObject.CreateInstance<InputActionAsset>();
      var action = asset.AddActionMap("Player").AddAction("Interact", InputActionType.Button);
      action.AddBinding("<Keyboard>/e");
      action.AddBinding("<Gamepad>/buttonSouth");
      var otherAction = otherAsset.AddActionMap("Player").AddAction("Interact", InputActionType.Button,
        "<Gamepad>/buttonSouth");
      var previousKeyboard = Keyboard.current;
      var previousGamepad = Gamepad.current;
      var keyboard = InputSystem.AddDevice<Keyboard>();
      var gamepad = InputSystem.AddDevice<Gamepad>();
      var previousMode = InputSystem.settings.updateMode;
      const string playerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      var feature = typeof(InputSettings).GetMethod("IsFeatureEnabled",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
      bool previousFeature = (bool)feature.Invoke(InputSystem.settings, new object[] { playerUpdates });
      using var first = new InputPromptService(actions: asset);
      using var second = new InputPromptService(actions: otherAsset);
      try
      {
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings.SetInternalFeatureFlag(playerUpdates, true);
        asset.devices = new InputDevice[] { keyboard, gamepad };
        otherAsset.devices = new InputDevice[] { gamepad };
        action.Enable(); otherAction.Enable();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
        Assert.That(first.Context.Device, Is.SameAs(keyboard));
        Assert.That(second.Context.Device, Is.Null, "Another player's keyboard must not change this context.");
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); InputSystem.Update();
        Assert.That(first.Context.Device, Is.SameAs(gamepad));
        Assert.That(first.Resolve(action), Is.EqualTo("South"));
        InputSystem.RemoveDevice(gamepad);
        Assert.That(first.Context.Device, Is.Not.SameAs(gamepad));
        Assert.That(first.Resolve(action), Is.EqualTo("E"));
      }
      finally
      {
        action.Disable(); otherAction.Disable();
        InputSystem.settings.SetInternalFeatureFlag(playerUpdates, previousFeature);
        InputSystem.settings.updateMode = previousMode;
        if (keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (gamepad.added) InputSystem.RemoveDevice(gamepad);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
        Object.DestroyImmediate(asset); Object.DestroyImmediate(otherAsset);
      }
    }

    [Test]
    public void BindingOverrideResetAndContextRefreshExistingTextWithoutReactivation()
    {
      using var action = new InputAction("Interact", binding: "<Keyboard>/e");
      var host = new GameObject("Live prompt", typeof(RectTransform));
      var text = host.AddComponent<TextMeshProUGUI>();
      using var service = new InputPromptService();
      try
      {
        using var binding = service.Bind(text, () => service.Resolve(action));
        Assert.That(text.text, Is.EqualTo("E"));
        action.ApplyBindingOverride(0, "<Keyboard>/q");
        Assert.That(text.text, Is.EqualTo("Q"));
        action.RemoveBindingOverride(0);
        Assert.That(text.text, Is.EqualTo("E"));
        Assert.That(host.activeSelf, Is.True);
        Assert.That(action.enabled, Is.False);
      }
      finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void SettingsChangesNotifyAndDisposedServiceStopsObservingThem()
    {
      var settings = ScriptableObject.CreateInstance<InputPromptSettings>();
      var service = new InputPromptService(settings);
      int updates = 0;
      using var observation = service.Changed.Subscribe(_ => updates++);
      try
      {
        settings.Configure(null, InputPromptStyle.GlyphAndName, InputGlyphFamily.PlayStation, "Controller");
        Assert.That(updates, Is.EqualTo(1));
        Assert.That(service.Context.BindingGroup, Is.EqualTo("Controller"));
        Assert.That(service.Context.FamilyOverride, Is.EqualTo(InputGlyphFamily.PlayStation));
        service.Dispose();
        settings.NotifyChanged();
        Assert.That(updates, Is.EqualTo(1));
      }
      finally { service.Dispose(); Object.DestroyImmediate(settings); }
    }

    [Test]
    public void IndependentContextsAndNamedPlainTextAreSafe()
    {
      using var action = new InputAction("Confirm");
      action.AddBinding("<Keyboard>/enter", groups: "Keyboard");
      action.AddBinding("<Gamepad>/buttonSouth", groups: "Controller");
      using var first = new InputPromptService();
      using var second = new InputPromptService();
      first.SetContext(new InputPromptContext(bindingGroup: "Keyboard"));
      second.SetContext(new InputPromptContext(bindingGroup: "Controller", familyOverride: InputGlyphFamily.PlayStation));
      Assert.That(first.Resolve(action), Is.EqualTo("Enter"));
      Assert.That(second.Resolve(action), Is.EqualTo("Cross"));
      var composed = first.Compose("missing.test", "{confirm} to confirm {name}",
        new InputPromptArgument("confirm", action), InputPromptArgument.PlainText("name", "<b>Player</b>"));
      Assert.That(composed, Does.StartWith("Enter to confirm "));
      Assert.That(composed, Does.Not.Contain("<b>"));
    }

    [Test]
    public void DisposedBindingDoesNotUpdateItsText()
    {
      using var action = new InputAction("Interact", binding: "<Keyboard>/e");
      using var service = new InputPromptService();
      var host = new GameObject("Disposed binding", typeof(RectTransform));
      var text = host.AddComponent<TextMeshProUGUI>();
      try
      {
        var binding = service.Bind(text, () => service.Resolve(action));
        Assert.That(text.text, Is.EqualTo("E"));
        binding.Dispose();
        action.ApplyBindingOverride(0, "<Keyboard>/q");
        Assert.That(text.text, Is.EqualTo("E"));
      }
      finally { Object.DestroyImmediate(host); }
    }
  }
}
