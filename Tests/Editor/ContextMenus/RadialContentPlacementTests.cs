using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class RadialPlacementCenterWidget : MonoBehaviour, IMenuWidget
  {
    public GameObject Auxiliary;
    public void Bind(MenuWidgetState state)
    { if (Auxiliary != null) Auxiliary.SetActive(state != null && state.SecondaryStates.Count > 0); }
    public void Unbind() { }
  }

  public sealed class RadialContentPlacementTests
  {
    private readonly List<Object> owned = new();
    private MenuSession session;
    private MenuPresentationContext context;
    private Canvas canvas;
    private Camera camera;
    private MenuProfile profile;

    private T Own<T>(T asset) where T : Object { owned.Add(asset); return asset; }
    [SetUp]
    public void Setup()
    {
      camera = Own(new GameObject("Radial content camera", typeof(Camera))).GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 500;
      camera.transform.position = new Vector3(0, 0, -10);
      canvas = Own(new GameObject("Radial content viewport", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(1000, 800);
      profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.InnerRadius = 90; profile.RingWidth = 60; profile.ChildRingWidth = 60;
      profile.WidgetSize = new Vector2(40, 40); profile.MinimumTargetSize = 32;
      profile.ShowCenter = true; profile.Backdrop = false;
      profile.Anchor = MenuAnchorPolicy.Centered;
    }
    [TearDown]
    public void Cleanup()
    {
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      context?.Dispose(); context = null;
      session?.Dispose(); session = null;
    }
    private GameObject Center(Vector2 size)
    {
      var root = Own(new GameObject("Authored center", typeof(RectTransform), typeof(Image), typeof(RadialPlacementCenterWidget)));
      ((RectTransform)root.transform).sizeDelta = size;
      return root;
    }
    private RadialMenuPresenter Bind(Vector2 requested, Vector3? scale = null)
    {
      var owner = Own(new GameObject("Radial content presenter", typeof(RectTransform), typeof(RadialMenuPresenter)));
      owner.transform.SetParent(canvas.transform, false);
      if (scale.HasValue) owner.transform.localScale = scale.Value;
      session = new MenuSession();
      var auxiliary = new MenuAction("variant", new MenuLabel("Variant"), new MenuCommandBehavior(_ => { }, MenuActionOutcome.Keep));
      var command = new MenuAction("command", new MenuLabel("Command"), new MenuCommandBehavior(_ => { }), secondaryActions: new[] { auxiliary });
      session.Open(new[] { command }, new MenuContext());
      var screen = RectTransformUtility.WorldToScreenPoint(camera, canvas.transform.TransformPoint(requested));
      context = new MenuPresentationContext(canvas, screen);
      var presenter = owner.GetComponent<RadialMenuPresenter>();
      presenter.Bind(session, profile, context);
      return presenter;
    }

    [Test]
    public void Oversized_custom_center_reports_nonfitting_instead_of_claiming_the_nominal_ring_fits()
    {
      profile.CenterPrefab = Center(new Vector2(1000, 1000));
      var presenter = Bind(Vector2.zero);
      var actualCenter = presenter.Root.GetComponentInChildren<RadialPlacementCenterWidget>();
      Assert.That(((RectTransform)actualCenter.transform).rect.size, Is.EqualTo(new Vector2(1000, 1000)), "Custom authoring dimensions must not be silently shrunk");
      Assert.That(presenter.FitsViewport, Is.False, "Nominal ring bounds ignored the 1000px custom center");
      Assert.That(presenter.PlacementDiagnostic, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Asymmetric_center_children_reposition_with_nonuniform_root_scale_without_mutating_their_transforms()
    {
      profile.CenterPrefab = Center(new Vector2(120, 100));
      var child = new GameObject("Authored offset content", typeof(RectTransform), typeof(Image));
      var authored = (RectTransform)child.transform;
      authored.SetParent(profile.CenterPrefab.transform, false);
      authored.sizeDelta = new Vector2(250, 140);
      authored.anchoredPosition = new Vector2(270, 30);
      authored.localRotation = Quaternion.Euler(0, 0, 20);
      var presenter = Bind(new Vector2(480, 380), new Vector3(1.25f, .75f, 1));
      var actual = (RectTransform)presenter.Root.GetComponentInChildren<RadialPlacementCenterWidget>().transform.Find(child.name);
      Assert.That(presenter.FitsViewport, Is.True, presenter.PlacementDiagnostic);
      AssertInsideViewport(actual);
      Assert.That(actual.rect.size, Is.EqualTo(authored.rect.size));
      Assert.That(actual.anchoredPosition, Is.EqualTo(authored.anchoredPosition));
      Assert.That(Quaternion.Angle(actual.localRotation, authored.localRotation), Is.LessThan(.01f));
      Assert.That(presenter.Root.localScale, Is.EqualTo(new Vector3(1.25f, .75f, 1)));
    }

    [Test]
    public void Actual_secondary_child_overflow_chooses_the_hosts_accessible_fallback_without_shrinking_widgets()
    {
      profile.CenterPrefab = Center(new Vector2(100, 100));
      var auxiliary = new GameObject("Authored auxiliary", typeof(RectTransform), typeof(Image));
      var auxiliaryRect = (RectTransform)auxiliary.transform;
      auxiliaryRect.SetParent(profile.CenterPrefab.transform, false);
      auxiliaryRect.sizeDelta = new Vector2(1400, 80);
      profile.CenterPrefab.GetComponent<RadialPlacementCenterWidget>().Auxiliary = auxiliary;
      var radialPrefab = Own(new GameObject("Radial prefab", typeof(RectTransform), typeof(RadialMenuPresenter)));
      var listPrefab = Own(new GameObject("Accessible list prefab", typeof(RectTransform), typeof(ListMenuPresenter)));
      profile.PresenterPrefab = radialPrefab;
      var fallback = Own(ScriptableObject.CreateInstance<MenuProfile>());
      fallback.PresenterPrefab = listPrefab; fallback.ShowCenter = false; fallback.Backdrop = false;
      profile.FallbackProfile = fallback;
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = profile;
      var secondary = new MenuAction("auxiliary", new MenuLabel("Auxiliary"), new MenuCommandBehavior(_ => { }, MenuActionOutcome.Keep));
      var action = new MenuAction("command", new MenuLabel("Command"), new MenuCommandBehavior(_ => { }), secondaryActions: new[] { secondary });
      using var host = new ContextMenuHostService(config);
      var point = RectTransformUtility.WorldToScreenPoint(camera, canvas.transform.position);
      host.OpenAsync(new MenuOpenRequest(new[] { action }, new MenuContext(), canvas, point)).GetAwaiter().GetResult();
      Assert.That(host.Presenter, Is.TypeOf<ListMenuPresenter>(), "Host ignored the bound auxiliary content that cannot fit this viewport");
      Assert.That(host.IsOpen, Is.True);
      Assert.That(host.Session.Selected.Id.Value, Is.EqualTo("command"));
      Assert.That(auxiliaryRect.rect.size, Is.EqualTo(new Vector2(1400, 80)), "Fallback must preserve the authored prefab");
    }

    [Test]
    public void Post_binding_layout_expansion_updates_containment_and_notifies_the_host()
    {
      profile.CenterPrefab = Center(new Vector2(120, 100));
      var group = profile.CenterPrefab.AddComponent<HorizontalLayoutGroup>();
      group.childControlWidth = true; group.childControlHeight = true;
      group.childForceExpandWidth = false; group.childForceExpandHeight = false;
      var fitter = profile.CenterPrefab.AddComponent<ContentSizeFitter>();
      fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
      var child = new GameObject("Layout-sized auxiliary", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
      child.transform.SetParent(profile.CenterPrefab.transform, false);
      ((RectTransform)child.transform).sizeDelta = new Vector2(20, 20);
      child.GetComponent<LayoutElement>().preferredWidth = 1400;
      child.GetComponent<LayoutElement>().preferredHeight = 80;
      var presenter = Bind(Vector2.zero);
      int notifications = 0; presenter.ContainmentChanged += () => notifications++;
      var center = (RectTransform)presenter.Root.GetComponentInChildren<RadialPlacementCenterWidget>().transform;
      // Run the real targeted uGUI layout and registry pipeline in EditMode. This is
      // layout ownership proof; it makes no PlayMode/rendering claim and forces no canvas.
      LayoutRebuilder.ForceRebuildLayoutImmediate(center);
      typeof(CanvasUpdateRegistry).GetMethod("PerformUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(CanvasUpdateRegistry.instance, null);
      Assert.That(center.rect.width, Is.GreaterThanOrEqualTo(1400), "The actual layout did not create the oversized footprint");
      Assert.That(presenter.FitsViewport, Is.False, "Containment was never rechecked after child layout finished");
      Assert.That(notifications, Is.GreaterThan(0), "The host was not told that a previously fitting composition overflowed after layout");
    }

    private void AssertInsideViewport(RectTransform actual)
    {
      var corners = new Vector3[4]; actual.GetWorldCorners(corners);
      var viewport = (RectTransform)canvas.transform;
      foreach (var corner in corners)
      {
        Vector3 point = viewport.InverseTransformPoint(corner);
        Assert.That(point.x, Is.InRange(viewport.rect.xMin - .01f, viewport.rect.xMax + .01f), "Custom content extends past the horizontal viewport");
        Assert.That(point.y, Is.InRange(viewport.rect.yMin - .01f, viewport.rect.yMax + .01f), "Custom content extends past the vertical viewport");
      }
    }
  }
}
