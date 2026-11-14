using System;
using System.Collections.Generic;
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
