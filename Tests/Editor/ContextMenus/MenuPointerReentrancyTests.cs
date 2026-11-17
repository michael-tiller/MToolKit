using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
  // Selection callbacks may synchronously retire or replace a binding, even reusing its session and context.
  public sealed class MenuPointerReentrancyTests
  {
    private readonly List<Object> owned = new();
    private readonly List<MenuSession> sessions = new();
    private readonly List<MenuPresentationContext> contexts = new();
    private EventSystem previousEvents;
    private Canvas canvas;
    private Camera camera;
    private RadialMenuPresenter presenter;
    private MenuProfile profile;
    private MenuSession original;
    private MenuPresentationContext originalContext;
    private MenuAction first, hovered;
    private int commands;

    [SetUp]
    public void Setup()
    {
      commands = 0;
      previousEvents = EventSystem.current;
      var events = Own(new GameObject("Reentrant point events", typeof(ReentrantPointEventSystem)));
      EventSystem.current = events.GetComponent<EventSystem>();
      var cameraObject = Own(new GameObject("Reentrant point camera", typeof(Camera)));
      camera = cameraObject.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 900;
      camera.transform.position = new Vector3(0, 0, -10);
      var canvasObject = Own(new GameObject("Reentrant point canvas", typeof(RectTransform), typeof(Canvas)));
      canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(2400, 1800);
      var item = Own(new GameObject("Owned counting item template", typeof(RectTransform), typeof(CountingItem)));
      item.SetActive(false);
      profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.ItemPrefab = item; profile.DrawSegments = false; profile.ShowCenter = false;
      profile.ShowBackControl = false; profile.Backdrop = false; profile.Anchor = MenuAnchorPolicy.Centered;
      profile.InnerRadius = 100; profile.RingWidth = 60; profile.WidgetSize = new Vector2(96, 56);
      profile.MaximumVisibleItems = 8;
      first = Command("first"); hovered = Command("hovered");
      original = NewSession(first, hovered); original.InteractionMode = MenuInteractionMode.Pointer;
      originalContext = NewContext();
      var root = new GameObject("Reentrant point presenter", typeof(RectTransform));
      root.transform.SetParent(canvas.transform, false);
      presenter = root.AddComponent<RadialMenuPresenter>();
      presenter.Bind(original, profile, originalContext);
      Assert.That(original.Selected, Is.SameAs(first), "Begin with a different selected action so the actual hover invokes Changed.");
    }

    [TearDown]
    public void Cleanup()
    {
      if (presenter != null) presenter.Unbind();
      foreach (var session in sessions) session.Dispose();
      foreach (var context in contexts) context.Dispose();
      sessions.Clear(); contexts.Clear();
      for (int i = owned.Count - 1; i >= 0; i--)
        if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      if (previousEvents != null && previousEvents.isActiveAndEnabled) EventSystem.current = previousEvents;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Pointer_selection_callback_may_unbind_or_close_without_stale_presenter_access(bool closeSession)
    {
      bool called = false;
      Action handler = () =>
      {
        if (called || original.Selected != hovered) return;
        called = true;
        if (closeSession) original.Close(); else presenter.Unbind();
      };
      original.Changed += handler;
      try
      {
        Vector2 point = HoveredScreenPoint();
        Assert.DoesNotThrow(() => presenter.Point(point),
          "Selecting a real Pie item can synchronously retire the binding through Session.Changed.");
        Assert.That(called, Is.True, "Exercise the actual Point→Select→Changed path.");
        Assert.That(presenter.VisibleWidgetCount, Is.Zero);
        Assert.That(presenter.InnerGeometry, Is.Null, "The retired geometry must stay detached.");
        Assert.That(original.IsOpen, Is.EqualTo(!closeSession));
        Assert.That(commands, Is.Zero, "Hover is navigation, never command execution.");
      }
      finally { original.Changed -= handler; }
    }

    [TestCase(Replacement.DifferentSessionAndContext)]
    [TestCase(Replacement.SameSessionNewContext)]
    [TestCase(Replacement.SameSessionAndContext)]
    public void Pointer_selection_callback_cannot_continue_rendering_into_a_replacement_binding(Replacement replacement)
    {
      bool called = false;
      int bindsAtReplacement = -1;
      MenuSession replacementSession = null;
      MenuPresentationContext replacementContext = null;
      MenuProfile replacementProfile = null;
      object replacementGeometry = null;
      Action handler = () =>
      {
        if (called || original.Selected != hovered) return;
        called = true;
        // Reuse the same descriptor deliberately. A guard that checks only Session identity or selected-action reference is insufficient.
        replacementSession = replacement == Replacement.DifferentSessionAndContext
          ? NewSession(first, Command("replacement-other")) : original;
        if (replacementSession == original) Assert.That(original.Select(first.Id), Is.True);
        replacementSession.InteractionMode = MenuInteractionMode.Pointer;
        replacementContext = replacement == Replacement.SameSessionAndContext ? originalContext : NewContext();
        replacementProfile = replacement == Replacement.SameSessionAndContext ? profile : Own(Object.Instantiate(profile));
        if (replacementProfile != profile)
        {
          replacementProfile.InnerRadius = 140;
          // Unity cloning serializes an unassigned typed wrapper as an empty reference. This test binds a component directly.
          replacementProfile.PresenterReference = null;
        }
        Assert.That(replacementProfile.Validate(out var diagnostic, requirePresenter: false), Is.True, diagnostic);
        presenter.Bind(replacementSession, replacementProfile, replacementContext);
        replacementGeometry = presenter.InnerGeometry;
        bindsAtReplacement = TotalBinds();
      };
      original.Changed += handler;
      try
      {
        Vector2 point = HoveredScreenPoint();
        Assert.DoesNotThrow(() => presenter.Point(point));
        Assert.That(called, Is.True, "Exercise the actual reentrant Point callback.");
        Assert.That(replacementSession.Selected, Is.SameAs(first), "Old hover must not select anything in the replacement.");
        Assert.That(replacementSession.Path.Count, Is.Zero);
        Assert.That(presenter.InnerGeometry, Is.SameAs(replacementGeometry), "Old geometry operations must not reach the replacement.");
        Assert.That(TotalBinds(), Is.EqualTo(bindsAtReplacement),
          "The previous Point call must stop after rebinding, including same Session+Context identity.");
        Assert.That(commands, Is.Zero);
      }
      finally { original.Changed -= handler; }
    }

    [TestCase(NativeEntry.Hover, false)]
    [TestCase(NativeEntry.Hover, true)]
    [TestCase(NativeEntry.Select, false)]
    [TestCase(NativeEntry.Select, true)]
    public void Native_widget_callback_revalidates_its_binding_after_external_selection_handlers(NativeEntry entry, bool rebind)
    {
      // SelectWidget must clear a real prior pointer hover, so its post-callback condition is exercised.
      presenter.GetComponentsInChildren<CountingItem>().Single(item => item.Action == first).State.HoverChanged(true);
      bool called = false;
      int bindsAtReplacement = -1;
      object replacementGeometry = null;
      Action handler = () =>
      {
        if (called || original.Selected != hovered) return;
        called = true;
        if (!rebind) { presenter.Unbind(); return; }
        Assert.That(original.Select(first.Id), Is.True);
        original.InteractionMode = MenuInteractionMode.Pointer;
        presenter.Bind(original, profile, originalContext);
        replacementGeometry = presenter.InnerGeometry;
        bindsAtReplacement = TotalBinds();
      };
      original.Changed += handler;
      try
      {
        var state = presenter.GetComponentsInChildren<CountingItem>().Single(item => item.Action == hovered).State;
        Assert.DoesNotThrow(() =>
        {
          if (entry == NativeEntry.Hover) state.HoverChanged(true);
          else state.Select();
        });
        Assert.That(called, Is.True, "Exercise the native widget's actual shared HoverWidget/SelectWidget callback.");
        if (rebind)
        {
          Assert.That(original.Selected, Is.SameAs(first));
          Assert.That(presenter.InnerGeometry, Is.SameAs(replacementGeometry));
          Assert.That(TotalBinds(), Is.EqualTo(bindsAtReplacement),
            "Same Session+Context reuse still requires a new binding identity; old callbacks cannot render the replacement.");
        }
        else
        {
          Assert.That(presenter.VisibleWidgetCount, Is.Zero);
          Assert.That(presenter.InnerGeometry, Is.Null);
        }
        Assert.That(commands, Is.Zero);
      }
      finally { original.Changed -= handler; }
    }
    [TestCase(ModeEntry.WidgetHover)]
    [TestCase(ModeEntry.WidgetSelect)]
    [TestCase(ModeEntry.SectorCommand)]
    [TestCase(ModeEntry.SectorHeader)]
    public void Interaction_mode_callback_cannot_select_or_change_feedback_in_a_reused_binding(ModeEntry entry)
    {
      bool sector = entry == ModeEntry.SectorCommand || entry == ModeEntry.SectorHeader;
      if (entry == ModeEntry.SectorHeader)
      {
        hovered = new MenuAction("header", new MenuLabel("Header"));
        original.Open(new[] { first, hovered }, new MenuContext());
      }
      if (sector || entry == ModeEntry.SectorHeader)
      {
        profile.DrawSegments = true;
        presenter.Bind(original, profile, originalContext);
      }
      original.InteractionMode = entry == ModeEntry.WidgetSelect ? MenuInteractionMode.Pointer : MenuInteractionMode.Keyboard;
      var targetMode = entry == ModeEntry.WidgetSelect ? MenuInteractionMode.Keyboard : MenuInteractionMode.Pointer;
      bool called = false;
      int bindsAtReplacement = -1;
      object geometryAtReplacement = null;
      Action handler = () =>
      {
        if (called || original.InteractionMode != targetMode || original.Selected != first) return;
        called = true;
        presenter.Bind(original, profile, originalContext);
        geometryAtReplacement = presenter.InnerGeometry;
        bindsAtReplacement = TotalBinds();
      };
      original.Changed += handler;
      try
      {
        var state = presenter.GetComponentsInChildren<CountingItem>().Single(item => item.Action == hovered).State;
        var pointer = sector
          ? presenter.GetComponentsInChildren<NativeRadialGraphic>().Single(graphic => graphic.Geometry.SegmentIndex == 1).GetComponent<MenuSectorPointer>()
          : null;
        Assert.DoesNotThrow(() =>
        {
          if (entry == ModeEntry.WidgetHover) state.HoverChanged(true);
          else if (entry == ModeEntry.WidgetSelect) state.Select();
          else pointer.OnPointerEnter(new PointerEventData(EventSystem.current));
        });
        Assert.That(called, Is.True, "Rebind inside InteractionMode.Changed before the old action selector runs.");
        Assert.That(original.Selected, Is.SameAs(first), "The old selector/header cannot change the replacement selection.");
        Assert.That(original.Path.Count, Is.Zero);
        Assert.That(presenter.InnerGeometry, Is.SameAs(geometryAtReplacement));
        Assert.That(TotalBinds(), Is.EqualTo(bindsAtReplacement), "No old callback refresh may reach the new generation.");
        Assert.That(commands, Is.Zero);
        if (entry == ModeEntry.SectorHeader)
        {
          presenter.Confirm();
          Assert.That(commands, Is.EqualTo(1), "An old noninteractive hover must not poison the new binding's directional target.");
        }
      }
      finally { original.Changed -= handler; }
    }

    [Test]
    public void Point_mode_callback_cannot_clear_a_replacement_controls_pointer_ownership()
    {
      original.InteractionMode = MenuInteractionMode.Keyboard;
      var control = new GameObject("Owned external Back control", typeof(RectTransform), typeof(Button));
      control.transform.SetParent(canvas.transform, false);
      var raycaster = canvas.gameObject.AddComponent<ControlHitRaycaster>();
      raycaster.Camera = camera;
      bool called = false;
      Action handler = () =>
      {
        if (called || original.InteractionMode != MenuInteractionMode.Pointer || original.Selected != first) return;
        called = true;
        presenter.Bind(original, profile, originalContext);
        raycaster.Hit = control;
        presenter.Point(Vector2.zero);
        Assert.That(original.Select(first.Id), Is.True);
        var ownsControl = typeof(MenuPresenterBase).GetProperty("PointerControlOwnsInput", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((bool)ownsControl.GetValue(presenter), Is.True, "Prime actual control hit ownership in the replacement.");
        raycaster.Hit = null;
      };
      original.Changed += handler;
      try
      {
        Vector2 oldPoint = HoveredScreenPoint();
        Assert.DoesNotThrow(() => presenter.Point(oldPoint));
        Assert.That(called, Is.True);
        Assert.That(original.Selected, Is.SameAs(first));
        presenter.ConfirmPointer();
        Assert.That(commands, Is.Zero, "The old Point call must not erase replacement control ownership and activate a gameplay action.");
      }
      finally { original.Changed -= handler; }
    }
    private Vector2 HoveredScreenPoint()
    {
      var widget = presenter.GetComponentsInChildren<CountingItem>()
        .Single(item => item.Action == hovered);
      var rect = (RectTransform)widget.transform;
      return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    private int TotalBinds() => presenter.GetComponentsInChildren<CountingItem>(true).Sum(item => item.Binds);
    private MenuAction Command(string id)
      => new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(_ => commands++, MenuActionOutcome.Keep));
    private MenuSession NewSession(params MenuAction[] actions)
    {
      var session = new MenuSession(); sessions.Add(session); session.Open(actions, new MenuContext()); return session;
    }
    private MenuPresentationContext NewContext()
    {
      var context = new MenuPresentationContext(canvas, RectTransformUtility.WorldToScreenPoint(camera, Vector3.zero),
        labelOverride: label => label.Fallback);
      contexts.Add(context); return context;
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    public enum Replacement { DifferentSessionAndContext, SameSessionNewContext, SameSessionAndContext }
    public enum NativeEntry { Hover, Select }
    public enum ModeEntry { WidgetHover, WidgetSelect, SectorCommand, SectorHeader }

    public sealed class CountingItem : MonoBehaviour, IMenuWidget
    {
      public int Binds { get; private set; }
      public MenuAction Action { get; private set; }
      public MenuWidgetState State { get; private set; }
      public void Bind(MenuWidgetState state) { Binds++; State = state; Action = state?.Action; }
      public void Unbind() { State = null; Action = null; }
    }

    [ExecuteAlways]
    public sealed class ControlHitRaycaster : BaseRaycaster
    {
      public Camera Camera;
      public GameObject Hit;
      public override Camera eventCamera => Camera;
      public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
      {
        if (Hit != null) results.Add(new RaycastResult { gameObject = Hit, module = this, index = results.Count, distance = 0 });
      }
    }
    [ExecuteAlways]
    public sealed class ReentrantPointEventSystem : EventSystem
    {
      protected override void OnEnable() { base.OnEnable(); current = this; }
    }
  }
}