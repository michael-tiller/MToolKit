using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input.Config;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputControllerGlyphFallbackReviewTests
  {
    private readonly List<Object> owned = new();
    private InputGlyphCatalog catalog;
    private InputPromptResolver resolver;
    private TMP_SpriteAsset generic;
    private Sprite genericImage;
    private const string UnknownLayout = "MTKReviewUnknownGamepad";

    [SetUp]
    public void SetUp()
    {
      catalog = Own(ScriptableObject.CreateInstance<InputGlyphCatalog>());
      generic = Atlas("MTK_Review_Generic", "generic-south", "generic-east", "generic-west", "generic-north", "generic-enter");
      genericImage = generic.spriteGlyphTable[0].sprite;
      resolver = new InputPromptResolver(catalog, new Names());
      InputSystem.RegisterLayout("{\"name\":\"" + UnknownLayout + "\",\"extend\":\"Gamepad\"}");
    }

    [TearDown]
    public void TearDown()
    {
      InputSystem.RemoveLayout(UnknownLayout);
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
    }

    [TestCase("missing-entry")]
    [TestCase("missing-asset")]
    [TestCase("missing-name")]
    [TestCase("unknown-name")]
    [TestCase("missing-material")]
    [TestCase("missing-material-texture")]
    [TestCase("wrong-material-texture")]
    [TestCase("missing-atlas")]
    [TestCase("unsafe-asset-name")]
    [TestCase("unsafe-sprite-name")]
    [TestCase("missing-character-glyph")]
    [TestCase("empty-glyph-rect")]
    [TestCase("out-of-atlas-glyph")]
    [TestCase("unavailable-registration")]
    public void UnusableFamilyTmpGlyphUsesGenericButKeepsTheOriginalLocalizedName(string defect)
    {
      var original = Atlas("MTK_Review_PlayStation", "playstation-south");
      var family = Entry(InputGlyphFamily.PlayStation, "buttonSouth", original, "playstation-south");
      family.LocalizationKey = "original.south";
      family.DisplayName = "Original";
      if (defect == "missing-asset") family.SpriteAsset = null;
      if (defect == "missing-name") family.SpriteName = null;
      if (defect == "unknown-name") family.SpriteName = "not-authored";
      if (defect == "missing-material") original.material = null;
      if (defect == "missing-material-texture") original.material.mainTexture = null;
      if (defect == "wrong-material-texture") original.material.mainTexture = Texture2D.blackTexture;
      if (defect == "missing-atlas") original.spriteSheet = null;
      if (defect == "unsafe-asset-name") original.name = "bad\"asset";
      if (defect == "unsafe-sprite-name") family.SpriteName = "bad\"sprite";
      if (defect == "missing-character-glyph") original.spriteCharacterTable[0].glyph = null;
      if (defect == "empty-glyph-rect") original.spriteGlyphTable[0].glyphRect = new GlyphRect(0, 0, 0, 0);
      if (defect == "out-of-atlas-glyph") original.spriteGlyphTable[0].glyphRect = new GlyphRect(1000, 1000, 16, 16);
      var fallback = Entry(InputGlyphFamily.Generic, "buttonSouth", generic, "generic-south");
      fallback.DisplayName = "Generic South";
      fallback.LocalizationKey = "generic.south";
      catalog.SetEntries(defect == "missing-entry" ? new[] { fallback } : new[] { family, fallback });
      resolver.SpriteAvailable = asset => defect != "unavailable-registration" || asset != original;
      var context = new InputPromptContext(familyOverride: InputGlyphFamily.PlayStation);
      foreach (var style in new[] { InputPromptStyle.Auto, InputPromptStyle.Glyph, InputPromptStyle.GlyphAndName })
        Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", style, context),
          Is.EqualTo(Tag(generic, "generic-south") + (style == InputPromptStyle.GlyphAndName ? " Croix" : "")), defect + " " + style);
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.Character, context), Is.EqualTo("Croix"));
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.LocalizedName, context), Is.EqualTo("Croix"));
    }

    [TestCase(InputGlyphFamily.Xbox, "A", "B", "X", "Y")]
    [TestCase(InputGlyphFamily.PlayStation, "Cross", "Circle", "Square", "Triangle")]
    [TestCase(InputGlyphFamily.Nintendo, "B", "A", "Y", "X")]
    public void MissingFamilyArtKeepsAllFourLogicalFaceControls(InputGlyphFamily family, string south, string east, string west, string north)
    {
      string[] controls = { "buttonSouth", "buttonEast", "buttonWest", "buttonNorth" };
      string[] names = { "generic-south", "generic-east", "generic-west", "generic-north" };
      var rows = new InputGlyphCatalog.Entry[4];
      for (int i = 0; i < rows.Length; i++) rows[i] = Entry(InputGlyphFamily.Generic, controls[i], generic, names[i]);
      catalog.SetEntries(rows);
      var context = new InputPromptContext(familyOverride: family);
      string[] labels = { south, east, west, north };
      for (int i = 0; i < rows.Length; i++)
      {
        string name = family == InputGlyphFamily.PlayStation && i == 0 ? "Croix" : labels[i];
        Assert.That(resolver.ResolvePath("<Gamepad>/" + controls[i], InputPromptStyle.Glyph, context), Is.EqualTo(Tag(generic, names[i])));
        Assert.That(resolver.ResolvePath("<Gamepad>/" + controls[i], InputPromptStyle.GlyphAndName, context), Is.EqualTo(Tag(generic, names[i]) + " " + name));
        Assert.That(resolver.ResolvePath("<Gamepad>/" + controls[i], InputPromptStyle.LocalizedName, context), Is.EqualTo(name));
      }
    }

    [Test]
    public void ValidOriginalFamilyArtWinsAndFinalFailureKeepsItsName()
    {
      var original = Atlas("MTK_Review_Original", "original-south");
      var family = Entry(InputGlyphFamily.PlayStation, "buttonSouth", original, "original-south");
      family.LocalizationKey = "original.south";
      catalog.SetEntries(family, Entry(InputGlyphFamily.Generic, "buttonSouth", generic, "generic-south"));
      var context = new InputPromptContext(familyOverride: InputGlyphFamily.PlayStation);
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.GlyphAndName, context),
        Is.EqualTo(Tag(original, "original-south") + " Croix"));
      resolver.SpriteAvailable = _ => false;
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.GlyphAndName, context), Is.EqualTo("Croix"));
      catalog.SetEntries(Entry(InputGlyphFamily.Generic, "buttonEast", generic, "generic-east"));
      Assert.That(resolver.ResolvePath("<Gamepad>/buttonSouth", InputPromptStyle.Glyph, context), Is.EqualTo("Croix"),
        "A fallback never borrows a different logical control.");
    }

    [TestCase(InputGlyphFamily.Xbox)]
    [TestCase(InputGlyphFamily.PlayStation)]
    [TestCase(InputGlyphFamily.Nintendo)]
    public void ImageLookupUsesOriginalThenGenericThenNull(InputGlyphFamily family)
    {
      var image = Image("original-image");
      var original = new InputGlyphCatalog.Entry { Family = family, ControlPath = "buttonSouth", ImageSprite = image };
      var fallback = new InputGlyphCatalog.Entry { Family = InputGlyphFamily.Generic, ControlPath = "buttonSouth", ImageSprite = genericImage };
      catalog.SetEntries(original, fallback);
      Assert.That(catalog.GetSprite(family, "buttonSouth"), Is.SameAs(image));
      original.ImageSprite = null;
      Assert.That(catalog.GetSprite(family, "buttonSouth"), Is.SameAs(genericImage));
      fallback.ImageSprite = null;
      Assert.That(catalog.GetSprite(family, "buttonSouth"), Is.Null, "Image callers use the original-family text after both image stages fail.");
    }

    [Test]
    public void NativeImageGlyphDoesNotRequireTmpMaterialWhenItsSpriteIsUsable()
    {
      var native = Atlas("MTK_Review_Image", "native-image");
      var sprite = native.spriteGlyphTable[0].sprite;
      native.material = null;
      var original = Entry(InputGlyphFamily.PlayStation, "buttonSouth", native, "native-image");
      catalog.SetEntries(original, new InputGlyphCatalog.Entry { Family = InputGlyphFamily.Generic, ControlPath = "buttonSouth", ImageSprite = genericImage });
      Assert.That(catalog.GetSprite(InputGlyphFamily.PlayStation, "buttonSouth"), Is.SameAs(sprite));
    }

    [Test]
    public void KeyboardPresentationNeverUsesGenericControllerArtwork()
    {
      catalog.SetEntries(Entry(InputGlyphFamily.Generic, "enter", generic, "generic-enter"));
      Assert.That(resolver.ResolvePath("<Keyboard>/enter", InputPromptStyle.Glyph), Is.EqualTo("Enter"));
      Assert.That(catalog.GetSprite(InputGlyphFamily.KeyboardAndMouse, "enter"), Is.Null);
    }

    [TestCase(InputGlyphFamily.Xbox, "XInputController")]
    [TestCase(InputGlyphFamily.PlayStation, "DualShockGamepad")]
    public void ValidSerializedFamilyArtPrecedesGenericCatalogArt(InputGlyphFamily family, string layout)
    {
      var data = Legacy();
      var familySprite = family == InputGlyphFamily.Xbox ? data.XboxIcons.GetSprite("buttonSouth") : data.Ps4Icons.GetSprite("buttonSouth");
      catalog.SetEntries(new InputGlyphCatalog.Entry { Family = InputGlyphFamily.Generic, ControlPath = "buttonSouth", ImageSprite = genericImage });
      data.SetCatalog(catalog);
      Assert.That(data.GetIcon(family, "buttonSouth"), Is.SameAs(familySprite));
      Assert.That(data.GetIcon(layout, "buttonSouth"), Is.SameAs(familySprite));
    }

    [TestCase(InputGlyphFamily.Generic, "Gamepad")]
    [TestCase(InputGlyphFamily.Generic, UnknownLayout)]
    [TestCase(InputGlyphFamily.Nintendo, "SwitchProControllerHID")]
    public void LegacyAdapterNeverBorrowsXboxForUnknownOrNintendoControllers(InputGlyphFamily family, string layout)
    {
      var data = Legacy();
      catalog.SetEntries(new InputGlyphCatalog.Entry { Family = InputGlyphFamily.Generic, ControlPath = "buttonSouth", ImageSprite = genericImage });
      data.SetCatalog(catalog);
      Assert.That(data.GetIcon(family, "buttonSouth"), Is.SameAs(genericImage));
      Assert.That(data.GetIcon(layout, "buttonSouth"), Is.SameAs(genericImage));
      data.SetCatalog(null);
      Assert.That(data.GetIcon(layout, "buttonSouth"), Is.Null, "Without Generic art, this renderer reaches the text stage; Xbox art is not a universal fallback.");
    }

    private GamepadIconsData Legacy()
    {
      var data = Own(ScriptableObject.CreateInstance<GamepadIconsData>());
      var serialized = new SerializedObject(data);
      serialized.FindProperty("<XboxIcons>k__BackingField").FindPropertyRelative("buttonSouth").objectReferenceValue = Image("legacy-xbox");
      serialized.FindProperty("<Ps4Icons>k__BackingField").FindPropertyRelative("buttonSouth").objectReferenceValue = Image("legacy-playstation");
      serialized.ApplyModifiedPropertiesWithoutUndo();
      return data;
    }

    private TMP_SpriteAsset Atlas(string name, params string[] names)
    {
      var texture = Own(new Texture2D(names.Length * 16, 16, TextureFormat.RGBA32, false));
      var material = Own(new Material(Shader.Find("TextMeshPro/Sprite")));
      material.mainTexture = texture;
      var asset = Own(ScriptableObject.CreateInstance<TMP_SpriteAsset>());
      var serialized = new SerializedObject(asset);
      serialized.FindProperty("m_Version").stringValue = "1.1.0";
      serialized.ApplyModifiedPropertiesWithoutUndo();
      asset.name = name;
      asset.spriteSheet = texture;
      asset.material = material;
      for (int i = 0; i < names.Length; i++)
      {
        var sprite = Own(Sprite.Create(texture, new Rect(i * 16, 0, 16, 16), Vector2.one * .5f));
        var glyph = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(16, 16, 0, 16, 16), new GlyphRect(i * 16, 0, 16, 16), 1, 0, sprite);
        asset.spriteGlyphTable.Add(glyph);
        asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xfffe, asset, glyph) { name = names[i] });
      }
      asset.UpdateLookupTables();
      return asset;
    }

    private Sprite Image(string name)
    {
      var texture = Own(new Texture2D(16, 16, TextureFormat.RGBA32, false));
      var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f));
      sprite.name = name;
      return sprite;
    }

    private static InputGlyphCatalog.Entry Entry(InputGlyphFamily family, string control, TMP_SpriteAsset asset, string name)
      => new() { Family = family, ControlPath = control, SpriteAsset = asset, SpriteName = name };
    private static string Tag(TMP_SpriteAsset asset, string name) => "<sprite=\"" + asset.name + "\" name=\"" + name + "\">";
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private sealed class Names : IInputPromptLocalizer
    {
      public string GetName(string key, string fallback)
        => key == "original.south" || key == "input.gamepad.playstation.buttonSouth" ? "Croix" : fallback;
    }
  }
}
