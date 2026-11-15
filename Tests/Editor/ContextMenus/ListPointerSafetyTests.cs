using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class ListPointerSafetyTests
  {
    private readonly List<Object> objects = new();
    private MenuSession session;
    private MenuPresentationContext context;
    private EventSystem previousEvents;
    private ListMenuPresenter presenter;
    private HitRaycaster raycaster;
    private int leafCalls;

    [TearDown]
    public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear(); context?.Dispose(); context = null; session?.Dispose(); session = null;
      if (previousEvents != null) EventSystem.current = previousEvents;
    }

    private void Open(bool disabled)
    {
      leafCalls = 0;
      previousEvents = EventSystem.current;
      var events = new GameObject("List pointer events", typeof(RectTransform), typeof(MenuPresenterTests.MenuTestEventSystem));
      objects.Add(events); EventSystem.current = events.GetComponent<EventSystem>();
      var canvasRoot = new GameObject("List pointer canvas", typeof(RectTransform), typeof(Canvas)); objects.Add(canvasRoot);
      var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
      ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(1200, 900);
      raycaster = canvasRoot.AddComponent<HitRaycaster>();
      var root = new GameObject("List", typeof(RectTransform)); root.transform.SetParent(canvas.transform, false);
      presenter = root.AddComponent<ListMenuPresenter>();
      var profile = ScriptableObject.CreateInstance<MenuProfile>(); objects.Add(profile);
      profile.InputBindings.Configure(routePointerClick: true);
      var leaf = new MenuAction("leaf", new MenuLabel("leaf"),
        new MenuCommandBehavior(_ => leafCalls++, MenuActionOutcome.Keep));
      var target = disabled
        ? new MenuAction("target", new MenuLabel("target"), new MenuCommandBehavior(_ => Assert.Fail("Disabled action executed")),
          state: _ => new MenuActionState(true, false))
        : new MenuAction("target", new MenuLabel("target"), new MenuHeaderBehavior());
      session = new MenuSession(); session.Open(new[] { leaf, target }, new MenuContext());
      context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback);
      presenter.Bind(session, profile, context);
      foreach (var widget in presenter.GetComponentsInChildren<MenuActionWidget>())
        if (widget.Label != null && widget.Label.text == "target") { raycaster.Target = (RectTransform)widget.transform; break; }
      Assert.That(raycaster.Target, Is.Not.Null);
      Assert.That(session.Selected.Id.Value, Is.EqualTo("leaf"));
    }

    private void PointAtTarget()
    {
      Vector2 point = RectTransformUtility.WorldToScreenPoint(null, raycaster.Target.TransformPoint(raycaster.Target.rect.center));
      presenter.Point(point);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HeaderOrDisabledRowNeverConfirmsThePreviousLeaf(bool disabled)
    {
      Open(disabled); PointAtTarget(); presenter.ConfirmPointer();
      Assert.That(leafCalls, Is.Zero, "A non-invokable row cannot reuse the previously selected gameplay command.");
      Assert.That(session.IsOpen, Is.True, "The row hit is not an outside-click dismissal.");
    }

    [Test]
    public void KeyboardNavigationAfterPointingAtAHeaderCanConfirmTheValidLeaf()
    {
      Open(false); PointAtTarget();
      presenter.Navigate(Vector2.down, false); presenter.Confirm();
      Assert.That(session.Selected.Id.Value, Is.EqualTo("leaf"));
      Assert.That(leafCalls, Is.EqualTo(1));
    }

    // This is a known endpoint for semantic safety, separate from rendered PlayMode raycaster proof.
    [ExecuteAlways]
    public sealed class HitRaycaster : BaseRaycaster
    {
      public RectTransform Target;
      public override Camera eventCamera => null;
      public override int sortOrderPriority => int.MaxValue;
      public override int renderOrderPriority => int.MaxValue;
      public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
      {
        if (Target == null || !RectTransformUtility.RectangleContainsScreenPoint(Target, eventData.position, null)) return;
        results.Add(new RaycastResult { gameObject = Target.gameObject, module = this, index = results.Count,
          sortingOrder = int.MaxValue, depth = int.MaxValue, distance = 0, screenPosition = eventData.position });
      }
    }
  }
}
