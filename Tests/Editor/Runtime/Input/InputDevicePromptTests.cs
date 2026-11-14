using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputDevicePromptTests
  {
    private readonly List<Object> owned = new();
    private readonly List<InputDevice> devices = new();
    private readonly List<string> layouts = new();
    private InputGlyphCatalog catalog;
    private Names names;
    private InputPromptResolver resolver;
    private Gamepad previousGamepad;
    private Keyboard previousKeyboard;

    [SetUp]
    public void SetUp()
    {
      previousGamepad = Gamepad.current;
      previousKeyboard = Keyboard.current;
      catalog = Own(ScriptableObject.CreateInstance<InputGlyphCatalog>());
      names = new Names();
      resolver = new InputPromptResolver(catalog, names);
    }

    [TearDown]
    public void TearDown()
    {
      for (int index = devices.Count - 1; index >= 0; index--)
        if (devices[index].added) InputSystem.RemoveDevice(devices[index]);
      devices.Clear();
      for (int index = layouts.Count - 1; index >= 0; index--) InputSystem.RemoveLayout(layouts[index]);
      layouts.Clear();
      if (previousGamepad != null && previousGamepad.added) previousGamepad.MakeCurrent();
      if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      for (int index = owned.Count - 1; index >= 0; index--)
        if (owned[index] != null) Object.DestroyImmediate(owned[index]);
      owned.Clear();
    }

    [TestCase("XInputController", InputGlyphFamily.Xbox, "Xbox")]
    [TestCase("DualShockGamepad", InputGlyphFamily.PlayStation, "PlayStation")]
    [TestCase("DualSenseGamepadHID", InputGlyphFamily.PlayStation, "PlayStation")]
    [TestCase("SwitchProControllerHID", InputGlyphFamily.Nintendo, "Nintendo")]
    [TestCase("Gamepad", InputGlyphFamily.Generic, "Controller")]
    public void DeviceIdentityUsesItsActualNativeLayout(string layout, InputGlyphFamily family, string displayName)
    {
      var device = Add(layout);
      catalog.SetEntries(Device(layout, "[" + displayName + "]", displayName, family));
      var context = new InputPromptContext(device, familyOverride: InputGlyphFamily.Nintendo);
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.GlyphAndName, context),
        Is.EqualTo("[" + displayName + "] " + displayName));
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.Character, context), Is.EqualTo(displayName));
    }

    [Test]
    public void MoreSpecificDeviceLayoutWinsBeforePlatformAndAuthoredOrder()
    {
      string child = Register("SpecificPlayStation", "DualShockGamepad");
      var device = Add(child);
      var generic = Device("Gamepad", "[Generic]", "Controller");
      generic.Platform = InputGlyphPlatform.Windows;
      catalog.SetEntries(generic, Device("DualShockGamepad", "[PS]", "PlayStation"));
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.Glyph,
        new InputPromptContext(device).WithPlatform(InputGlyphPlatform.Windows)), Is.EqualTo("[PS]"));
      catalog.SetEntries(generic, Device("DualShockGamepad", "[PS]", "PlayStation"),
        Device(child, "[Exact]", "Specific controller"));
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.Glyph), Is.EqualTo("[Exact]"));
    }

    [Test]
    public void ExplicitNonGamepadLayoutCanHaveItsOwnIdentityWithoutChangingButtonFamily()
    {
      string source = Register("ProviderController", "InputDevice", true);
      string derived = Register("ProviderControllerChild", source);
      var device = Add(derived);
      catalog.SetEntries(Device("Gamepad", "[Controller]", "Controller"),
        Device(source, "[Provider]", "Provider controller"));
      Assert.That(device, Is.Not.InstanceOf<Gamepad>());
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.Glyph), Is.EqualTo("[Provider]"));
      Assert.That(InputGlyphCatalog.FamilyForLayout(device.layout), Is.EqualTo(InputGlyphFamily.Generic));
    }

    [Test]
    public void PlatformAndLocaleFallbacksApplyWithinTheSelectedLayout()
    {
      var device = Add("Gamepad");
      var neutral = Device("Gamepad", "[Any]", "Controller");
      var windows = Device("Gamepad", "[Windows]", "Controller");
      windows.Platform = InputGlyphPlatform.Windows;
      var french = Device("Gamepad", "[French]", "Manette");
      french.Platform = InputGlyphPlatform.Windows;
      french.LocaleCode = "fr";
      catalog.SetEntries(neutral, windows, french);
      names.LocaleCode = "fr-CA";
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.Glyph,
        new InputPromptContext(device).WithPlatform(InputGlyphPlatform.Windows)), Is.EqualTo("[French]"));
      Assert.That(resolver.ResolveDevice(device, InputPromptStyle.Glyph,
        new InputPromptContext(device).WithPlatform(InputGlyphPlatform.Linux)), Is.EqualTo("[Any]"));
    }

    [Test]
    public void DeviceRowsCannotReplaceBindingNamesSpritesOrGeneratedKeycaps()
    {
      var texture = Own(new Texture2D(2, 2));
      var image = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero));
      var device = Device("Keyboard", "[DEVICE]", "Wrong device label", InputGlyphFamily.KeyboardAndMouse);
      device.ControlPath = "enter";
      device.ImageSprite = image;
      var binding = new InputGlyphCatalog.Entry { ControlPath = "enter", DisplayName = "Enter", Symbol = "[ENTER]" };
      catalog.SetEntries(device, binding);
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "enter", out var selected), Is.True);
      Assert.That(selected, Is.SameAs(binding));
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "enter"), Is.Null);
      device.KeycapContent = InputKeycapContentMode.Sprite;
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      Assert.That(catalog.TryGetKeycapEntry("enter", null, "Enter", out _), Is.False);
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
    }

    [Test]
    public void BindingRowsCannotSupplyDeviceIdentity()
    {
      var pad = Add("Gamepad");
      catalog.SetEntries(new InputGlyphCatalog.Entry
        { Family = InputGlyphFamily.Generic, DeviceLayout = "Gamepad", Symbol = "[BUTTON]", DisplayName = "Wrong button" });
      Assert.That(catalog.TryGetDeviceEntry(pad.layout, null, InputGlyphPlatform.Any, out _), Is.False);
      Assert.That(resolver.ResolveDevice(pad, InputPromptStyle.Glyph), Is.EqualTo(InputPromptRichText.Escape(pad.displayName)));
    }

    [Test]
    public void DeviceGlyphsUseValidatedNativeTagsAndLocalizedExternalNames()
    {
      var pad = Add("Gamepad");
      var entry = Device("Gamepad", null, "Controller");
      entry.LocalizationKey = "input.device.controller";
      entry.SpriteAsset = SpriteAsset();
      entry.SpriteName = "controller";
      catalog.SetEntries(entry);
      names.Values[entry.LocalizationKey] = "Manette";
      string tag = "<sprite=\"" + entry.SpriteAsset.name + "\" name=\"controller\">";
      Assert.That(resolver.ResolveDevice(pad, InputPromptStyle.Glyph), Is.EqualTo(tag));
      Assert.That(resolver.ResolveDevice(pad, InputPromptStyle.GlyphAndName), Is.EqualTo(tag + " Manette"));
      Assert.That(resolver.ResolveDevice(pad, InputPromptStyle.LocalizedName), Is.EqualTo("Manette"));
      entry.SpriteName = "missing";
      Assert.That(resolver.ResolveDevice(pad, InputPromptStyle.Glyph), Is.EqualTo("Manette"));
      names.Values[entry.LocalizationKey] = "<b>Player</b>";
      Assert.That(resolver.ResolveDevice(pad, InputPromptStyle.GlyphAndName),
        Is.EqualTo(InputPromptRichText.Escape("<b>Player</b>")));
    }

    [Test]
    public void NullDisconnectedAndUnknownDevicesHaveReadableFallbacks()
    {
      var pen = Add("Pen");
      var pad = Add("Gamepad");
      catalog.SetEntries(Device("Gamepad", "[Controller]", "Controller"));
      Assert.That(resolver.ResolveDevice(pen), Is.EqualTo(InputPromptRichText.Escape(pen.displayName)));
      Assert.That(resolver.ResolveDevice(null), Is.EqualTo("No input device"));
      InputSystem.RemoveDevice(pad);
      Assert.That(resolver.ResolveDevice(pad), Is.EqualTo("No input device"));
    }

    [Test]
    public void OptionalServiceInterfacePreservesIndependentExplicitContexts()
    {
      var xbox = Add("XInputController");
      var playStation = Add("DualShockGamepad");
      catalog.SetEntries(Device("XInputController", "[Xbox]", "Xbox"), Device("DualShockGamepad", "[PS]", "PlayStation"));
      var settings = Own(ScriptableObject.CreateInstance<InputPromptSettings>());
      settings.Configure(catalog);
      using var first = new InputPromptService(settings);
      using var second = new InputPromptService(settings);
      IInputDevicePromptService firstDevices = first;
      IInputDevicePromptService secondDevices = second;
      first.SetContext(new InputPromptContext(xbox));
      second.SetContext(new InputPromptContext(playStation));
      Assert.That(firstDevices.ResolveDevice(), Is.EqualTo("[Xbox]"));
      Assert.That(secondDevices.ResolveDevice(), Is.EqualTo("[PS]"));
      Assert.That(firstDevices.ResolveDevice(context: new InputPromptContext(playStation)), Is.EqualTo("[PS]"));
      Assert.That(first.Context.Device, Is.SameAs(xbox));
    }

    [Test]
    public void BindingPreviewCannotReplaceTheActualActiveDeviceIdentity()
    {
      var pad = Add("XInputController");
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      var action = asset.AddActionMap("Player").AddAction("Confirm", binding: "<Gamepad>/buttonSouth");
      asset.devices = new[] { pad };
      _ = action.controls.Count;
      catalog.SetEntries(Device("XInputController", "[Xbox]", "Xbox"));
      var settings = Own(ScriptableObject.CreateInstance<InputPromptSettings>());
      settings.Configure(catalog, InputPromptStyle.Glyph, InputGlyphFamily.Nintendo, "Keyboard");
      using var service = new InputPromptService(settings, actions: asset);
      ProcessState(pad, new GamepadState().WithButton(GamepadButton.South));
      Assert.That(service.Context.Device, Is.Null, "Existing binding-preview selection remains independent.");
      Assert.That(((IInputDevicePromptService)service).ResolveDevice(), Is.EqualTo("[Xbox]"));
      Assert.That(action.enabled, Is.False, "Presentation must not enable an action to detect its bound device.");
    }

    [Test]
    public void ConnectingAnIdleControllerDoesNotSelectItForAutomaticDevicePresentation()
    {
      using var service = new InputPromptService();
      _ = Add("XInputController");
      Assert.That(service.Context.Device, Is.Null);
      Assert.That(((IInputDevicePromptService)service).ResolveDevice(), Is.EqualTo("No input device"));
    }

    [Test]
    public void BoundDevicePromptRefreshesInPlaceAndReleasesItsSubscription()
    {
      var xbox = Add("XInputController");
      var pad = Add("Gamepad");
      catalog.SetEntries(Device("XInputController", "[Xbox]", "Xbox"), Device("Gamepad", "[Controller]", "Controller"));
      var settings = Own(ScriptableObject.CreateInstance<InputPromptSettings>());
      settings.Configure(catalog);
      using var service = new InputPromptService(settings);
      var host = Own(new GameObject("Device identity", typeof(RectTransform)));
      var text = host.AddComponent<TextMeshProUGUI>();
      service.SetContext(new InputPromptContext(xbox));
      var binding = service.Bind(text, () => ((IInputDevicePromptService)service).ResolveDevice());
      try
      {
        Assert.That(text.text, Is.EqualTo("[Xbox]"));
        service.SetContext(new InputPromptContext(pad));
        Assert.That(text.text, Is.EqualTo("[Controller]"));
        InputSystem.RemoveDevice(pad);
        Assert.That(text.text, Is.EqualTo("No input device"));
        binding.Dispose();
        service.SetContext(new InputPromptContext(xbox));
        Assert.That(text.text, Is.EqualTo("No input device"));
        Assert.That(host.activeSelf, Is.True);
      }
      finally { binding.Dispose(); }
    }

    private InputDevice Add(string layout)
    {
      var device = InputSystem.AddDevice(layout);
      devices.Add(device);
      return device;
    }

    private string Register(string suffix, string parent, bool button = false)
    {
      string layout = "MTKDevicePrompt" + suffix + Guid.NewGuid().ToString("N");
      if (button) InputSystem.RegisterLayout<ProviderDevice>(layout);
      else InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"" + parent + "\"}");
      layouts.Add(layout);
      return layout;
    }

    private static InputGlyphCatalog.Entry Device(string layout, string glyph, string displayName,
      InputGlyphFamily family = InputGlyphFamily.Generic)
      => new() { Kind = InputGlyphEntryKind.Device, DeviceLayout = layout, Family = family,
        Symbol = glyph, DisplayName = displayName };

    private TMP_SpriteAsset SpriteAsset()
    {
      var texture = Own(new Texture2D(2, 2));
      var material = Own(new Material(Shader.Find("TextMeshPro/Sprite")) { mainTexture = texture });
      var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
      asset.name = "MTK_Device_" + Guid.NewGuid().ToString("N");
      var serialized = new SerializedObject(asset);
      serialized.FindProperty("m_Version").stringValue = "1.1.0";
      serialized.ApplyModifiedPropertiesWithoutUndo();
      asset.spriteSheet = texture;
      asset.material = material;
      var glyph = new TMP_SpriteGlyph { index = 0,
        metrics = new UnityEngine.TextCore.GlyphMetrics(2, 2, 0, 2, 2),
        glyphRect = new UnityEngine.TextCore.GlyphRect(0, 0, 2, 2) };
      asset.spriteGlyphTable.Add(glyph);
      asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xfffe, glyph) { name = "controller" });
      asset.UpdateLookupTables();
      return asset;
    }

    private static void ProcessState(InputDevice device, GamepadState state)
    {
      const string feature = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      var settings = InputSystem.settings;
      var getter = typeof(InputSettings).GetMethod("IsFeatureEnabled",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
      bool previousFeature = (bool)getter.Invoke(settings, new object[] { feature });
      var previousMode = settings.updateMode;
      try
      {
        settings.SetInternalFeatureFlag(feature, true);
        settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.QueueStateEvent(device, state);
        InputSystem.Update();
      }
      finally
      {
        settings.updateMode = previousMode;
        settings.SetInternalFeatureFlag(feature, previousFeature);
      }
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    private sealed class Names : IInputPromptLocalizer, IInputPromptLocaleProvider
    {
      public readonly Dictionary<string, string> Values = new();
      public string LocaleCode { get; set; }
      public string GetName(string key, string fallback) => Values.TryGetValue(key, out string value) ? value : fallback;
    }

    [InputControlLayout]
    public sealed class ProviderDevice : InputDevice
    {
      [InputControl(layout = "Button")]
      public ButtonControl confirm { get; private set; }
      protected override void FinishSetup()
      {
        confirm = GetChildControl<ButtonControl>("confirm");
        base.FinishSetup();
      }
    }
  }
}
