using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputPromptResolverTests
  {
    private readonly List<Object> owned = new();
    private InputGlyphCatalog catalog;
    private Names names;
    private InputPromptResolver resolver;

    [SetUp]
    public void SetUp()
    {
      catalog = Own(ScriptableObject.CreateInstance<InputGlyphCatalog>());
      names = new Names();
      resolver = new InputPromptResolver(catalog, names);
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
      owned.Clear();
    }

    [Test]
    public void DisabledAction_ResolvesPrintableCharacterWithoutEnablingIt()
    {
      using var action = new InputAction("Interact", binding: "<Keyboard>/e");
      Assert.That(resolver.Resolve(action, InputPromptStyle.Character), Is.EqualTo("E"));
      Assert.That(action.enabled, Is.False);
    }

    [Test]
    public void SpecialKey_UsesLocalizedNameAndSymbolAccordingToStyle()
    {
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", Symbol = "↵",
        LocalizationKey = "input.key.enter", DisplayName = "Enter"
      });
      names.Values["input.key.enter"] = "Entrée";
      using var action = new InputAction("Confirm", binding: "<Keyboard>/enter");
      Assert.That(resolver.Resolve(action, InputPromptStyle.Character), Is.EqualTo("Entrée"));
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName), Is.EqualTo("Entrée"));
      Assert.That(resolver.Resolve(action, InputPromptStyle.Glyph), Is.EqualTo("↵"));
      Assert.That(resolver.Resolve(action, InputPromptStyle.GlyphAndName), Is.EqualTo("↵ Entrée"));
      names.Values["input.key.enter"] = "Enter";
      Assert.That(resolver.Resolve(action, InputPromptStyle.GlyphAndName), Is.EqualTo("↵ Enter"));
    }

    [TestCase(InputGlyphFamily.Xbox, "A", "B", "X", "Y")]
    [TestCase(InputGlyphFamily.PlayStation, "Cross", "Circle", "Square", "Triangle")]
    [TestCase(InputGlyphFamily.Nintendo, "B", "A", "Y", "X")]
    [TestCase(InputGlyphFamily.Generic, "South", "East", "West", "North")]
    public void FaceButtons_PreserveLogicalControlForEachPresentationFamily(InputGlyphFamily family,
      string south, string east, string west, string north)
    {
      var context = new InputPromptContext(familyOverride: family);
      string[] paths = { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth" };
      string[] expected = { south, east, west, north };
      for (int index = 0; index < paths.Length; index++)
        Assert.That(resolver.ResolvePath("<Gamepad>/" + paths[index], InputPromptStyle.LocalizedName, context),
          Is.EqualTo(expected[index]), paths[index]);
    }

    [Test]
    public void FamilyOverride_ChangesPresentationWithoutSelectingAnotherBinding()
    {
      using var action = new InputAction("Interact");
      action.AddBinding("<Keyboard>/e", groups: "Keyboard");
      action.AddBinding("<Gamepad>/buttonNorth", groups: "Controller");
      Assert.That(resolver.Resolve(action, context: new InputPromptContext(bindingGroup: "Keyboard",
        familyOverride: InputGlyphFamily.PlayStation)), Is.EqualTo("E"));
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName,
        new InputPromptContext(bindingGroup: "Controller", familyOverride: InputGlyphFamily.PlayStation)), Is.EqualTo("Triangle"));
    }

    [Test]
    public void ExplicitGroups_AreMatchedAsTokensAndSelectedDeterministically()
    {
      using var action = new InputAction("Use");
      action.AddBinding("<Keyboard>/q", groups: "KeyboardAlt");
      action.AddBinding("<Keyboard>/e", groups: "Keyboard;Shared");
      action.AddBinding("<Keyboard>/f", groups: "Keyboard");
      Assert.That(resolver.Resolve(action, context: new InputPromptContext(bindingGroup: "Keyboard")), Is.EqualTo("E"));
      Assert.That(resolver.Resolve(action, context: new InputPromptContext(bindingGroup: "Keyboard", bindingIndex: 2)), Is.EqualTo("F"));
      Assert.That(resolver.Resolve(action, context: new InputPromptContext(bindingGroup: "Controller")), Is.EqualTo("Unbound"));
    }

    [Test]
    public void EmptyEffectiveOverride_IsUnboundAndDoesNotRevealAuthoredBinding()
    {
      using var action = new InputAction("Interact", binding: "<Keyboard>/e");
      action.ApplyBindingOverride(0, string.Empty);
      Assert.That(resolver.Resolve(action), Is.EqualTo("Unbound"));
      Assert.That(resolver.Resolve(null), Is.EqualTo("Unbound"));
      using var empty = new InputAction("Empty");
      Assert.That(resolver.Resolve(empty), Is.EqualTo("Unbound"));
    }

    [Test]
    public void BindingOverride_ResetAndConflictNotifications_UseCurrentEffectivePath()
    {
      using var map = new InputActionMap("Player");
      var action = map.AddAction("Interact", binding: "<Keyboard>/e");
      var other = map.AddAction("Other", binding: "<Keyboard>/q");
      using var rebinder = new InputRebinderService();
      rebinder.RegisterAction(action);
      rebinder.RegisterAction(other);
      action.ApplyBindingOverride(0, "<Keyboard>/f");
      Assert.That(resolver.Resolve(action), Is.EqualTo("F"));
      other.ApplyBindingOverride(0, "<Keyboard>/f");
      Assert.That(rebinder.CheckBindingConflict(action, 0, "<Keyboard>/f"), Is.True);
      rebinder.ResolveBindingConflict(other, 0);
      Assert.That(resolver.Resolve(other), Is.EqualTo("Q"));
      rebinder.ResetActionBindings(action);
      Assert.That(resolver.Resolve(action), Is.EqualTo("E"));
    }

    [TestCase("OneModifier", "LCtrl + E")]
    [TestCase("TwoModifiers", "LCtrl + LShift + E")]
    public void ModifierComposites_KeepChordTopology(string composite, string expected)
    {
      using var action = new InputAction("Chord");
      var syntax = action.AddCompositeBinding(composite);
      if (composite == "OneModifier") syntax.With("Modifier", "<Keyboard>/leftCtrl");
      else syntax.With("Modifier1", "<Keyboard>/leftCtrl").With("Modifier2", "<Keyboard>/leftShift");
      syntax.With("Binding", "<Keyboard>/e");
      Assert.That(resolver.Resolve(action), Is.EqualTo(expected));
    }

    [Test]
    public void DirectionComposite_PreservesDirectionLabels()
    {
      using var action = new InputAction("Move");
      action.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
        .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
      Assert.That(resolver.Resolve(action), Is.EqualTo("Up: W / Down: S / Left: A / Right: D"));
    }

    [Test]
    public void BindingMasks_ApplyAtActionMapAndAssetLevels()
    {
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      var map = asset.AddActionMap("Player");
      var action = map.AddAction("Use");
      action.AddBinding("<Keyboard>/e", groups: "Keyboard");
      action.AddBinding("<Gamepad>/buttonSouth", groups: "Controller");
      asset.bindingMask = InputBinding.MaskByGroup("Controller");
      // This test exercises masks, independently of a real controller's printed family.
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName,
        new InputPromptContext(familyOverride: InputGlyphFamily.Generic)), Is.EqualTo("South"));
      map.bindingMask = InputBinding.MaskByGroup("Keyboard");
      Assert.That(resolver.Resolve(action), Is.EqualTo("Unbound"));
      map.bindingMask = null;
      action.bindingMask = InputBinding.MaskByGroup("Keyboard");
      Assert.That(resolver.Resolve(action), Is.EqualTo("Unbound"));
    }

    [Test]
    public void DeviceSelection_AndPlayerDeviceRestrictions_DoNotCrossContexts()
    {
      var keyboard = InputSystem.AddDevice<Keyboard>();
      var firstPad = InputSystem.AddDevice<Gamepad>();
      var secondPad = InputSystem.AddDevice<Gamepad>();
      try
      {
        using var map = new InputActionMap("Player");
        var action = map.AddAction("Use");
        action.AddBinding("<Keyboard>/e");
        action.AddBinding("<Gamepad>/buttonNorth");
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: keyboard)), Is.EqualTo("E"));
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: firstPad,
          familyOverride: InputGlyphFamily.Xbox)), Is.EqualTo("Y"));
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: secondPad,
          familyOverride: InputGlyphFamily.Nintendo)), Is.EqualTo("X"));
        map.devices = new InputDevice[] { firstPad };
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: secondPad)), Is.EqualTo("Unbound"));
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: keyboard)), Is.EqualTo("Unbound"));
      }
      finally
      {
        InputSystem.RemoveDevice(secondPad);
        InputSystem.RemoveDevice(firstPad);
        InputSystem.RemoveDevice(keyboard);
      }
    }

    [Test]
    public void ExplicitKeyboard_DoesNotSelectABindingExclusiveToAnotherKeyboardLayout()
    {
      const string firstLayout = "MTKOwnedKeyboardA";
      const string secondLayout = "MTKOwnedKeyboardB";
      InputSystem.RegisterLayout("{\"name\":\"" + firstLayout + "\",\"extend\":\"Keyboard\"}");
      InputSystem.RegisterLayout("{\"name\":\"" + secondLayout + "\",\"extend\":\"Keyboard\"}");
      var first = InputSystem.AddDevice(firstLayout);
      var second = InputSystem.AddDevice(secondLayout);
      try
      {
        string path = "<" + secondLayout + ">/e";
        using var action = new InputAction("Use", binding: path);
        Assert.That(InputControlPath.TryFindControl(first, path), Is.Null, "The selected keyboard cannot execute this binding.");
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: first)), Is.EqualTo("Unbound"));
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: second)), Is.EqualTo("E"));
      }
      finally
      {
        InputSystem.RemoveDevice(second);
        InputSystem.RemoveDevice(first);
        InputSystem.RemoveLayout(secondLayout);
        InputSystem.RemoveLayout(firstLayout);
      }
    }

    [Test]
    public void KeyboardMouseChord_PermitsItsCompanionDeviceType()
    {
      var keyboard = InputSystem.AddDevice<Keyboard>();
      var mouse = InputSystem.AddDevice<Mouse>();
      try
      {
        using var action = new InputAction("Modified click");
        action.AddCompositeBinding("OneModifier").With("Modifier", "<Keyboard>/leftCtrl").With("Binding", "<Mouse>/leftButton");
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: keyboard)), Is.EqualTo("LCtrl + Left Click"));
        Assert.That(resolver.Resolve(action, context: new InputPromptContext(device: mouse)), Is.EqualTo("LCtrl + Left Click"));
      }
      finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); }
    }

    [Test]
    public void ControllerPresence_DoesNotOverrideAutomaticKeyboardSelection()
    {
      var pad = InputSystem.AddDevice<Gamepad>();
      try
      {
        using var action = new InputAction("Use");
        action.AddBinding("<Keyboard>/e");
        action.AddBinding("<Gamepad>/buttonSouth");
        Assert.That(resolver.Resolve(action), Is.EqualTo("E"));
      }
      finally { InputSystem.RemoveDevice(pad); }
    }

    [Test]
    public void IdleXboxCurrent_DoesNotSelectAControllerBindingOrCreateAnActivePlayerContext()
    {
      const string layout = "MTKIdlePresentationXbox";
      var previous = Gamepad.current;
      InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"XInputController\"}");
      var pad = (Gamepad)InputSystem.AddDevice(layout);
      try
      {
        pad.MakeCurrent();
        var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
        var action = asset.AddActionMap("Owned player").AddAction("Use");
        action.AddBinding("<Keyboard>/e");
        action.AddBinding("<Gamepad>/buttonSouth");
        using var service = new InputPromptService(actions: asset);
        Assert.That(Gamepad.current, Is.SameAs(pad));
        Assert.That(service.Context.Device, Is.Null, "Connecting an idle controller is not player activity.");
        Assert.That(resolver.Resolve(action), Is.EqualTo("E"));
        Assert.That(service.Resolve(action), Is.EqualTo("E"));
        InputSystem.RemoveDevice(pad);
        Assert.That(service.Context.Device, Is.Null);
        Assert.That(service.Resolve(action), Is.EqualTo("E"));
        Assert.That(action.enabled, Is.False);
      }
      finally
      {
        if (pad.added) InputSystem.RemoveDevice(pad);
        if (previous != null && previous.added) previous.MakeCurrent();
        InputSystem.RemoveLayout(layout);
      }
    }

    [Test]
    public void NamedSprite_IsEmittedOnlyWhenTheActualSpriteAssetContainsIt()
    {
      var spriteAsset = SpriteAsset();
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", SpriteAsset = spriteAsset,
        SpriteName = "keyboard-enter", DisplayName = "Enter"
      });
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName),
        Is.EqualTo("<sprite=\"MTK_InputGlyphs\" name=\"keyboard-enter\"> Enter"));
      Assert.That(resolver.SpriteAssets, Does.Contain(spriteAsset));
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", SpriteAsset = spriteAsset,
        SpriteName = "absent", DisplayName = "Enter"
      });
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
    }

    [Test]
    public void ImageConsumers_UseTheSameCatalogAndRetainNativeSpriteFallback()
    {
      var spriteAsset = SpriteAsset();
      var entry = new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", SpriteAsset = spriteAsset,
        SpriteName = "keyboard-enter"
      };
      catalog.SetEntries(entry);
      var nativeSprite = (spriteAsset.spriteCharacterTable[0].glyph as TMP_SpriteGlyph).sprite;
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "enter"), Is.SameAs(nativeSprite));
      var replacement = Own(Sprite.Create((Texture2D)spriteAsset.spriteSheet, new Rect(0, 0, 1, 1), Vector2.zero));
      entry.ImageSprite = replacement;
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "enter"), Is.SameAs(replacement));
      Assert.That(catalog.GetSprite(InputGlyphFamily.Xbox, "enter"), Is.Null);
    }

    [Test]
    public void CatalogChanges_NotifySubscribersAndUnsafeOrIncompleteAssetsFallBack()
    {
      int changes = 0;
      Action changed = () => changes++;
      catalog.Changed += changed;
      var spriteAsset = SpriteAsset();
      spriteAsset.name = "broken\"asset";
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", SpriteAsset = spriteAsset,
        SpriteName = "keyboard-enter", DisplayName = "Enter"
      });
      catalog.SetLayoutRules(new InputGlyphCatalog.LayoutRule { Layout = "Gamepad", Family = InputGlyphFamily.Generic });
      Assert.That(changes, Is.EqualTo(2));
      catalog.Changed -= changed;
      catalog.SetLayoutRules();
      Assert.That(changes, Is.EqualTo(2));
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
      spriteAsset.name = "MTK_InputGlyphs";
      spriteAsset.spriteSheet = null;
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
    }

    [Test]
    public void MissingFontSymbol_UsesReadableLocalizedName()
    {
      Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null, "The proving project must include TMP essential font assets.");
      var font = Own(Object.Instantiate(TMP_Settings.defaultFontAsset));
      // TMP_FontAsset owns/destroys these resources. A ScriptableObject clone still
      // shares persistent atlas/material references until explicitly detached.
      font.atlasTextures = Array.Empty<Texture2D>();
      font.material = new Material(font.material);
      font.atlasPopulationMode = AtlasPopulationMode.Static;
      font.characterTable.Clear();
      font.glyphTable.Clear();
      font.ReadFontAssetDefinition();
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", Symbol = "\ue001",
        LocalizationKey = "input.key.enter", DisplayName = "Enter"
      });
      names.Values["input.key.enter"] = "Entrée";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph,
        new InputPromptContext(null, null, null, null, font)), Is.EqualTo("Entrée"));
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName,
        new InputPromptContext(null, null, null, null, font)), Is.EqualTo("Entrée"));
    }

    [Test]
    public void ControlSchemeName_CanSelectADifferentBindingGroup()
    {
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      asset.AddControlScheme("KeyboardScheme").WithBindingGroup("KeyboardBindings");
      var action = asset.AddActionMap("Player").AddAction("Use");
      action.AddBinding("<Gamepad>/buttonSouth", groups: "Controller");
      action.AddBinding("<Keyboard>/e", groups: "KeyboardBindings");
      Assert.That(resolver.Resolve(action, context: new InputPromptContext(bindingGroup: "KeyboardScheme")), Is.EqualTo("E"));
    }

    [Test]
    public void UnsafeSpriteNamesAndExternalText_CannotInjectTmpTags()
    {
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", Symbol = "<sprite name=\"evil\">",
        DisplayName = "</noparse><size=200%>"
      });
      string actual = resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName);
      Assert.That(actual, Does.Not.Contain("<sprite name=\"evil\">"));
      Assert.That(actual, Does.Not.Contain("<size=200%>"));
      Assert.That(InputPromptRichText.Escape("<b>A</b>"), Is.EqualTo("<noparse><</noparse>b>A<noparse><</noparse>/b>"));
    }

    [Test]
    public void EncodedSpriteAssetName_UsesReadableFallback()
    {
      var asset = SpriteAsset();
      asset.name = @"encoded\u0022asset";
      catalog.SetEntries(new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter",
        SpriteAsset = asset, SpriteName = "keyboard-enter", DisplayName = "Enter"
      });
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"),
        "TMP decodes Unicode escapes before parsing tags, so encoded attribute delimiters are unsafe.");
    }

    [Test]
    public void UnknownDeviceAndMissingGlyph_HaveReadableFallbacks()
    {
      Assert.That(resolver.ResolvePath("<UnregisteredController>/strangeButton", InputPromptStyle.Glyph),
        Is.EqualTo("Strange Button"));
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
      Assert.That(resolver.ResolvePath("<Mouse>/rightButton", InputPromptStyle.LocalizedName), Is.EqualTo("Right Click"));
    }

    [Test]
    public void ExplicitCharacterStyle_PreservesNativeShortMouseLabels()
    {
      Assert.That(resolver.ResolvePath("<Mouse>/leftButton", InputPromptStyle.Character), Is.EqualTo("LMB"));
      Assert.That(resolver.ResolvePath("<Mouse>/rightButton", InputPromptStyle.Character), Is.EqualTo("RMB"));
      Assert.That(resolver.ResolvePath("<Mouse>/leftButton", InputPromptStyle.Auto), Is.EqualTo("Left Click"));
      Assert.That(resolver.ResolvePath("<Mouse>/leftButton", InputPromptStyle.LocalizedName), Is.EqualTo("Left Click"));
    }

    [TestCase("numpad0", "Numpad 0")]
    [TestCase("numpad9", "Numpad 9")]
    [TestCase("numpadDivide", "Numpad /")]
    [TestCase("numpadEquals", "Numpad =")]
    [TestCase("pause", "Pause/Break")]
    [TestCase("leftMeta", "Left System")]
    [TestCase("rightMeta", "Right System")]
    [TestCase("f1", "F1")]
    [TestCase("f24", "F24")]
    public void FullKeyboardNames_UseReadableNativeMetadataWithoutAConnectedDevice(string control, string expected)
    {
      string path = "<Keyboard>/" + control;
      Assert.That(resolver.ResolvePath(path, InputPromptStyle.LocalizedName), Is.EqualTo(expected));
      names.Values["input.key." + control] = "Localized " + control;
      Assert.That(resolver.ResolvePath(path, InputPromptStyle.LocalizedName), Is.EqualTo("Localized " + control),
        "Every dedicated key keeps its semantic Unity Localization path.");
    }

    [TestCase("oem1", "OEM1")]
    [TestCase("oem5", "OEM5")]
    public void KeyboardSemanticKeys_UseNativeCanonicalCasingWithAndWithoutADevice(string pathName, string canonical)
    {
      var previousKeyboard = Keyboard.current;
      var keyboard = InputSystem.AddDevice<Keyboard>();
      try
      {
        string path = "<Keyboard>/" + pathName;
        string expected = "Localized " + canonical;
        names.Values["input.key." + canonical] = expected;
        using var action = new InputAction("Canonical OEM prompt", binding: path);
        Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName, new InputPromptContext(device: keyboard)), Is.EqualTo(expected));
        Assert.That(resolver.ResolvePath(path, InputPromptStyle.LocalizedName), Is.EqualTo(expected),
          "Input System paths ignore case; semantic localization keys must still use the native canonical control name.");
        Assert.That(action.enabled, Is.False);
      }
      finally
      {
        InputSystem.RemoveDevice(keyboard);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      }
    }

    [TestCase(InputPromptStyle.Character, false, "Q")]
    [TestCase(InputPromptStyle.GlyphAndName, false, "Q")]
    [TestCase(InputPromptStyle.GlyphAndName, true, "[Q] Q")]
    public void AutomaticBinding_UsesNativeKeyboardDisplayNameWithoutAnActivityEvent(
      InputPromptStyle style, bool printedGlyphAuthored, string expected)
    {
      const string layout = "MTKPresentationKeyboard";
      InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"Keyboard\",\"controls\":[{\"name\":\"a\",\"displayName\":\"Q\"}]}");
      var keyboard = (Keyboard)InputSystem.AddDevice(layout);
      try
      {
        Assert.That(keyboard.aKey.displayName, Is.EqualTo("Q"), "The native control simulates a different OS keyboard layout.");
        using var action = new InputAction("Use", binding: "<" + layout + ">/a");
        var physicalA = new InputGlyphCatalog.Entry
        {
          Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "a", Symbol = "[A]",
          DisplayName = "A", LocalizationKey = "input.key.a"
        };
        names.Values["input.key.a"] = "A";
        names.Values["input.key.q"] = "Q";
        catalog.SetEntries(physicalA);
        if (printedGlyphAuthored)
          catalog.SetEntries(physicalA, new InputGlyphCatalog.Entry
          {
            Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "q", Symbol = "[Q]",
            DisplayName = "Q", LocalizationKey = "input.key.q"
          });
        Assert.That(resolver.Resolve(action, style), Is.EqualTo(expected),
          "A physical A binding must represent the printed Q, including its artwork and localized name.");
        Assert.That(action.enabled, Is.False);
      }
      finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveLayout(layout); }
    }

    [Test]
    public void AutomaticBinding_UsesConcreteGamepadLayoutFromItsActualBoundControl()
    {
      const string layout = "MTKPresentationPad";
      InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"XInputController\"}");
      var gamepad = InputSystem.AddDevice(layout);
      InputSystem.SetDeviceUsage(gamepad, "MTKPresentation");
      try
      {
        using var action = new InputAction("Use", binding: "<Gamepad>{MTKPresentation}/buttonSouth");
        Assert.That(action.controls.Count, Is.EqualTo(1));
        Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName), Is.EqualTo("A"));
      }
      finally { InputSystem.RemoveDevice(gamepad); InputSystem.RemoveLayout(layout); }
    }

    [Test]
    public void LayoutRules_ExtendDeviceFamilyWithoutChangingResolver()
    {
      InputSystem.RegisterLayout("{\"name\":\"MTKTestPad\",\"extend\":\"Gamepad\"}");
      try
      {
        catalog.SetLayoutRules(new InputGlyphCatalog.LayoutRule { Layout = "MTKTestPad", Family = InputGlyphFamily.Nintendo });
        Assert.That(resolver.ResolvePath("<MTKTestPad>/buttonSouth", InputPromptStyle.LocalizedName), Is.EqualTo("B"));
      }
      finally { InputSystem.RemoveLayout("MTKTestPad"); }
    }

    private TMP_SpriteAsset SpriteAsset()
    {
      var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
      asset.name = "MTK_InputGlyphs";
      var serialized = new SerializedObject(asset);
      serialized.FindProperty("m_Version").stringValue = "1.1.0";
      serialized.ApplyModifiedPropertiesWithoutUndo();
      var texture = Own(new Texture2D(2, 2));
      asset.spriteSheet = texture;
      asset.material = Own(new Material(Shader.Find("TextMeshPro/Sprite")));
      asset.material.mainTexture = texture;
      var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero));
      var glyph = new TMP_SpriteGlyph(0, new UnityEngine.TextCore.GlyphMetrics(2, 2, 0, 2, 2),
        new UnityEngine.TextCore.GlyphRect(0, 0, 2, 2), 1, 0, sprite);
      asset.spriteGlyphTable.Add(glyph);
      asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xfffe, glyph) { name = "keyboard-enter" });
      asset.UpdateLookupTables();
      return asset;
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private sealed class Names : IInputPromptLocalizer
    {
      public readonly Dictionary<string, string> Values = new();
      public string GetName(string key, string fallback) => Values.TryGetValue(key, out string value) ? value : fallback;
    }
  }
}
