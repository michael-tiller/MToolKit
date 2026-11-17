using System;
using System.Collections;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuSelectionResetTests
  {
    private sealed class Fixture : IDisposable
    {
      public readonly MenuSession Session = new();
      public readonly MenuProfile Profile = ScriptableObject.CreateInstance<MenuProfile>();
      public readonly MenuPresenterBase Presenter;
      public readonly MenuAction First;
      public int Calls;
      private readonly GameObject canvasRoot;
      private readonly MenuPresentationContext context;
      public Fixture(int mode = 0, bool children = false, bool header = false)
      {
        canvasRoot = new GameObject("Selection reset canvas", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1600, 1200);
        var root = new GameObject("Selection reset presenter", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        Profile.Navigation = mode == 2 ? MenuNavigationPolicy.Replacement : mode == 3
          ? MenuNavigationPolicy.Concentric : MenuNavigationPolicy.Inline;
        Profile.DrawSegments = mode != 2;
        First = new MenuAction("first", new MenuLabel("First"), new MenuCommandBehavior(_ => Calls++, MenuActionOutcome.Keep),
          children: children ? new[] { new MenuAction("child", new MenuLabel("Child"), new MenuCommandBehavior(_ => Calls++)) } : null);
        Session.Open(new[] { First, header ? new MenuAction("header", new MenuLabel("Header"))
          : new MenuAction("second", new MenuLabel("Second"), new MenuCommandBehavior(_ => Calls++, MenuActionOutcome.Keep)) }, new MenuContext());
        context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback);
        Presenter = mode == 0 ? (MenuPresenterBase)root.AddComponent<ListMenuPresenter>() : root.AddComponent<RadialMenuPresenter>();
        Presenter.Bind(Session, Profile, context);
        Canvas.ForceUpdateCanvases();
      }
      public void PointAtFirst()
      {
        Vector3 point = Presenter is RadialMenuPresenter radial && Profile.DrawSegments
          ? Presenter.Root.TransformPoint(radial.InnerGeometry.Segments[0].ContentAnchor)
          : Presenter.GetComponentInChildren<MenuActionWidget>().transform.position;
        Presenter.Point(RectTransformUtility.WorldToScreenPoint(null, point));
        Assert.That(Session.Selected, Is.SameAs(First));
      }
      public void Dispose()
      { Presenter.Unbind(); Object.DestroyImmediate(canvasRoot); context.Dispose(); Session.Dispose(); Object.DestroyImmediate(Profile); }
    }

    private static void Clear(MenuSession session)
      => session.ClearSelection();
    private static IEnumerator Wait(float seconds)
    { double until = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < until) yield return null; }

    [Test]
    public void Explicit_empty_selection_survives_action_and_provider_refresh()
    {
      using var fixture = new Fixture();
      Clear(fixture.Session);
      Assert.That(fixture.Session.Selected, Is.Null);
      fixture.First.NotifyChanged(); fixture.Session.RefreshAsync().GetAwaiter().GetResult();
      Assert.That(fixture.Session.Selected, Is.Null, "Refresh must not silently restore the first action.");
      fixture.Session.ActivateAsync().GetAwaiter().GetResult();
      Assert.That(fixture.Calls, Is.Zero);
      Assert.That(fixture.Session.Select("first"), Is.True);
      Assert.That(fixture.Session.Selected, Is.SameAs(fixture.First));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Pointer_leaving_actions_clears_selection_and_center(int mode)
    {
      using var fixture = new Fixture(mode);
      fixture.PointAtFirst();
      fixture.Presenter.Point(new Vector2(-10000, -10000));
      Assert.That(fixture.Session.Selected, Is.Null);
      var center = fixture.Presenter.GetComponentInChildren<MenuCenterWidget>(true);
      Assert.That(center == null || !center.gameObject.activeSelf, Is.True);
      fixture.Presenter.Confirm();
      Assert.That(fixture.Calls, Is.Zero);
      fixture.PointAtFirst();
    }

    [Test]
    public void Clearing_concentric_child_selection_preserves_the_parent_path()
    {
      using var fixture = new Fixture(3, true);
      fixture.Session.EnterSubmenuAsync("first").GetAwaiter().GetResult();
      fixture.Presenter.Point(new Vector2(-10000, -10000));
      Assert.That(fixture.Session.Selected, Is.Null);
      Assert.That(fixture.Session.ParentSelected, Is.SameAs(fixture.First));
      Assert.That(fixture.Session.Path.Count, Is.EqualTo(1));
      Assert.That(((RadialMenuPresenter)fixture.Presenter).OuterGeometry, Is.Not.Null);
    }

    [Test]
    public void Cleared_concentric_selection_can_refocus_the_child_ring_from_its_parent()
    {
      using var fixture = new Fixture(3, true);
      fixture.Session.EnterSubmenuAsync("first").GetAwaiter().GetResult();
      Clear(fixture.Session);
      var radial = (RadialMenuPresenter)fixture.Presenter;
      radial.SwitchRing();
      var direction = radial.InnerGeometry.Segments[0].ContentAnchor.normalized;
      radial.Navigate(direction, true);
      Assert.That(radial.FocusedRing, Is.Zero);
      radial.Confirm();
      Assert.That(radial.FocusedRing, Is.EqualTo(1), "Parent confirmation changes ring focus even with no selected child.");
      Assert.That(fixture.Calls, Is.Zero);
      radial.Navigate(radial.OuterGeometry.Segments[0].ContentAnchor.normalized, true);
      Assert.That(fixture.Session.Selected?.Id.Value, Is.EqualTo("child"));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Native_item_pointer_exit_resolves_the_actual_destination(int mode)
    {
      using var fixture = new Fixture(mode);
      fixture.PointAtFirst();
      var widget = fixture.Presenter.GetComponentInChildren<MenuActionWidget>();
      widget.OnPointerExit(new PointerEventData(EventSystem.current) { position = new Vector2(-10000, -10000) });
      Assert.That(fixture.Session.Selected, Is.Null);
    }

    [Test]
    public void Native_sector_exit_to_its_own_label_preserves_selection()
    {
      using var fixture = new Fixture(1);
      fixture.PointAtFirst();
      var radial = (RadialMenuPresenter)fixture.Presenter;
      Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
        radial.Root.TransformPoint(radial.InnerGeometry.Segments[0].ContentAnchor));
      var sector = radial.GetComponentInChildren<MenuSectorPointer>();
      sector.OnPointerExit(new PointerEventData(EventSystem.current) { position = point });
      Assert.That(fixture.Session.Selected, Is.SameAs(fixture.First));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Hovering_a_noninteractive_header_clears_the_previous_action(int mode)
    {
      using var fixture = new Fixture(mode, header: true);
      fixture.PointAtFirst();
      foreach (var widget in fixture.Presenter.GetComponentsInChildren<MenuActionWidget>())
      {
        if (widget.Label == null || widget.Label.text != "Header") continue;
        fixture.Presenter.Point(RectTransformUtility.WorldToScreenPoint(null, widget.transform.position));
        Assert.That(fixture.Session.Selected, Is.Null);
        return;
      }
      Assert.Fail("The noninteractive header must be visible.");
    }

    [Test]
    public void Keyboard_reacquires_first_or_last_action_from_empty_selection()
    {
      using var fixture = new Fixture();
      Clear(fixture.Session); fixture.Presenter.Navigate(Vector2.down, false);
      Assert.That(fixture.Session.Selected, Is.SameAs(fixture.First));
      Clear(fixture.Session); fixture.Presenter.Navigate(Vector2.up, false);
      Assert.That(fixture.Session.Selected?.Id.Value, Is.EqualTo("second"));
    }

    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void Invalid_neutral_reset_delay_has_a_useful_diagnostic(float value)
    {
      using var fixture = new Fixture(1);
      fixture.Profile.AnalogSelectionResetDelay = value;
      Assert.That(fixture.Profile.Validate(out var diagnostic, requirePresenter: false), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.AnalogSelectionResetDelay)));
    }

    [Test]
    public void Zero_delay_clears_selection_immediately()
    {
      using var fixture = new Fixture(1);
      fixture.Profile.AnalogSelectionResetDelay = 0;
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      Assert.That(fixture.Session.Selected, Is.Null);
    }

    [UnityTest]
    public IEnumerator Neutral_stick_clears_selection_after_unscaled_grace()
    {
      using var fixture = new Fixture(1);
      float previousScale = Time.timeScale;
      try
      {
        Time.timeScale = 0;
        fixture.Presenter.Navigate(Vector2.right, true);
        fixture.Presenter.Navigate(Vector2.zero, true);
        Assert.That(fixture.Session.Selected, Is.Not.Null, "Brief neutral retains visual context.");
        yield return Wait(.6f);
        Assert.That(fixture.Session.Selected, Is.Null);
        var center = fixture.Presenter.GetComponentInChildren<MenuCenterWidget>(true);
        Assert.That(center == null || !center.gameObject.activeSelf, Is.True);
      }
      finally { Time.timeScale = previousScale; }
    }

    [UnityTest]
    public IEnumerator Leaving_neutral_cancels_the_pending_reset()
    {
      using var fixture = new Fixture(1);
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      fixture.Presenter.Navigate(Vector2.left, true);
      var selected = fixture.Session.Selected;
      yield return Wait(.6f);
      Assert.That(fixture.Session.Selected, Is.SameAs(selected));
    }

    [UnityTest]
    public IEnumerator Availability_refresh_during_neutral_does_not_prevent_reset()
    {
      using var fixture = new Fixture(1);
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      fixture.First.NotifyChanged(); fixture.Session.RefreshAsync().GetAwaiter().GetResult();
      yield return Wait(.6f);
      Assert.That(fixture.Session.Selected, Is.Null);
    }

    [UnityTest]
    public IEnumerator Configured_longer_grace_is_respected()
    {
      using var fixture = new Fixture(1);
      fixture.Profile.AnalogSelectionResetDelay = .8f;
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      yield return Wait(.5f);
      Assert.That(fixture.Session.Selected, Is.Not.Null);
      yield return Wait(.5f);
      Assert.That(fixture.Session.Selected, Is.Null);
    }

    [UnityTest]
    public IEnumerator Late_neutral_callback_cannot_erase_pointer_selection()
    {
      using var fixture = new Fixture(1);
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      fixture.PointAtFirst(); fixture.Presenter.Navigate(Vector2.zero, true);
      yield return Wait(.6f);
      Assert.That(fixture.Session.Selected, Is.SameAs(fixture.First));
      Assert.That(fixture.Session.InteractionMode, Is.EqualTo(MenuInteractionMode.Pointer));
    }

    [UnityTest]
    public IEnumerator Keyboard_selection_cancels_the_pending_analog_reset()
    {
      using var fixture = new Fixture(1);
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      fixture.Presenter.Navigate(Vector2.down, false);
      var selected = fixture.Session.Selected;
      yield return Wait(.6f);
      Assert.That(fixture.Session.Selected, Is.SameAs(selected));
    }

    [UnityTest]
    public IEnumerator Retired_presenter_timer_cannot_clear_a_replacement_session()
    {
      using var fixture = new Fixture(1);
      fixture.Presenter.Navigate(Vector2.right, true); fixture.Presenter.Navigate(Vector2.zero, true);
      fixture.Presenter.Unbind();
      using var replacement = new MenuSession();
      replacement.Open(new[] { new MenuAction("replacement", new MenuLabel("Replacement"), new MenuCommandBehavior(_ => { })) }, new MenuContext());
      var canvas = fixture.Presenter.GetComponentInParent<Canvas>();
      using var context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback);
      fixture.Presenter.Bind(replacement, fixture.Profile, context);
      yield return Wait(.6f);
      Assert.That(replacement.Selected?.Id.Value, Is.EqualTo("replacement"));
      fixture.Presenter.Unbind();
    }

    [Test]
    public void Neutral_grace_never_permits_hold_release_to_execute_a_stale_target()
    {
      using var fixture = new Fixture(1);
      fixture.Presenter.BeginHold(); fixture.Presenter.Navigate(Vector2.right, true);
      fixture.Presenter.Navigate(Vector2.zero, true); fixture.Presenter.EndHold();
      Assert.That(fixture.Calls, Is.Zero);
      Assert.That(fixture.Session.IsOpen, Is.False);
    }
  }
}
