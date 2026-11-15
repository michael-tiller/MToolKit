using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Rendering;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuHoverThemeTests
  {
    private readonly List<Object> objects = new();
    private EventSystem previousEvents;
    private MenuSession session;
    private MenuPresentationContext context;

    [TearDown]
    public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear(); context?.Dispose(); context = null; session?.Dispose(); session = null;
      if (previousEvents != null) EventSystem.current = previousEvents;
    }

    private GameObject Root(string name, Transform parent = null)
    {
      var root = new GameObject(name, typeof(RectTransform));
      if (parent == null) objects.Add(root); else root.transform.SetParent(parent, false);
      return root;
    }
    private MenuSkin Skin()
    {
      var skin = ScriptableObject.CreateInstance<MenuSkin>(); objects.Add(skin);
      skin.SetColor(MenuVisualRole.Hovered, Color.magenta);
      skin.SetColor(MenuVisualRole.Selected, Color.cyan);
      skin.SetColor(MenuVisualRole.Disabled, Color.gray);
      return skin;
    }

    [Test]
    public void DefaultWidgetUsesHoveredRoleWithoutChangingSelectionOrCheckedState()
    {
      var root = Root("Themed action"); var background = root.AddComponent<Image>();
      var widget = root.AddComponent<MenuActionWidget>(); widget.Background = background;
      widget.Highlight = Root("Selection marker", root.transform).AddComponent<Image>();
      widget.Check = Root("Checked marker", root.transform).AddComponent<Image>();
      var action = new MenuAction("action", new MenuLabel("Action"));
      var skin = Skin(); var state = new MenuActionState(true, true, true);
      widget.Bind(new MenuWidgetState(action, state, selected: true, skin: skin, isHovered: true));
      Assert.That(widget.Check.enabled, Is.True); Assert.That(widget.Highlight.enabled, Is.True);
      Assert.That(background.color, Is.EqualTo(Color.magenta), "Pointer-hot widgets consume the semantic Hovered token.");
      widget.Bind(new MenuWidgetState(action, state, selected: true, skin: skin));
      Assert.That(background.color, Is.EqualTo(Color.cyan), "Keyboard/controller focus uses Selected independently of hover.");
      Assert.That(widget.Check.enabled, Is.True);
      widget.Bind(new MenuWidgetState(action, new MenuActionState(true, false, true), selected: true, skin: skin, isHovered: true));
      Assert.That(background.color, Is.EqualTo(Color.gray), "Disabled state retains precedence.");
    }

    [Test]
    public void NativeSectorUsesHoveredForPointerAndSelectedForController()
    {
      previousEvents = EventSystem.current;
      var events = Root("Hover events").AddComponent<MenuPresenterTests.MenuTestEventSystem>(); EventSystem.current = events;
      var canvasRoot = Root("Hover canvas"); var canvas = canvasRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(1200, 900);
      var presenter = Root("Radial", canvas.transform).AddComponent<RadialMenuPresenter>();
      var profile = ScriptableObject.CreateInstance<MenuProfile>(); objects.Add(profile);
      profile.Skin = Skin(); profile.ShowCenter = false;
      session = new MenuSession();
      session.Open(new[] { new MenuAction("leaf", new MenuLabel("Leaf"), new MenuCommandBehavior(_ => { })) }, new MenuContext());
      context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback);
      presenter.Bind(session, profile, context);
      var graphic = presenter.GetComponentInChildren<NativeRadialGraphic>();
      var point = RectTransformUtility.WorldToScreenPoint(null,
        presenter.Root.TransformPoint(presenter.InnerGeometry.Segments[0].ContentAnchor));
      presenter.Point(point);
      Assert.That(graphic.color, Is.EqualTo(Color.magenta), "The visible native wedge uses the same pointer-hot role as its widget.");
      presenter.Navigate(Vector2.right, true);
      Assert.That(graphic.color, Is.EqualTo(Color.cyan));
      Assert.That(session.Selected.Id.Value, Is.EqualTo("leaf"), "Changing interaction device does not change the action contract.");
    }
  }
}
