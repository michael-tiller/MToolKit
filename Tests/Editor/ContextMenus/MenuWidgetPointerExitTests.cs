using System;
using System.Linq;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuWidgetPointerExitTests
  {
    private GameObject root;

    [TearDown]
    public void Cleanup()
    {
      if (root != null) Object.DestroyImmediate(root);
      root = null;
    }

    private MenuActionWidget Widget()
    {
      root = new GameObject("Pointer exit contract", typeof(RectTransform));
      var widget = root.AddComponent<MenuActionWidget>();
      widget.UseSkin = false;
      return widget;
    }

    private static MenuWidgetState State(Action<Vector2> pointerExited, Action<bool> hoverChanged = null)
    {
      // Reflect only the new optional seam so this test compiles against the pre-change contract.
      var constructor = typeof(MenuWidgetState).GetConstructors().SingleOrDefault(value =>
        value.GetParameters().Last().Name == "pointerExited");
      Assert.That(constructor, Is.Not.Null, "The additive final pointer-exit callback is missing.");
      var parameters = constructor.GetParameters();
      Assert.That(parameters.Last().ParameterType, Is.EqualTo(typeof(Action<Vector2>)));
      Assert.That(parameters.Last().IsOptional, Is.True, "Existing widget-state callers must remain compatible.");
      var values = parameters.Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray();
      values[0] = null;
      values[1] = MenuActionState.Available;
      values[Array.FindIndex(parameters, parameter => parameter.Name == "hoverChanged")] = hoverChanged;
      values[values.Length - 1] = pointerExited;
      return (MenuWidgetState)constructor.Invoke(values);
    }

    [Test]
    public void Pointer_exit_delivers_the_current_endpoint_without_also_clearing_legacy_hover()
    {
      var widget = Widget();
      int exits = 0, legacy = 0;
      var received = Vector2.zero;
      var endpoint = new Vector2(123.5f, 67.25f);
      widget.Bind(State(position => { exits++; received = position; }, _ => legacy++));
      widget.OnPointerExit(new PointerEventData(null) { position = endpoint });
      Assert.That(exits, Is.EqualTo(1));
      Assert.That(received, Is.EqualTo(endpoint));
      Assert.That(legacy, Is.Zero, "The presenter owns destination-aware clearing when it supplies this callback.");
    }

    [Test]
    public void Relabeling_preserves_the_same_pointer_exit_callback()
    {
      Action<Vector2> callback = _ => { };
      var original = State(callback);
      var relabeled = original.WithLabel("Localized label");
      var property = typeof(MenuWidgetState).GetProperty("PointerExited");
      Assert.That(property, Is.Not.Null);
      Assert.That(property.GetValue(original), Is.SameAs(callback));
      Assert.That(property.GetValue(relabeled), Is.SameAs(callback));
      Assert.That(relabeled.Label, Is.EqualTo("Localized label"));
    }

    [Test]
    public void Legacy_exit_still_clears_hover_and_an_unbound_exit_does_nothing()
    {
      var widget = Widget();
      int legacy = 0;
      bool? value = null;
      widget.Bind(new MenuWidgetState(null, MenuActionState.Available,
        hoverChanged: hovered => { legacy++; value = hovered; }));
      widget.OnPointerExit(new PointerEventData(null));
      Assert.That(legacy, Is.EqualTo(1));
      Assert.That(value, Is.False);
      widget.Unbind();
      widget.OnPointerExit(new PointerEventData(null));
      Assert.That(legacy, Is.EqualTo(1));
    }

    [Test]
    public void A_pointer_exit_callback_can_rebind_without_falling_through_to_either_hover_owner()
    {
      var widget = Widget();
      int exits = 0, oldHover = 0, replacementHover = 0;
      var replacement = new MenuWidgetState(null, MenuActionState.Available,
        hoverChanged: _ => replacementHover++);
      widget.Bind(State(_ => { exits++; widget.Bind(replacement); }, _ => oldHover++));
      widget.OnPointerExit(new PointerEventData(null) { position = Vector2.one });
      Assert.That(exits, Is.EqualTo(1));
      Assert.That(oldHover, Is.Zero);
      Assert.That(replacementHover, Is.Zero);
    }
  }
}
