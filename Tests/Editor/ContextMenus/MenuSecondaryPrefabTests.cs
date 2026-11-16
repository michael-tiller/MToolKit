using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuSecondaryPrefabTests
  {
    private readonly List<Object> owned = new();
    private MenuSession session;
    private MenuPresentationContext context;

    [TearDown]
    public void Cleanup()
    {
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear(); context?.Dispose(); context = null; session?.Dispose(); session = null;
    }

    private GameObject Root(string name, Transform parent = null)
    {
      var root = new GameObject(name, typeof(RectTransform));
      if (parent == null) owned.Add(root); else root.transform.SetParent(parent, false);
      return root;
    }

    private GameObject Secondary(string name)
    {
      var root = Root(name);
      var button = root.AddComponent<Button>();
      root.AddComponent<MenuActionWidget>().Configure(null, null, null, null, button);
      return root;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FactoryProvidesIndependentOptionalIconAndImageTargets(bool center)
    {
      var parent = Root("Visual channel parent");
      using var instance = center ? MenuWidgetFactory.CreateCenter(parent.transform) : MenuWidgetFactory.CreateItem(parent.transform);
      var widget = instance.DefaultWidget;
      Assert.That(widget.Icon, Is.Not.Null, "Default action and center widgets both support a compact icon.");
      var imageProperty = typeof(MenuActionWidget).GetProperty("ActionImage");
      Assert.That(imageProperty, Is.Not.Null, "An action's artwork needs an independent optional binding target.");
      var artwork = imageProperty.GetValue(widget) as Image;
      Assert.That(artwork, Is.Not.Null);
      Assert.That(artwork, Is.Not.SameAs(widget.Icon));
      Assert.That(artwork.raycastTarget, Is.False, "Artwork cannot replace native wedge hit testing.");
      Assert.That(widget.Icon.raycastTarget, Is.False);
      Assert.That(artwork.preserveAspect, Is.True);
      Assert.That(widget.Icon.preserveAspect, Is.True);
    }

    [Test]
    public void AuthoredDefaultCenterUsesProfileSecondaryPrefabAndKeepsExecutionShared()
    {
      int primary = 0, auxiliary = 0;
      var original = Secondary("Authored auxiliary");
      var replacement = Secondary("Profile auxiliary");
      var prefab = Root("Authored default center");
      var center = prefab.AddComponent<MenuCenterWidget>();
      var container = Root("Secondary controls", prefab.transform);
      center.Configure(null, null, secondaryContainer: (RectTransform)container.transform, secondaryPrefab: original);
      var presenter = Presenter(prefab, replacement, () => primary++, () => auxiliary++);
      var instance = presenter.GetComponentInChildren<MenuCenterWidget>();
      Assert.That(instance.SecondaryPrefab, Is.SameAs(replacement), "An authored MenuCenterWidget explicitly opts into profile composition.");
      Assert.That(instance.SecondaryContainer.childCount, Is.EqualTo(1));
      var control = instance.SecondaryContainer.GetChild(0);
      Assert.That(control.name, Does.StartWith(replacement.name));
      control.GetComponent<Button>().onClick.Invoke();
      Assert.That(primary, Is.Zero); Assert.That(auxiliary, Is.EqualTo(1));
    }

    [Test]
    public void FactoryForwardsOverrideOnlyToAnExplicitlyOptedInCustomRoot()
    {
      var parent = Root("Parent");
      var prefab = Root("Opt-in custom center"); prefab.AddComponent<OptInCenter>();
      var replacement = Secondary("Profile auxiliary");
      using var instance = MenuWidgetFactory.CreateCenter(parent.transform, prefab, replacement);
      var receiver = instance.GameObject.GetComponent<OptInCenter>();
      Assert.That(receiver.Configurations, Is.EqualTo(1));
      Assert.That(receiver.Prefab, Is.SameAs(replacement));
      Assert.That(instance.DefaultWidget, Is.Null, "An opt-in custom center remains fully custom.");
    }

    [Test]
    public void CustomRootWithoutOptInKeepsItsOwnComposition()
    {
      var parent = Root("Parent");
      var prefab = Root("Independent custom center"); prefab.AddComponent<IndependentCenter>();
      var child = Root("Private layout", prefab.transform);
      var childRect = (RectTransform)child.transform;
      childRect.anchoredPosition = new Vector2(37, 19);
      childRect.sizeDelta = new Vector2(74, 38);
      var replacement = Secondary("Profile auxiliary");
      using var instance = MenuWidgetFactory.CreateCenter(parent.transform, prefab, replacement);
      Assert.That(instance.GameObject.GetComponent<MenuCenterWidget>(), Is.Null);
      Assert.That(instance.Rect.childCount, Is.EqualTo(1));
      var clonedChild = (RectTransform)instance.Rect.GetChild(0);
      Assert.That(clonedChild.anchoredPosition, Is.EqualTo(new Vector2(37, 19)));
      Assert.That(clonedChild.sizeDelta, Is.EqualTo(new Vector2(74, 38)));
    }

    [Test]
    public void MissingProfileOverridePreservesAuthoredSecondaryTemplate()
    {
      var parent = Root("Parent"); var original = Secondary("Authored auxiliary");
      var prefab = Root("Authored default center");
      prefab.AddComponent<MenuCenterWidget>().SecondaryPrefab = original;
      using var instance = MenuWidgetFactory.CreateCenter(parent.transform, prefab);
      Assert.That(instance.GameObject.GetComponent<MenuCenterWidget>().SecondaryPrefab, Is.SameAs(original));
    }

    private RadialMenuPresenter Presenter(GameObject centerPrefab, GameObject secondaryPrefab, Action primary, Action auxiliary)
    {
      var canvasRoot = Root("Menu canvas"); var canvas = canvasRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(1200, 900);
      var root = Root("Presenter", canvasRoot.transform); var presenter = root.AddComponent<RadialMenuPresenter>();
      var profile = ScriptableObject.CreateInstance<MenuProfile>(); owned.Add(profile);
      profile.CenterPrefab = centerPrefab; profile.SecondaryPrefab = secondaryPrefab;
      session = new MenuSession();
      var secondary = new MenuAction("secondary", new MenuLabel("Secondary"), new MenuCommandBehavior(_ => auxiliary(), MenuActionOutcome.Keep));
      var action = new MenuAction("owner", new MenuLabel("Owner"), new MenuCommandBehavior(_ => primary(), MenuActionOutcome.Keep),
        secondaryActions: new[] { secondary });
      session.Open(new[] { action }, new MenuContext());
      context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback);
      presenter.Bind(session, profile, context);
      return presenter;
    }

    public sealed class OptInCenter : MonoBehaviour, IMenuWidget, IMenuSecondaryPrefabReceiver
    {
      public GameObject Prefab; public int Configurations;
      public void ConfigureSecondaryPrefab(GameObject prefab) { Prefab = prefab; Configurations++; }
      public void Bind(MenuWidgetState state) { }
      public void Unbind() { }
    }
    public sealed class IndependentCenter : MonoBehaviour, IMenuWidget
    {
      public void Bind(MenuWidgetState state) { }
      public void Unbind() { }
    }
  }
}
