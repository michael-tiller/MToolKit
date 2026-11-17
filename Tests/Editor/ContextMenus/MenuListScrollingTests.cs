using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuListScrollingTests
  {
    private readonly List<Object> owned = new();
    private readonly Vector3[] corners = new Vector3[4];
    private EventSystem previousEvents;
    private MenuSession session;
    private MenuPresentationContext context;
    private MenuProfile profile;
    private ListMenuPresenter presenter;
    private Canvas canvas;
    private Camera camera;
    private MenuListHitRaycaster raycaster;
    private int calls;

    [SetUp]
    public void Setup()
    {
      calls = 0;
      previousEvents = EventSystem.current;
      var events = Own(new GameObject("Scrolling menu events", typeof(RectTransform),
        typeof(MenuPresenterTests.MenuTestEventSystem)));
      EventSystem.current = events.GetComponent<EventSystem>();
      var cameraOwner = Own(new GameObject("Scrolling menu camera", typeof(Camera)));
      camera = cameraOwner.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 500;
      camera.transform.position = new Vector3(0, 0, -10);
      var canvasOwner = Own(new GameObject("Scrolling menu canvas", typeof(RectTransform), typeof(Canvas)));
      canvas = canvasOwner.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(1000, 360);
      raycaster = canvasOwner.AddComponent<MenuListHitRaycaster>(); raycaster.Camera = camera;
      var root = new GameObject("Scrolling list", typeof(RectTransform));
      root.transform.SetParent(canvas.transform, false);
      presenter = root.AddComponent<ListMenuPresenter>();
      profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.MaximumVisibleItems = 5; profile.RowHeight = 40; profile.ListWidth = 320;
      profile.Backdrop = false; profile.ShowCenter = false;
      profile.InputBindings.Configure(routePointerClick: true);
      var screen = RectTransformUtility.WorldToScreenPoint(camera, canvas.transform.position);
      context = new MenuPresentationContext(canvas, screen, labelOverride: label => label.Fallback);
      session = new MenuSession();
    }

    [TearDown]
    public void Cleanup()
    {
      if (presenter != null) presenter.Unbind();
      session?.Dispose(); context?.Dispose();
      session = null; context = null;
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      if (previousEvents != null) EventSystem.current = previousEvents;
    }

    private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    private MenuAction Command(string id)
      => new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(_ => calls++, MenuActionOutcome.Keep));
    private MenuAction[] Commands(int count, string prefix = "item")
    {
      var result = new MenuAction[count];
      for (int i = 0; i < count; i++) result[i] = Command(prefix + i);
      return result;
    }
    private PropertyInfo Policy()
    {
      var property = typeof(MenuProfile).GetProperty("ListOverflow", BindingFlags.Instance | BindingFlags.Public);
      Assert.That(property, Is.Not.Null, "MenuProfile must author optional native scrolling independently of pagination.");
      Assert.That(property.PropertyType.FullName, Is.EqualTo("MToolKit.ContextMenus.Configuration.MenuListOverflowPolicy"));
      return property;
    }
    private void Scrolling()
    {
      var property = Policy();
      Assert.That(Enum.GetName(property.PropertyType, 0), Is.EqualTo("Pagination"));
      Assert.That(Enum.GetName(property.PropertyType, 1), Is.EqualTo("Scrolling"));
      property.SetValue(profile, Enum.ToObject(property.PropertyType, 1));
    }
    private void Open(IReadOnlyList<MenuAction> actions)
    {
      session.Open(actions, new MenuContext());
      presenter.Bind(session, profile, context);
      Assert.That(presenter.FitsViewport, Is.True, presenter.PlacementDiagnostic);
    }
    private ScrollRect Scroll()
    {
      var scroll = presenter.GetComponentInChildren<ScrollRect>(true);
      Assert.That(scroll, Is.Not.Null, "Scrolling must create a real native uGUI scroll owner.");
      Assert.That(scroll.viewport, Is.Not.Null);
      Assert.That(scroll.content, Is.Not.Null);
      Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null, "Overflow rows need actual clipping.");
      return scroll;
    }
    private MenuActionWidget Widget(string id)
    {
      var property = typeof(MenuActionWidget).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.That(property, Is.Not.Null, "The fixture inspects the existing protected binding without widening the widget API.");
      foreach (var widget in presenter.GetComponentsInChildren<MenuActionWidget>(true))
      {
        var state = (MenuWidgetState)property.GetValue(widget);
        if (state?.Action != null && state.Action.Id.Value == id) return widget;
      }
      Assert.Fail("Missing bound action widget: " + id);
      return null;
    }
    private Rect Bounds(RectTransform item, RectTransform viewport)
    {
      item.GetWorldCorners(corners);
      Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
      Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
      foreach (var corner in corners)
      {
        var local = (Vector2)viewport.InverseTransformPoint(corner);
        minimum = Vector2.Min(minimum, local); maximum = Vector2.Max(maximum, local);
      }
      return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }
    private void AssertRevealed(MenuActionWidget widget)
    {
      var scroll = widget.GetComponentInParent<ScrollRect>();
      Assert.That(scroll, Is.Not.Null, "Action roots must belong to the native scroll content.");
      Assert.That(widget.transform.IsChildOf(scroll.content), Is.True);
      var bounds = Bounds((RectTransform)widget.transform, scroll.viewport);
      Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - .1f));
      Assert.That(bounds.yMax, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + .1f));
    }

    [Test]
    public void Pagination_remains_the_serialized_default_and_keeps_bounded_pages()
    {
      Assert.That(Convert.ToInt32(Policy().GetValue(profile)), Is.Zero);
      Open(Commands(18));
      Assert.That(session.PageSize, Is.EqualTo(5));
      Assert.That(session.Rows.Count, Is.EqualTo(5));
      presenter.Page(1);
      Assert.That(session.PageIndex, Is.EqualTo(1));
      Assert.That(presenter.GetComponentInChildren<ScrollRect>(true), Is.Null);
    }

    [Test]
    public void Invalid_list_overflow_policy_has_a_useful_profile_diagnostic()
    {
      var property = Policy(); property.SetValue(profile, Enum.ToObject(property.PropertyType, 99));
      Assert.That(profile.Validate(out string diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain("ListOverflow"));
    }

    [Test]
    public void Native_scroll_view_keeps_all_rows_and_scrolls_content_without_turning_a_page()
    {
      Scrolling(); var actions = Commands(18); Open(actions);
      var scroll = Scroll();
      Assert.That(session.PageSize, Is.EqualTo(int.MaxValue));
      Assert.That(session.Rows.Count, Is.EqualTo(18));
      Assert.That(session.PageCount, Is.EqualTo(1));
      Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
      Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(18 * profile.RowHeight));
      Assert.That(scroll.viewport.rect.height, Is.LessThanOrEqualTo(360));
      var before = scroll.content.anchoredPosition;
      scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down * 4 });
      Assert.That(scroll.content.anchoredPosition, Is.Not.EqualTo(before));
      Assert.That(session.PageIndex, Is.Zero);
      Assert.That(Widget("item17").transform.IsChildOf(scroll.content), Is.True);
      var wheelOffset = scroll.content.anchoredPosition;
      actions[0].NotifyChanged();
      Assert.That(scroll.content.anchoredPosition, Is.EqualTo(wheelOffset), "A state/theme-style refresh must not undo the user's scroll position.");
      presenter.Page(1);
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item5"), "Explicit page controls move by a viewport while the session keeps one page.");
      AssertRevealed(Widget("item5"));
      presenter.Page(-1);
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item0"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Semantic_keyboard_or_controller_selection_reveals_offscreen_rows(bool controller)
    {
      Scrolling(); Open(Commands(18));
      var scroll = Scroll();
      Assert.That(Bounds((RectTransform)Widget("item17").transform, scroll.viewport).yMax,
        Is.LessThan(scroll.viewport.rect.yMin), "The target must begin clipped, rather than accidentally passing reveal.");
      for (int i = 0; i < 17; i++)
      {
        presenter.Navigate(Vector2.down, controller);
        if (controller) presenter.Navigate(Vector2.zero, true);
      }
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item17"));
      AssertRevealed(Widget("item17"));
      presenter.Confirm();
      Assert.That(calls, Is.EqualTo(1));
      Assert.That(session.PageIndex, Is.Zero);
    }

    [Test]
    public void Clipped_row_cannot_steal_semantic_pointer_selection_or_confirm_a_previous_leaf()
    {
      Scrolling(); Open(Commands(18));
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item0"));
      var scroll = Scroll();
      var clipped = (RectTransform)Widget("item17").transform;
      var point = RectTransformUtility.WorldToScreenPoint(camera, clipped.TransformPoint(clipped.rect.center));
      Assert.That(scroll.viewport.GetComponent<RectMask2D>().IsRaycastLocationValid(point, camera), Is.False);
      // Supply a broad-phase row hit deliberately: semantic selection must still honor the viewport mask.
      raycaster.Target = clipped;
      presenter.Point(point); presenter.ConfirmPointer();
      Assert.That(session.Selected, Is.Null);
      Assert.That(calls, Is.Zero, "A clipped row must not execute itself or reuse the old highlighted command.");
      Assert.That(session.IsOpen, Is.True);
    }

    [Test]
    public void Inline_dynamic_expansion_and_shrink_reconcile_content_selection_and_bounds()
    {
      Scrolling();
      IReadOnlyList<MenuAction> children = Commands(18, "child");
      var parent = new MenuAction("parent", new MenuLabel("parent"), childrenProvider:
        new DelegateMenuChildrenProvider((_, __) => UniTask.FromResult(children)));
      Open(new[] { parent }); presenter.Confirm();
      var scroll = Scroll();
      Assert.That(session.Path, Is.Empty);
      Assert.That(session.Rows.Count, Is.EqualTo(19));
      session.Select("child17"); AssertRevealed(Widget("child17"));
      float previousHeight = scroll.content.rect.height;
      children = Commands(2, "child"); parent.NotifyChanged();
      Assert.That(session.Rows.Count, Is.EqualTo(3));
      Assert.That(scroll.content.rect.height, Is.LessThan(previousHeight));
      Assert.That(session.Selected.Id.Value, Is.Not.EqualTo("child17"));
      AssertRevealed(Widget(session.Selected.Id.Value));
      session.Select("parent"); presenter.Confirm();
      Assert.That(session.Rows.Count, Is.EqualTo(1));
      AssertRevealed(Widget("parent"));
      Assert.That(calls, Is.Zero);
    }

    [Test]
    public void Unbind_and_rebind_retire_owned_scroll_objects_and_old_action_callbacks()
    {
      Scrolling(); var oldActions = Commands(18); Open(oldActions);
      var oldScroll = Scroll();
      presenter.Unbind();
      Assert.That(oldScroll == null || !oldScroll.gameObject.activeInHierarchy, Is.True);
      session.Open(new[] { Command("fresh") }, new MenuContext());
      Policy().SetValue(profile, Enum.ToObject(Policy().PropertyType, 0));
      presenter.Bind(session, profile, context);
      oldActions[0].NotifyChanged();
      Assert.That(session.Rows.Count, Is.EqualTo(1));
      Assert.That(Widget("fresh").gameObject.activeInHierarchy, Is.True);
      Assert.That(presenter.GetComponentInChildren<ScrollRect>(), Is.Null);
      Assert.That(calls, Is.Zero);
    }

    [Test]
    public void Scrolling_flyout_keeps_parent_and_child_columns_accessible()
    {
      Scrolling(); profile.Navigation = MenuNavigationPolicy.Flyout;
      var parents = new MenuAction[12];
      for (int i = 0; i < parents.Length; i++)
        parents[i] = new MenuAction("parent" + i, new MenuLabel("parent" + i), children: Commands(8, "p" + i + "child"));
      Open(parents); session.EnterSubmenuAsync("parent11").GetAwaiter().GetResult();
      Assert.That(session.Rows.Count, Is.EqualTo(8));
      Assert.That(session.Path.Count, Is.EqualTo(1));
      for (int i = 0; i < parents.Length; i++) Assert.That(Widget("parent" + i), Is.Not.Null);
      AssertRevealed(Widget("parent11"));
      AssertRevealed(Widget("p11child0"));
      presenter.Navigate(Vector2.down, false); presenter.Confirm();
      Assert.That(calls, Is.EqualTo(1));
      Assert.That(session.ParentSelected.Id.Value, Is.EqualTo("parent11"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Native_row_pointer_release_clicks_once_but_drag_never_executes_on_press(bool drag)
    {
      using var input = new PointerScope();
      profile.InputBindings.Configure(point: input.Reference(input.Point), click: input.Reference(input.Click), routePointerClick: true);
      Scrolling(); Open(Commands(18));
      using var ownership = new MenuInputOwnership();
      using var router = new MenuInputRouter(presenter, profile.InputBindings, ownership: ownership);
      var widget = Widget("item0");
      var scroll = Scroll();
      raycaster.Target = (RectTransform)widget.transform;
      Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, raycaster.Target.TransformPoint(raycaster.Target.rect.center));
      input.Send(point, false); input.Send(point, true);
      Assert.That(calls, Is.Zero, "Semantic press must defer to native pointer release so a row can become a scroll drag.");
      Assert.That(widget.Button.enabled, Is.True, "The native Button owns the actual pointer click while semantic navigation remains active.");
      var pointer = new PointerEventData(EventSystem.current)
      {
        position = point, pressPosition = point, button = PointerEventData.InputButton.Left,
        pointerPress = widget.Button.gameObject, rawPointerPress = widget.Button.gameObject,
        pointerDrag = scroll.gameObject, eligibleForClick = true,
        pointerPressRaycast = new RaycastResult { gameObject = widget.Button.gameObject, module = raycaster }
      };
      ExecuteEvents.Execute(widget.Button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
      var before = scroll.content.anchoredPosition;
      if (drag)
      {
        scroll.OnInitializePotentialDrag(pointer); scroll.OnBeginDrag(pointer);
        pointer.position += Vector2.up * 60; pointer.dragging = true; pointer.eligibleForClick = false;
        scroll.OnDrag(pointer);
        Assert.That(scroll.content.anchoredPosition, Is.Not.EqualTo(before), "This must exercise an actual native content drag.");
      }
      input.Send(pointer.position, false);
      ExecuteEvents.Execute(widget.Button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
      if (drag) scroll.OnEndDrag(pointer);
      else ExecuteEvents.Execute(widget.Button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
      Assert.That(calls, Is.EqualTo(drag ? 0 : 1));
      Assert.That(session.IsOpen, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Stationary_native_hover_cannot_steal_semantic_scroll_selection_but_real_pointer_input_can(bool controller)
    {
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      var pointAction = asset.AddActionMap("Hover owner").AddAction("Point", InputActionType.PassThrough,
        "<Mouse>/position", expectedControlLayout: "Vector2");
      var reference = Own(InputActionReference.Create(pointAction));
      profile.InputBindings.Configure(point: reference, routePointerClick: true);
      Scrolling(); Open(Commands(18));
      for (int i = 0; i < 10; i++)
      {
        presenter.Navigate(Vector2.down, controller);
        if (controller) presenter.Navigate(Vector2.zero, true);
      }
      var mode = controller ? MenuInteractionMode.Controller : MenuInteractionMode.Keyboard;
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item10"));
      var underMouse = Widget("item6");
      Vector2 position = RectTransformUtility.WorldToScreenPoint(camera,
        underMouse.transform.TransformPoint(((RectTransform)underMouse.transform).rect.center));
      var stationary = new PointerEventData(EventSystem.current) { position = position, delta = Vector2.zero };
      // InputSystemUIInputModule sends these when layout moves rows beneath a stationary mouse.
      ExecuteEvents.Execute(underMouse.gameObject, stationary, ExecuteEvents.pointerEnterHandler);
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item10"));
      Assert.That(session.InteractionMode, Is.EqualTo(mode));
      AssertRevealed(Widget("item10"));
      presenter.Point(position);
      Assert.That(session.InteractionMode, Is.EqualTo(MenuInteractionMode.Pointer));
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item6"));
      presenter.Navigate(Vector2.down, false);
      var scroll = Scroll();
      var beforeWheel = scroll.content.anchoredPosition;
      ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(EventSystem.current)
        { position = position, scrollDelta = Vector2.down }, ExecuteEvents.scrollHandler);
      Assert.That(session.InteractionMode, Is.EqualTo(MenuInteractionMode.Pointer), "A real wheel gesture must hand off even without a Point value change.");
      Assert.That(scroll.content.anchoredPosition, Is.Not.EqualTo(beforeWheel));
      profile.InputBindings.Configure(routePointerClick: true);
      presenter.Navigate(Vector2.down, false);
      underMouse = Widget("item8");
      ExecuteEvents.Execute(underMouse.gameObject, stationary, ExecuteEvents.pointerEnterHandler);
      Assert.That(session.Selected.Id.Value, Is.EqualTo("item8"), "A profile without a Point binding retains native hover fallback.");
      Assert.That(session.InteractionMode, Is.EqualTo(MenuInteractionMode.Pointer));
      Assert.That(calls, Is.Zero);
    }

    private sealed class PointerScope : IDisposable
    {
      private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      private readonly InputSettings settings = InputSystem.settings;
      private readonly InputSettings.UpdateMode previousMode;
      private readonly bool previousFeature;
      private readonly Mouse previousMouse = Mouse.current;
      private readonly List<InputActionReference> references = new();
      private readonly InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
      private readonly Mouse mouse;
      public readonly InputAction Point, Click;
      public PointerScope()
      {
        previousMode = settings.updateMode;
        previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(settings, new object[] { PlayerUpdates });
        settings.SetInternalFeatureFlag(PlayerUpdates, true);
        settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        mouse = InputSystem.AddDevice<Mouse>(); asset.devices = new InputDevice[] { mouse };
        var map = asset.AddActionMap("Scrolling pointer");
        Point = map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
        Click = map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
      }
      public InputActionReference Reference(InputAction action)
      {
        var reference = InputActionReference.Create(action); references.Add(reference); return reference;
      }
      public void Send(Vector2 point, bool pressed)
      {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = pressed ? (ushort)1 : (ushort)0 });
        InputSystem.Update();
      }
      public void Dispose()
      {
        try
        {
          asset.Disable();
          if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
          foreach (var reference in references) Object.DestroyImmediate(reference);
          Object.DestroyImmediate(asset);
        }
        finally
        {
          settings.SetInternalFeatureFlag(PlayerUpdates, previousFeature); settings.updateMode = previousMode;
          if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
      }
    }
  }

  // Known broad-phase endpoint only; clipping proof is the real RectMask2D above, not a rendered-frame claim.
  [ExecuteAlways]
  public sealed class MenuListHitRaycaster : BaseRaycaster
  {
    public RectTransform Target;
    public Camera Camera;
    public override Camera eventCamera => Camera;
    public override int sortOrderPriority => int.MaxValue;
    public override int renderOrderPriority => int.MaxValue;
    public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
    {
      if (Target == null || !RectTransformUtility.RectangleContainsScreenPoint(Target, eventData.position, Camera)) return;
      results.Add(new RaycastResult { gameObject = Target.gameObject, module = this, index = results.Count,
        sortingOrder = int.MaxValue, depth = int.MaxValue, distance = 0, screenPosition = eventData.position });
    }
  }
}
