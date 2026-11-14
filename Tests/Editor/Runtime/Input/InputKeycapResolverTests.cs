using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputKeycapResolverTests
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

    [TestCase(InputPromptStyle.Auto)]
    [TestCase(InputPromptStyle.Glyph)]
    [TestCase(InputPromptStyle.GlyphAndName)]
    public void GeneratedPrintableCap_UsesNativeSpriteAndPreservesTheRequestedName(InputPromptStyle style)
    {
      var cap = Cap("e", "E");
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      using var action = new InputAction("Interact", binding: "<Keyboard>/e");
      Assert.That(resolver.Resolve(action, style),
        Is.EqualTo(Tag(cap) + (style == InputPromptStyle.GlyphAndName ? " E" : string.Empty)));
      Assert.That(action.enabled, Is.False, "Presentation must not enable the execution action.");
      Assert.That(resolver.Resolve(action, InputPromptStyle.Character), Is.EqualTo("E"));
      Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName), Is.EqualTo("E"));
    }

    [Test]
    public void AuthoredModeAndLegacyLookup_IgnoreLocalizedAndGeneratedRows()
    {
      var generated = Cap("enter", "Enter");
      var french = new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", LocaleCode = "fr", Symbol = "[FR]"
      };
      var authored = new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", Symbol = "[Return]",
        ImageSprite = generated.ImageSprite
      };
      catalog.SetEntries(generated, french, authored);
      Assert.That(catalog.KeyboardGlyphMode, Is.EqualTo(InputKeyboardGlyphMode.Authored));
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "enter", out var legacy), Is.True);
      Assert.That(legacy, Is.SameAs(authored));
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "enter"), Is.SameAs(authored.ImageSprite));
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("[Return]"));
    }

    [Test]
    public void LocaleSelection_PrefersExactThenParentThenNeutralAndRequiresMatchingBakedText()
    {
      var neutral = Cap("enter", "Enter");
      var french = Cap("enter", "Entrée", "fr");
      var canadian = Cap("enter", "Entrée", "fr-CA");
      catalog.SetEntries(neutral, french, canadian);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.Values["input.key.enter"] = "Entrée";
      names.LocaleCode = "fr-CA";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter"), Is.EqualTo(Tag(canadian)));
      names.LocaleCode = "fr-BE";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter"), Is.EqualTo(Tag(french)));
      names.LocaleCode = "de";
      names.Values["input.key.enter"] = "Eingabe";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter"), Is.EqualTo("Eingabe"));
      names.Values["input.key.enter"] = "Enter";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter"), Is.EqualTo(Tag(neutral)));
    }

    [Test]
    public void LocaleLookup_HandlesScriptParentsAndIgnoresWrongLanguageEvenWhenTextMatches()
    {
      var wrongLanguage = Cap("e", "E", "en");
      var script = Cap("e", "E", "zh-Hant");
      var language = Cap("e", "E", "zh");
      catalog.SetEntries(wrongLanguage, language, script);
      Assert.That(catalog.TryGetKeycapEntry("e", "ZH_hant_TW", "E", out var match), Is.True);
      Assert.That(match, Is.SameAs(script));
      Assert.That(catalog.TryGetKeycapEntry("e", "de", "E", out _), Is.False);
      Assert.That(catalog.TryGetKeycapEntry("e", null, "E", out _), Is.False);
    }

    [Test]
    public void SeparateLocaleAssets_ResolveAlongsideSharedNeutralCaps()
    {
      var english = Cap("enter", "Enter", "en");
      var french = Cap("enter", "Entrée", "fr");
      var japanese = Cap("enter", "決定", "ja");
      var neutral = Cap("e", "E");
      catalog.SetEntries(english, french, japanese, neutral);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      Assert.That(resolver.SpriteAssets.Count, Is.EqualTo(4));
      foreach (var pair in new[] { ("en", "Enter", english), ("fr", "Entrée", french), ("ja", "決定", japanese) })
      {
        names.LocaleCode = pair.Item1;
        names.Values["input.key.enter"] = pair.Item2;
        Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName),
          Is.EqualTo(Tag(pair.Item3) + " " + pair.Item2));
        Assert.That(resolver.ResolvePath("<Keyboard>/e", InputPromptStyle.Glyph), Is.EqualTo(Tag(neutral)));
      }
      names.LocaleCode = "de";
      names.Values["input.key.enter"] = "Eingabe";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName), Is.EqualTo("Eingabe"));
    }

    [Test]
    public void LocalizedAuthoredLookup_IsAdditiveAndDoesNotExposeGeneratedCaps()
    {
      var french = new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", LocaleCode = "fr", Symbol = "[Entrée]"
      };
      var neutral = new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "enter", Symbol = "[Return]"
      };
      catalog.SetEntries(Cap("enter", "Entrée", "fr-CA"), neutral, french);
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "enter", "fr-CA", out var localized), Is.True);
      Assert.That(localized, Is.SameAs(french));
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "enter", "de", out localized), Is.True);
      Assert.That(localized, Is.SameAs(neutral));
    }

    [Test]
    public void StaleTranslationOrMissingNativeSprite_FallsBackToEscapedReadableLabel()
    {
      var stale = Cap("enter", "Enter", "fr");
      var missing = Cap("enter", "<Entrée>", "fr");
      missing.SpriteName = "missing-native-character";
      catalog.SetEntries(stale, missing);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = "fr";
      names.Values["input.key.enter"] = "<Entrée>";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName),
        Is.EqualTo(InputPromptRichText.Escape("<Entrée>")));
    }

    [TestCase("<b>Entrée</b>")]
    [TestCase(@"\u003C")]
    public void BakedLiteralLabel_IsMatchedBeforeRichTextEscaping(string literalLabel)
    {
      var cap = Cap("enter", literalLabel, "fr");
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = "fr";
      names.Values["input.key.enter"] = literalLabel;
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName),
        Is.EqualTo(Tag(cap) + " " + InputPromptRichText.Escape(literalLabel)));
      cap.BakedLabel = "different label";
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.GlyphAndName),
        Is.EqualTo(InputPromptRichText.Escape(literalLabel)));
    }

    [Test]
    public void BackslashCharacter_UsesItsLiteralBakedCap()
    {
      var cap = Cap("backslash", "\\");
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      Assert.That(resolver.ResolvePath("<Keyboard>/backslash"), Is.EqualTo(Tag(cap)));
    }

    [TestCase("leftCtrl", "LCtrl")]
    [TestCase("rightCtrl", "RCtrl")]
    [TestCase("leftShift", "LShift")]
    [TestCase("rightShift", "RShift")]
    [TestCase("leftAlt", "LAlt")]
    [TestCase("rightAlt", "RAlt")]
    public void ModifierNames_RetainCompactSideIdentity(string control, string expected)
    {
      Assert.That(resolver.ResolvePath("<Keyboard>/" + control, InputPromptStyle.LocalizedName), Is.EqualTo(expected));
      Assert.That(resolver.ResolvePath("<Keyboard>/" + control, InputPromptStyle.Character), Is.EqualTo(expected));
    }

    [Test]
    public void NeutralSpriteCap_ReusesItsArtworkAndKeepsLocalizedNamesOutsideTheCap()
    {
      var cap = Cap("leftShift", null);
      cap.KeycapContent = InputKeycapContentMode.Sprite;
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = "fr";
      names.Values["input.key.leftShift"] = "Maj G";
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.GlyphAndName), Is.EqualTo(Tag(cap) + " Maj G"));
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.Glyph), Is.EqualTo(Tag(cap)));
      names.LocaleCode = "en";
      names.Values["input.key.leftShift"] = "LShift";
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.GlyphAndName), Is.EqualTo(Tag(cap) + " LShift"));
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.Auto), Is.EqualTo(Tag(cap)));
    }

    [Test]
    public void GeneratedSpriteCap_DoesNotReplaceLegacyAuthoredGlyphsOrImageReferences()
    {
      var cap = Cap("enter", null);
      cap.KeycapContent = InputKeycapContentMode.Sprite;
      catalog.SetEntries(cap);
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "enter", out _), Is.False);
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "enter"), Is.Null);
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo(Tag(cap)));
    }

    [TestCase("en", "LShift")]
    [TestCase("fr", "Maj G")]
    public void SpriteAndLabelCap_UsesItsLocalizedBakedTextAndKeepsCallerStyleSemantics(string locale, string label)
    {
      var english = Cap("leftShift", "LShift", "en");
      var french = Cap("leftShift", "Maj G", "fr");
      english.KeycapContent = french.KeycapContent = InputKeycapContentMode.SpriteAndLabel;
      catalog.SetEntries(english, french);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = locale;
      names.Values["input.key.leftShift"] = label;
      var expected = locale == "en" ? english : french;
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.Glyph), Is.EqualTo(Tag(expected)),
        "The icon and localized label are already inside this one native sprite.");
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.GlyphAndName),
        Is.EqualTo(Tag(expected) + " " + label), "The explicitly requested external name remains part of GlyphAndName.");
    }

    [Test]
    public void SpriteAndLabelCap_StaleTextFallsBackAndLegacyLookupsStayAuthored()
    {
      var cap = Cap("leftShift", "Old label", "fr");
      cap.KeycapContent = InputKeycapContentMode.SpriteAndLabel;
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = "fr";
      names.Values["input.key.leftShift"] = "Maj G";
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.Glyph), Is.EqualTo("Maj G"));
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "leftShift", "fr", out _), Is.False);
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "leftShift"), Is.Null);
      cap.BakedLabel = "Maj G";
      Assert.That(resolver.ResolvePath("<Keyboard>/leftShift", InputPromptStyle.Glyph), Is.EqualTo(Tag(cap)));
    }

    [TestCase("numpad0", "0", "Pavé 0")]
    [TestCase("numpadPlus", "+", "Pavé +")]
    [TestCase("numpadPeriod", ".", "Pavé .")]
    public void NativeNumpadCharacter_PreservesDedicatedLocalizationAndKeycapIdentity(string control, string printed, string localized)
    {
      const string layout = "MTKDedicatedNumpadKeyboard";
      var previousKeyboard = Keyboard.current;
      InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"Keyboard\",\"controls\":[{\"name\":\""
        + control + "\",\"displayName\":\"" + printed + "\"}]}");
      var keyboard = InputSystem.AddDevice(layout);
      try
      {
        var cap = Cap(control, localized, "fr");
        catalog.SetEntries(cap);
        catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
        names.LocaleCode = "fr";
        names.Values["input.key." + control] = localized;
        using var action = new InputAction("Dedicated numpad prompt", binding: "<" + layout + ">/" + control);
        Assert.That(keyboard[control].displayName, Is.EqualTo(printed));
        Assert.That(resolver.Resolve(action, InputPromptStyle.LocalizedName), Is.EqualTo(localized),
          "A native printed character must not turn a dedicated numpad binding into a main-row semantic key.");
        Assert.That(resolver.Resolve(action, InputPromptStyle.Glyph), Is.EqualTo(Tag(cap)));
        Assert.That(resolver.ResolvePath("<Keyboard>/" + control, InputPromptStyle.Glyph), Is.EqualTo(Tag(cap)));
        Assert.That(action.enabled, Is.False);
      }
      finally
      {
        InputSystem.RemoveDevice(keyboard);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        InputSystem.RemoveLayout(layout);
      }
    }

    [TestCase(InputKeycapContentMode.Label)]
    [TestCase(InputKeycapContentMode.SpriteAndLabel)]
    public void ConfiguredKeycapFallback_IsUsedWhenTranslationIsMissingAndRejectsStaleLabels(InputKeycapContentMode content)
    {
      var cap = Cap("numpad8", "Num 8", "fr");
      cap.KeycapContent = content;
      cap.DisplayName = "Num 8";
      cap.LocalizationKey = "input.key.numpad8";
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = "fr";
      Assert.That(resolver.ResolvePath("<Keyboard>/numpad8", InputPromptStyle.LocalizedName), Is.EqualTo("Num 8"));
      Assert.That(resolver.ResolvePath("<Keyboard>/numpad8", InputPromptStyle.Glyph), Is.EqualTo(Tag(cap)));
      Assert.That(resolver.ResolvePath("<Keyboard>/numpad8", InputPromptStyle.GlyphAndName), Is.EqualTo(Tag(cap) + " Num 8"));
      names.Values["input.key.numpad8"] = "Pavé 8";
      Assert.That(resolver.ResolvePath("<Keyboard>/numpad8", InputPromptStyle.Glyph), Is.EqualTo("Pavé 8"),
        "A new translation must invalidate the previously baked fallback label.");
    }

    [Test]
    public void ConfiguredKeycapDefinition_RespectsLocaleAndKeepsLegacyAuthoredLookupsSeparate()
    {
      var cap = Cap("numpadEnter", "Num Enter", "fr");
      cap.DisplayName = "Num Enter";
      cap.LocalizationKey = "project.key.numpadEnter";
      catalog.SetEntries(cap);
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      names.LocaleCode = "fr-CA";
      Assert.That(resolver.ResolvePath("<Keyboard>/numpadEnter", InputPromptStyle.LocalizedName), Is.EqualTo("Num Enter"));
      Assert.That(catalog.TryGetEntry(InputGlyphFamily.KeyboardAndMouse, "numpadEnter", "fr-CA", out _), Is.False);
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "numpadEnter"), Is.Null);
      names.Values["project.key.numpadEnter"] = "Entrée Num";
      Assert.That(resolver.ResolvePath("<Keyboard>/numpadEnter", InputPromptStyle.LocalizedName), Is.EqualTo("Entrée Num"));
      names.LocaleCode = "de";
      Assert.That(resolver.ResolvePath("<Keyboard>/numpadEnter", InputPromptStyle.LocalizedName), Is.EqualTo("Numpad Enter"));
      names.LocaleCode = "fr";
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.Authored);
      Assert.That(resolver.ResolvePath("<Keyboard>/numpadEnter", InputPromptStyle.LocalizedName), Is.EqualTo("Numpad Enter"));
    }

    [Test]
    public void GeneratedMode_LeavesControllerAndMouseArtworkUnchanged()
    {
      var cap = Cap("e", "E");
      catalog.SetEntries(cap, new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.Xbox, ControlPath = "buttonSouth", Symbol = "[A]"
      }, new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = "leftButton", Symbol = "[Mouse]"
      }, Cap("leftButton", "Left Click"));
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.GlyphAndName,
        new InputPromptContext(familyOverride: InputGlyphFamily.Xbox)), Is.EqualTo("[A] A"));
      Assert.That(resolver.ResolvePath("<Mouse>/leftButton", InputPromptStyle.Glyph), Is.EqualTo("[Mouse]"));
    }

    [Test]
    public void PrintableCap_UsesPrintedCharacterAndPreservesDistinctLocalizedName()
    {
      const string layout = "MTKKeycapPrintedKeyboard";
      InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"Keyboard\",\"controls\":[{\"name\":\"a\",\"displayName\":\"Q\"}]}");
      var keyboard = InputSystem.AddDevice(layout);
      try
      {
        var printed = Cap("q", "Q");
        catalog.SetEntries(Cap("a", "A"), printed);
        catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
        names.Values["input.key.q"] = "Letter Q";
        using var action = new InputAction("Use", binding: "<" + layout + ">/a");
        Assert.That(resolver.Resolve(action), Is.EqualTo(Tag(printed)));
        Assert.That(resolver.Resolve(action, InputPromptStyle.GlyphAndName), Is.EqualTo(Tag(printed) + " Letter Q"));
      }
      finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveLayout(layout); }
    }

    [Test]
    public void CatalogModeChanges_RefreshLiveBindingAndReleaseSubscriptionsOnDispose()
    {
      var cap = Cap("e", "E");
      catalog.SetEntries(cap);
      var settings = Own(ScriptableObject.CreateInstance<InputPromptSettings>());
      settings.Configure(catalog);
      using var service = new InputPromptService(settings, localizer: names);
      var host = Own(new GameObject("Keycap binding", typeof(RectTransform)));
      var text = host.AddComponent<TextMeshProUGUI>();
      int renders = 0;
      var binding = service.Bind(text, () => { renders++; return service.ResolvePath("<Keyboard>/e"); });
      try
      {
        Assert.That(text.text, Is.EqualTo("E"));
        catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
        Assert.That(text.text, Is.EqualTo(Tag(cap)));
        catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.Authored);
        Assert.That(text.text, Is.EqualTo("E"));
      }
      finally { binding.Dispose(); }
      int previous = renders;
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      Assert.That(renders, Is.EqualTo(previous));
      Assert.That(text.text, Is.EqualTo("E"));
    }

    [TestCase(InputGlyphPlatform.Any)]
    [TestCase(InputGlyphPlatform.Windows)]
    [TestCase(InputGlyphPlatform.MacOS)]
    [TestCase(InputGlyphPlatform.Linux)]
    public void PlatformOverride_PrefersTheSpecificMixedCapBeforeAnyIconFallback(InputGlyphPlatform platform)
    {
      var caps = PlatformCaps();
      var context = new InputPromptContext().WithPlatform(platform);
      Assert.That(resolver.ResolvePath("<Keyboard>/leftMeta", InputPromptStyle.Glyph, context), Is.EqualTo(Tag(caps[platform])));
      Assert.That(catalog.TryGetKeycapEntry("leftMeta", "en", "System", platform, out var entry), Is.True);
      Assert.That(entry, Is.SameAs(caps[platform]));
      Assert.That(catalog.TryGetKeycapEntry("leftMeta", "en", "System", out var legacy), Is.True);
      Assert.That(legacy, Is.SameAs(caps[InputGlyphPlatform.Any]), "Legacy APIs remain platform independent.");
    }

    [TestCase(OperatingSystemFamily.Windows, InputGlyphPlatform.Windows)]
    [TestCase(OperatingSystemFamily.MacOSX, InputGlyphPlatform.MacOS)]
    [TestCase(OperatingSystemFamily.Linux, InputGlyphPlatform.Linux)]
    [TestCase(OperatingSystemFamily.Other, InputGlyphPlatform.Any)]
    public void NativeOperatingSystemFamily_MapsToTypedPlatform(OperatingSystemFamily family, InputGlyphPlatform expected)
      => Assert.That(InputGlyphCatalog.PlatformForOperatingSystem(family), Is.EqualTo(expected));

    [Test]
    public void AutomaticPlatform_UsesTheActualUnityHostFamily()
    {
      var caps = PlatformCaps();
      var expected = SystemInfo.operatingSystemFamily switch
      {
        OperatingSystemFamily.Windows => InputGlyphPlatform.Windows,
        OperatingSystemFamily.MacOSX => InputGlyphPlatform.MacOS,
        OperatingSystemFamily.Linux => InputGlyphPlatform.Linux,
        _ => InputGlyphPlatform.Any
      };
      Assert.That(resolver.ResolvePath("<Keyboard>/leftMeta", InputPromptStyle.Glyph), Is.EqualTo(Tag(caps[expected])));
    }

    [Test]
    public void PlatformImageLookup_KeepsLegacyAnyAndControllerCompatibility()
    {
      var windows = Cap("leftMeta", null); windows.Platform = InputGlyphPlatform.Windows;
      var any = Cap("leftMeta", null);
      var mac = Cap("leftMeta", null); mac.Platform = InputGlyphPlatform.MacOS;
      catalog.SetEntries(windows, any, mac, new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.Xbox, ControlPath = "buttonSouth", Symbol = "[A]"
      });
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "leftMeta"), Is.SameAs(any.ImageSprite));
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "leftMeta", InputGlyphPlatform.MacOS), Is.SameAs(mac.ImageSprite));
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.Glyph,
        new InputPromptContext(familyOverride: InputGlyphFamily.Xbox).WithPlatform(InputGlyphPlatform.Linux)), Is.EqualTo("[A]"));
    }

    [Test]
    public void BoundPlatformContexts_RemainIndependentAndPreserveTheirActionSelection()
    {
      var caps = PlatformCaps();
      var settings = Own(ScriptableObject.CreateInstance<InputPromptSettings>());
      settings.Configure(catalog);
      using var action = new InputAction("Independent OS prompts");
      action.AddBinding("<Keyboard>/leftMeta", groups: "Keyboard");
      action.AddBinding("<Gamepad>/buttonSouth", groups: "Controller");
      using var first = new InputPromptService(settings, localizer: names);
      using var second = new InputPromptService(settings, localizer: names);
      var original = new InputPromptContext(null, "Keyboard", InputGlyphFamily.Generic, 0, TMP_Settings.defaultFontAsset);
      var windows = original.WithPlatform(InputGlyphPlatform.Windows);
      Assert.That(windows.BindingGroup, Is.EqualTo(original.BindingGroup));
      Assert.That(windows.BindingIndex, Is.EqualTo(original.BindingIndex));
      Assert.That(windows.FontAsset, Is.SameAs(original.FontAsset));
      Assert.That(windows.FamilyOverride, Is.EqualTo(original.FamilyOverride));
      first.SetContext(windows);
      second.SetContext(original.WithPlatform(InputGlyphPlatform.MacOS));
      var firstHost = Own(new GameObject("Windows prompt", typeof(RectTransform), typeof(TextMeshProUGUI)));
      var secondHost = Own(new GameObject("Mac prompt", typeof(RectTransform), typeof(TextMeshProUGUI)));
      var firstText = firstHost.GetComponent<TextMeshProUGUI>();
      var secondText = secondHost.GetComponent<TextMeshProUGUI>();
      using var firstBinding = first.Bind(firstText, () => first.Resolve(action, InputPromptStyle.Glyph));
      using var secondBinding = second.Bind(secondText, () => second.Resolve(action, InputPromptStyle.Glyph));
      Assert.That(firstText.text, Is.EqualTo(Tag(caps[InputGlyphPlatform.Windows])));
      Assert.That(secondText.text, Is.EqualTo(Tag(caps[InputGlyphPlatform.MacOS])));
      second.SetContext(original.WithPlatform(InputGlyphPlatform.Linux));
      Assert.That(secondText.text, Is.EqualTo(Tag(caps[InputGlyphPlatform.Linux])));
      Assert.That(firstText.text, Is.EqualTo(Tag(caps[InputGlyphPlatform.Windows])));
      Assert.That(action.enabled, Is.False);
    }

    private Dictionary<InputGlyphPlatform, InputGlyphCatalog.Entry> PlatformCaps()
    {
      var result = new Dictionary<InputGlyphPlatform, InputGlyphCatalog.Entry>();
      foreach (var platform in new[] { InputGlyphPlatform.Any, InputGlyphPlatform.Windows, InputGlyphPlatform.MacOS, InputGlyphPlatform.Linux })
      {
        var entry = Cap("leftMeta", platform == InputGlyphPlatform.Any ? null : "System", platform == InputGlyphPlatform.Any ? null : "en");
        entry.Platform = platform;
        entry.DisplayName = "System";
        entry.LocalizationKey = "input.key.leftMeta";
        entry.KeycapContent = platform == InputGlyphPlatform.Any ? InputKeycapContentMode.Sprite : InputKeycapContentMode.SpriteAndLabel;
        result.Add(platform, entry);
      }
      catalog.SetEntries(result.Values.ToArray());
      catalog.ConfigureKeyboardGlyphs(InputKeyboardGlyphMode.GeneratedKeycaps);
      return result;
    }

    private InputGlyphCatalog.Entry Cap(string control, string label, string locale = null)
    {
      var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
      asset.name = "MTK_Keycaps_" + Guid.NewGuid().ToString("N");
      var serialized = new SerializedObject(asset);
      serialized.FindProperty("m_Version").stringValue = "1.1.0";
      serialized.ApplyModifiedPropertiesWithoutUndo();
      var texture = Own(new Texture2D(2, 2));
      asset.spriteSheet = texture;
      asset.material = Own(new Material(Shader.Find("TextMeshPro/Sprite")));
      asset.material.mainTexture = texture;
      var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero));
      var glyph = new TMP_SpriteGlyph { index = 0, sprite = sprite };
      asset.spriteGlyphTable.Add(glyph);
      asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xfffe, glyph) { name = "keycap" });
      asset.UpdateLookupTables();
      return new InputGlyphCatalog.Entry
      {
        Family = InputGlyphFamily.KeyboardAndMouse, ControlPath = control,
        BakedLabel = label, LocaleCode = locale, SpriteAsset = asset, SpriteName = "keycap", ImageSprite = sprite
      };
    }

    private static string Tag(InputGlyphCatalog.Entry entry)
      => "<sprite=\"" + entry.SpriteAsset.name + "\" name=\"" + entry.SpriteName + "\">";

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    private sealed class Names : IInputPromptLocalizer, IInputPromptLocaleProvider
    {
      public string LocaleCode { get; set; } = "en";
      public readonly Dictionary<string, string> Values = new();
      public string GetName(string key, string fallback) => Values.TryGetValue(key, out string value) ? value : fallback;
    }
  }
}
