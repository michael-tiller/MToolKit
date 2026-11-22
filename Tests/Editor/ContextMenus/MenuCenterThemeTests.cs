using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuCenterThemeTests
  {
    private readonly List<Object> objects = new();

    [TearDown]
    public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--)
        if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear();
    }

    private MenuCenterWidget Center(out Image background)
    {
      var root = new GameObject("Themed center information", typeof(RectTransform), typeof(Image));
      objects.Add(root); background = root.GetComponent<Image>();
      var widget = root.AddComponent<MenuCenterWidget>(); widget.Background = background;
      return widget;
    }

    private MenuSkin Skin()
    {
      var skin = ScriptableObject.CreateInstance<MenuSkin>(); objects.Add(skin);
      skin.SetColor(MenuVisualRole.Center, Color.black);
      skin.SetColor(MenuVisualRole.Selected, Color.cyan);
      skin.SetColor(MenuVisualRole.Hovered, Color.magenta);
      return skin;
    }

    [Test]
    public void CenterBackgroundUsesItsSemanticRoleWhileTheDisplayedActionIsSelected()
    {
      var widget = Center(out var background);
      var action = new MenuAction("selected", new MenuLabel("Selected action"));
      widget.Bind(new MenuWidgetState(action, MenuActionState.Available,
        selected: true, skin: Skin(), isHovered: true));
      Assert.That(background.color, Is.EqualTo(Color.black),
        "Center information has an independent semantic surface; the selected or hovered item keeps its own highlight.");
    }

    [Test]
    public void CenterWithSkinOptOutPreservesItsAuthoredBackground()
    {
      var widget = Center(out var background); widget.UseSkin = false;
      var authored = new Color(.17f, .29f, .43f, .8f); background.color = authored;
      var action = new MenuAction("selected", new MenuLabel("Selected action"));
      widget.Bind(new MenuWidgetState(action, MenuActionState.Available,
        selected: true, skin: Skin(), isHovered: true));
      Assert.That(background.color, Is.EqualTo(authored),
        "A custom center can retain its own composition and visual tokens.");
    }
  }
}
