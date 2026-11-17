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
  public sealed class ThrowingMenuCleanupWidget : MonoBehaviour, IMenuWidget
  {
    public bool ThrowOnUnbind = true;
    public int UnbindCalls;
    public void Bind(MenuWidgetState state) { }
    public void Unbind()
    {
      UnbindCalls++;
      if (ThrowOnUnbind) throw new InvalidOperationException("first custom widget cleanup failure");
    }
  }
  public sealed class MenuCleanupProbeWidget : MonoBehaviour, IMenuWidget
  {
    public int UnbindCalls;
    public void Bind(MenuWidgetState state) { }
    public void Unbind() { UnbindCalls++; }
  }
  [ExecuteAlways]
  public sealed class MenuCleanupEventSystem : EventSystem { }
  public sealed class MenuCleanupPresenter : MenuPresenterBase
  {
    public bool Bound => IsBound;
    protected override void Render(bool forceLayout)
    {
      for (int i = 0; i < Session.Rows.Count; i++)
      {
        var action = Session.Rows[i].Action;
        Item(i).Bind(StateFor(action, Session.Selected == action));
      }
      FinishItems(Session.Rows.Count);
      BindCenter(true);
    }
  }

  public sealed class MenuWidgetCleanupTests
  {
    private readonly List<Object> owned = new();
    private readonly List<MenuWidgetInstance> instances = new();
    private MenuCleanupPresenter presenter;
    private MenuPresentationContext context;
    private MenuSession session;
    private EventSystem previousEvents;
    private T Own<T>(T asset) where T : Object { owned.Add(asset); return asset; }
    [SetUp] public void Setup() => previousEvents = EventSystem.current;
    [TearDown]
    public void Cleanup()
    {
      // A RED must not poison the shared editor's navigation state for the next test.
      foreach (var asset in owned)
        if (asset is GameObject owner && owner != null)
          foreach (var widget in owner.GetComponentsInChildren<ThrowingMenuCleanupWidget>(true)) widget.ThrowOnUnbind = false;
      foreach (var instance in instances)
        if (instance.GameObject != null)
          foreach (var widget in instance.GameObject.GetComponentsInChildren<ThrowingMenuCleanupWidget>(true)) widget.ThrowOnUnbind = false;
      presenter?.Unbind(); presenter = null;
      foreach (var instance in instances) instance.Dispose();
      instances.Clear();
      context?.Dispose(); context = null;
      session?.Dispose(); session = null;
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      if (previousEvents != null) EventSystem.current = previousEvents;
    }
    private GameObject Template(string name, bool throws)
    {
      var root = Own(new GameObject(name, typeof(RectTransform)));
      if (throws) root.AddComponent<ThrowingMenuCleanupWidget>();
      root.AddComponent<MenuCleanupProbeWidget>();
      return root;
    }
    private MenuWidgetInstance Instance()
    {
      var parent = Own(new GameObject("Widget cleanup owner", typeof(RectTransform)));
      var template = Template("Custom cleanup template", true);
      var instance = MenuWidgetFactory.CreateItem(parent.transform, template);
      instances.Add(instance); return instance;
    }

    [Test]
    public void Widget_unbind_attempts_every_component_and_preserves_the_first_failure()
    {
      var instance = Instance();
      var probe = instance.GameObject.GetComponent<MenuCleanupProbeWidget>();
      var error = Assert.Throws<InvalidOperationException>(() => instance.Unbind());
      Assert.That(error.Message, Is.EqualTo("first custom widget cleanup failure"));
      Assert.That(probe.UnbindCalls, Is.EqualTo(1), "The first custom widget exception skipped the remaining component's cleanup");
      Assert.That(instance.GameObject.activeSelf, Is.False);
    }

    [Test]
    public void Widget_dispose_destroys_the_owned_root_even_when_custom_unbind_throws()
    {
      var instance = Instance();
      var root = instance.GameObject;
      var error = Assert.Throws<InvalidOperationException>(() => instance.Dispose());
      Assert.That(error.Message, Is.EqualTo("first custom widget cleanup failure"));
      Assert.That(root == null, Is.True, "A throwing widget Unbind orphaned the framework-owned instance");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Presenter_cleanup_restores_navigation_and_cleans_all_items_and_center_when_one_widget_throws(bool centerThrows)
    {
      var events = Own(new GameObject("Cleanup event system", typeof(MenuCleanupEventSystem))).GetComponent<MenuCleanupEventSystem>();
      EventSystem.current = events; events.sendNavigationEvents = true;
      var canvas = Own(new GameObject("Cleanup canvas", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var previousFocus = Own(new GameObject("Previous cleanup focus", typeof(RectTransform)));
      previousFocus.transform.SetParent(canvas.transform, false);
      events.SetSelectedGameObject(previousFocus);
      var profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.Backdrop = false; profile.ShowCenter = true;
      profile.ItemPrefab = Template("Item cleanup template", !centerThrows);
      profile.CenterPrefab = Template("Center cleanup template", centerThrows);
      var presenterOwner = Own(new GameObject("Cleanup presenter", typeof(RectTransform), typeof(MenuCleanupPresenter)));
      presenterOwner.transform.SetParent(canvas.transform, false);
      presenter = presenterOwner.GetComponent<MenuCleanupPresenter>();
      session = new MenuSession();
      session.Open(new[]
      {
        new MenuAction("one", new MenuLabel("One"), new MenuCommandBehavior(_ => { })),
        new MenuAction("two", new MenuLabel("Two"), new MenuCommandBehavior(_ => { }))
      }, new MenuContext());
      context = new MenuPresentationContext(canvas, Vector2.zero);
      presenter.Bind(session, profile, context);
      var probes = presenter.Root.GetComponentsInChildren<MenuCleanupProbeWidget>(true);
      Assert.That(probes.Length, Is.EqualTo(3));
      Assert.That(events.sendNavigationEvents, Is.False, "Semantic presenter did not acquire the real EventSystem navigation lease");
      Assert.DoesNotThrow(() => presenter.Unbind(), "Custom cleanup failure escaped before framework ownership retired");
      foreach (var probe in probes) Assert.That(probe.UnbindCalls, Is.EqualTo(1), "A later item/center component was skipped");
      Assert.That(events.sendNavigationEvents, Is.True, "A throwing widget stranded the framework navigation lease");
      Assert.That(events.currentSelectedGameObject, Is.SameAs(previousFocus));
      Assert.That(presenter.Bound, Is.False);
      Assert.That(presenter.Root.GetComponentsInChildren<MenuCleanupProbeWidget>(true), Is.Empty);
    }
  }
}
