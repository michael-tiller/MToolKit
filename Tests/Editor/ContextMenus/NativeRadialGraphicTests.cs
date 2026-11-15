using MToolKit.ContextMenus.Geometry;
using MToolKit.ContextMenus.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class NativeRadialGraphicTests
  {
    [Test]
    public void Renderer_and_raycast_share_the_visible_sector_under_nonuniform_scale()
    {
      var canvasOwner = new GameObject("Sector test canvas", typeof(RectTransform), typeof(Canvas));
      var owner = new GameObject("Sector", typeof(RectTransform), typeof(CanvasRenderer), typeof(NativeRadialGraphic));
      try
      {
        var canvas = canvasOwner.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        owner.transform.SetParent(canvasOwner.transform, false);
        var rect = owner.GetComponent<RectTransform>();
        rect.localScale = new Vector3(1.75f, .6f, 1);
        rect.localRotation = Quaternion.Euler(0, 0, 23);
        var graphic = owner.GetComponent<NativeRadialGraphic>();
        var geometry = new RadialRingGeometry(0, 4, 40, 100, 0, 360, 8).Segments[0];
        graphic.SetGeometry(geometry);
        var inside = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(geometry.ContentAnchor));
        var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(Vector3.zero));
        var outside = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(110, 0)));
        Assert.That(graphic.IsRaycastLocationValid(inside, null), Is.True);
        Assert.That(graphic.IsRaycastLocationValid(center, null), Is.False);
        Assert.That(graphic.IsRaycastLocationValid(outside, null), Is.False);
      }
      finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(canvasOwner); }
    }

    [Test]
    public void Sector_mesh_vertices_stay_inside_the_shared_geometry_envelope()
    {
      var owner = new GameObject("Sector mesh", typeof(RectTransform), typeof(CanvasRenderer), typeof(NativeRadialGraphic));
      var mesh = new Mesh();
      try
      {
        var graphic = owner.GetComponent<NativeRadialGraphic>();
        var geometry = new RadialRingGeometry(1, 3, 60, 130, 315, 210, 5).Segments[1];
        graphic.SetGeometry(geometry);
        using (var helper = new VertexHelper())
        {
          typeof(NativeRadialGraphic).GetMethod("OnPopulateMesh",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(VertexHelper) }, null)
            .Invoke(graphic, new object[] { helper });
          helper.FillMesh(mesh);
        }
        Assert.That(mesh.vertexCount, Is.GreaterThan(3));
        foreach (var point in mesh.vertices)
        {
          float radius = new Vector2(point.x, point.y).magnitude;
          Assert.That(radius, Is.InRange(geometry.InnerRadius - .01f, geometry.OuterRadius + .01f));
        }
      }
      finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(owner); }
    }
  }
}
