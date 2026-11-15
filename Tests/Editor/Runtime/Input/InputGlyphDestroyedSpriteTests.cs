using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input.Config;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public class InputGlyphDestroyedSpriteTests
  {
    private readonly List<Object> owned = new();
    private InputGlyphCatalog catalog;
    private const string Control = "buttonSouth";

    [SetUp]
    public void SetUp() => catalog = Own(ScriptableObject.CreateInstance<InputGlyphCatalog>());

    [TearDown]
    public void TearDown()
    {
      for (int i = owned.Count - 1; i >= 0; i--)
        if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DestroyedFamilyTmpSpriteUsesLiveGenericArtwork(bool genericFromTmp)
    {
      var original = Atlas();
      var generic = Atlas();
      var fallback = Entry(InputGlyphFamily.Generic, generic);
      if (!genericFromTmp) fallback.ImageSprite = generic.spriteGlyphTable[0].sprite;
      catalog.SetEntries(Entry(InputGlyphFamily.PlayStation, original), fallback);
      Assert.That(ReferenceEquals(catalog.GetSprite(InputGlyphFamily.PlayStation, Control),
        original.spriteGlyphTable[0].sprite), Is.True, "Live family artwork must win first.");

      DestroyGlyphSprite(original);

      var expected = generic.spriteGlyphTable[0].sprite;
      Assert.That(ReferenceEquals(catalog.GetSprite(InputGlyphFamily.PlayStation, Control), expected), Is.True,
        "A detached Sprite wrapper must not suppress Generic fallback.");
      Assert.That(ReferenceEquals(catalog.GetSprite(InputGlyphFamily.PlayStation, Control,
        InputGlyphPlatform.Windows), expected), Is.True, "The platform overload shares the same fallback.");
    }

    [TestCase(InputGlyphFamily.Generic)]
    [TestCase(InputGlyphFamily.KeyboardAndMouse)]
    public void DestroyedTerminalSpriteReturnsRealNull(InputGlyphFamily family)
    {
      var broken = Atlas();
      var entry = Entry(family, broken);
      if (family == InputGlyphFamily.KeyboardAndMouse)
        catalog.SetEntries(entry, Entry(InputGlyphFamily.Generic, Atlas()));
      else catalog.SetEntries(entry);
      DestroyGlyphSprite(broken);

      Assert.That(ReferenceEquals(catalog.GetSprite(family, Control), null), Is.True,
        "Return CLR null, not a destroyed wrapper; keyboard input must not borrow controller art.");
    }

    [Test]
    public void DestroyedFamilyAndGenericSpritesEndWithRealNull()
    {
      var original = Atlas();
      var generic = Atlas();
      catalog.SetEntries(Entry(InputGlyphFamily.PlayStation, original), Entry(InputGlyphFamily.Generic, generic));
      DestroyGlyphSprite(original);
      DestroyGlyphSprite(generic);

      Assert.That(ReferenceEquals(catalog.GetSprite(InputGlyphFamily.PlayStation, Control), null), Is.True);
    }

    [Test]
    public void DestroyedExplicitImageStillUsesTheLiveFamilyTmpSprite()
    {
      var original = Atlas();
      var entry = Entry(InputGlyphFamily.PlayStation, original);
      var detached = Atlas();
      entry.ImageSprite = detached.spriteGlyphTable[0].sprite;
      DestroyGlyphSprite(detached);
      catalog.SetEntries(entry, Entry(InputGlyphFamily.Generic, Atlas()));

      Assert.That(ReferenceEquals(catalog.GetSprite(InputGlyphFamily.PlayStation, Control),
        original.spriteGlyphTable[0].sprite), Is.True);
    }

    [Test]
    public void LiveImageSpriteDoesNotRequireAUsableTmpMaterial()
    {
      var original = Atlas();
      original.material = null;
      catalog.SetEntries(Entry(InputGlyphFamily.PlayStation, original), Entry(InputGlyphFamily.Generic, Atlas()));

      Assert.That(ReferenceEquals(catalog.GetSprite(InputGlyphFamily.PlayStation, Control),
        original.spriteGlyphTable[0].sprite), Is.True,
        "Image extraction and native TMP material validation are independent.");
    }

    [Test]
    public void LegacyImageConsumerDoesNotLeakADestroyedGenericSprite()
    {
      var generic = Atlas();
      catalog.SetEntries(Entry(InputGlyphFamily.Generic, generic));
      DestroyGlyphSprite(generic);
      var legacy = Own(ScriptableObject.CreateInstance<GamepadIconsData>());
      legacy.SetCatalog(catalog);

      Assert.That(ReferenceEquals(legacy.GetIcon(InputGlyphFamily.Nintendo, Control), null), Is.True);
    }

    [Test]
    public void DestroyingTheConvenienceImageSpritePreservesNativeTmpPresentation()
    {
      var original = Atlas();
      catalog.SetEntries(Entry(InputGlyphFamily.PlayStation, original), Entry(InputGlyphFamily.Generic, Atlas()));
      DestroyGlyphSprite(original);
      var resolver = new InputPromptResolver(catalog, new Names()) { SpriteAvailable = _ => true };

      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.Glyph,
        new InputPromptContext(familyOverride: InputGlyphFamily.PlayStation)),
        Is.EqualTo("<sprite=\"" + original.name + "\" name=\"south\">"),
        "TMP renders the atlas and glyph metrics, independently of the optional Image Sprite wrapper.");
    }

    private static void DestroyGlyphSprite(TMP_SpriteAsset asset)
    {
      var sprite = asset.spriteGlyphTable[0].sprite;
      Object.DestroyImmediate(sprite);
      Assert.That(ReferenceEquals(sprite, null), Is.False, "Keep the managed wrapper alive.");
      Assert.That(sprite == null, Is.True, "Its native Unity Sprite must actually be destroyed.");
      Assert.That(ReferenceEquals(asset.spriteGlyphTable[0].sprite, sprite), Is.True);
    }

    private TMP_SpriteAsset Atlas()
    {
      var texture = Own(new Texture2D(16, 16, TextureFormat.RGBA32, false));
      var material = Own(new Material(Shader.Find("TextMeshPro/Sprite")));
      material.mainTexture = texture;
      var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f));
      var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
      var serialized = new SerializedObject(asset);
      serialized.FindProperty("m_Version").stringValue = "1.1.0";
      serialized.ApplyModifiedPropertiesWithoutUndo();
      asset.name = "MTK_DestroyedSprite_" + Guid.NewGuid().ToString("N");
      asset.spriteSheet = texture;
      asset.material = material;
      var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(16, 16, 0, 16, 16),
        new GlyphRect(0, 0, 16, 16), 1, 0, sprite);
      asset.spriteGlyphTable.Add(glyph);
      asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xfffe, asset, glyph) { name = "south" });
      asset.UpdateLookupTables();
      return asset;
    }

    private static InputGlyphCatalog.Entry Entry(InputGlyphFamily family, TMP_SpriteAsset asset)
      => new() { Family = family, ControlPath = Control, SpriteAsset = asset, SpriteName = "south" };

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    private sealed class Names : IInputPromptLocalizer
    {
      public string GetName(string key, string fallback) => fallback;
    }
  }
}
