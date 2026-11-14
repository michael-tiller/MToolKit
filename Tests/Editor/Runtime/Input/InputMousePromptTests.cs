using System.Collections.Generic;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Input
{
  public sealed class InputMousePromptTests
  {
    private InputGlyphCatalog catalog;
    private InputPromptResolver resolver;

    [SetUp]
    public void SetUp()
    {
      catalog = ScriptableObject.CreateInstance<InputGlyphCatalog>();
      resolver = new InputPromptResolver(catalog);
    }

    [TearDown]
    public void TearDown()
    {
      Object.DestroyImmediate(catalog);
    }

    [Test]
    public void StandardMouseButtonsRetainTheirNativeControlPaths()
    {
      string[] paths = { "leftButton", "rightButton", "middleButton", "forwardButton", "backButton" };
      string[] labels = { "LMB", "RMB", "MMB", "EXTRA1", "EXTRA2" };
      var entries = new List<InputGlyphCatalog.Entry>();
      for (int index = 0; index < paths.Length; index++)
        entries.Add(new InputGlyphCatalog.Entry { ControlPath = paths[index], Symbol = "[" + labels[index] + "]" });
      catalog.SetEntries(entries.ToArray());
      for (int index = 0; index < paths.Length; index++)
      {
        using var action = new InputAction("Mouse " + index, binding: "<Mouse>/" + paths[index]);
        Assert.That(resolver.Resolve(action, InputPromptStyle.Glyph), Is.EqualTo("[" + labels[index] + "]"));
        Assert.That(action.bindings[0].effectivePath, Is.EqualTo("<Mouse>/" + paths[index]));
      }
    }

    [Test]
    public void UndirectedNativeScrollDoesNotSelectDirectionalDisplayArt()
    {
      catalog.SetEntries(
        new InputGlyphCatalog.Entry { ControlPath = "scroll/up", Symbol = "[UP]" },
        new InputGlyphCatalog.Entry { ControlPath = "scroll/down", Symbol = "[DOWN]" },
        new InputGlyphCatalog.Entry { ControlPath = "scroll/y", Symbol = "[WHEEL]" });
      using var action = new InputAction("Scroll", binding: "<Mouse>/scroll/y");
      Assert.That(resolver.Resolve(action, InputPromptStyle.Glyph), Is.EqualTo("[WHEEL]"));
      Assert.That(InputControlPath.TryGetControlLayout("<Mouse>/scroll/y"), Is.EqualTo("Axis"));
      Assert.That(InputControlPath.TryGetControlLayout("<Mouse>/scroll/up"), Is.EqualTo("Axis"));
      Assert.That(InputControlPath.TryGetControlLayout("<Mouse>/scroll/down"), Is.EqualTo("Axis"));
    }

  }
}
