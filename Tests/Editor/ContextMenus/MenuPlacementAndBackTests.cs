using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuBackGuardTestPresenter : MenuPresenterBase
  {
    protected override void Render(bool forceLayout) { }
  }

  public sealed class MenuPlacementAndBackTests
  {
    private readonly List<Object> owned = new List<Object>();
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    [TearDown] public void Cleanup()
    {
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
    }

    [TestCase(MenuAnchorPolicy.TopLeft, 75, -20)]
    [TestCase(MenuAnchorPolicy.TopRight, -75, -20)]
    [TestCase(MenuAnchorPolicy.BottomLeft, 75, 20)]
    [TestCase(MenuAnchorPolicy.BottomRight, -75, 20)]
    [TestCase(MenuAnchorPolicy.TopCenter, 0, -20)]
    [TestCase(MenuAnchorPolicy.BottomCenter, 0, 20)]
    [TestCase(MenuAnchorPolicy.Centered, 0, 0)]
    public void Requested_anchor_preserves_the_chosen_corner_with_nonuniform_scale(MenuAnchorPolicy anchor, float x, float y)
    {
      var cameraOwner = Own(new GameObject("Placement camera", typeof(Camera)));
      var camera = cameraOwner.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 500;
      camera.transform.position = new Vector3(0, 0, -10);
      var canvasOwner = Own(new GameObject("Placement canvas", typeof(RectTransform), typeof(Canvas)));
      var canvas = canvasOwner.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
      var viewport = (RectTransform)canvas.transform;
      viewport.sizeDelta = new Vector2(1000, 1000);
      var menu = Own(new GameObject("Menu bounds", typeof(RectTransform))).GetComponent<RectTransform>();
      menu.SetParent(viewport, false); menu.localScale = new Vector3(1.5f, .5f, 1);
      var screen = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(Vector3.zero));
      Assert.That(MenuScreenPlacement.TryPlace(menu, canvas, screen, new Vector2(100, 80), out var diagnostic, anchor),
        Is.True, diagnostic);
      Assert.That(menu.localPosition.x, Is.EqualTo(x).Within(.01f));
      Assert.That(menu.localPosition.y, Is.EqualTo(y).Within(.01f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Native_cancel_respects_the_external_back_owner_without_filtering_programmatic_back(bool allowed)
    {
      var canvasOwner = Own(new GameObject("Back canvas", typeof(RectTransform), typeof(Canvas)));
      var canvas = canvasOwner.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var presenter = Own(new GameObject("Back presenter", typeof(RectTransform), typeof(MenuBackGuardTestPresenter)))
        .GetComponent<MenuBackGuardTestPresenter>();
      presenter.transform.SetParent(canvas.transform, false);
      var profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.Backdrop = false; profile.ShowCenter = false;
      profile.InputBindings.Configure(useNativeUguiNavigation: true, routePointerClick: false);
      using var session = new MenuSession();
      var child = new MenuAction("child", new MenuLabel("Child"), new MenuCommandBehavior(_ => { }));
      var parent = new MenuAction("parent", new MenuLabel("Parent"), children: new[] { child });
      session.Open(new[] { parent }, new MenuContext());
      session.EnterSubmenuAsync("parent").GetAwaiter().GetResult();
      int guardCalls = 0;
      using var context = new MenuPresentationContext(canvas, Vector2.zero, backInputAllowed: () => { guardCalls++; return allowed; });
      presenter.Bind(session, profile, context);
      var input = new BaseEventData(null);
      presenter.OnCancel(input);
      Assert.That(guardCalls, Is.EqualTo(1));
      Assert.That(session.Path.Count, Is.EqualTo(allowed ? 0 : 1));
      Assert.That(input.used, Is.EqualTo(allowed));
      if (!allowed) { presenter.Back(); Assert.That(session.Path, Is.Empty); }
      presenter.Unbind();
    }
  }
}
