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

    [Test]
    public void Selected_outer_band_is_inset_and_follows_clockwise_partial_sector_gap()
    {
      var owner = new GameObject("Selected sector mesh", typeof(RectTransform), typeof(CanvasRenderer), typeof(NativeRadialGraphic));
      var mesh = new Mesh();
      try
      {
        var graphic = owner.GetComponent<NativeRadialGraphic>();
        var geometry = new RadialRingGeometry(1, 3, 60, 130, 25, -120, 8).Segments[1];
        graphic.SetGeometry(geometry); graphic.color = Color.red; graphic.BorderColor = Color.green;
        var originalRect = graphic.rectTransform.rect;
        SetOuterHighlight(graphic, 4, Color.blue);
        Populate(graphic, mesh);
        int blue = 0; bool inner = false, outer = false, start = false, end = false;
        var positions = mesh.vertices; var colors = mesh.colors32;
        for (int i = 0; i < positions.Length; i++)
        {
          if (!colors[i].Equals((Color32)Color.blue)) continue;
          blue++; var point = new Vector2(positions[i].x, positions[i].y); float radius = point.magnitude;
          Assert.That(radius, Is.InRange(geometry.OuterRadius - 4 - .01f, geometry.OuterRadius + .01f));
          float angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
          float travel = Mathf.Repeat((angle - geometry.StartAngle) * Mathf.Sign(geometry.SweepAngle), 360);
          Assert.That(travel <= Mathf.Abs(geometry.SweepAngle) + .01f || travel >= 359.99f, Is.True, "The inset band must respect the visible gap-trimmed angular interval.");
          inner |= Mathf.Abs(radius - (geometry.OuterRadius - 4)) < .01f;
          outer |= Mathf.Abs(radius - geometry.OuterRadius) < .01f;
          start |= Mathf.Abs(Mathf.DeltaAngle(angle, geometry.StartAngle)) < .01f;
          end |= Mathf.Abs(Mathf.DeltaAngle(angle, geometry.StartAngle + geometry.SweepAngle)) < .01f;
        }
        Assert.That(blue, Is.GreaterThan(3), "The selected arc must generate native mesh geometry in the existing marker color.");
        Assert.That(inner && outer && start && end, Is.True);
        Assert.That(graphic.rectTransform.rect, Is.EqualTo(originalRect), "Selection decoration must not grow the sector's broad-phase bounds.");
        Assert.That(graphic.Geometry.OuterRadius, Is.EqualTo(geometry.OuterRadius));
        SetOuterHighlight(graphic, 0, Color.blue); Populate(graphic, mesh);
        foreach (var color in mesh.colors32) Assert.That(color.Equals((Color32)Color.blue), Is.False, "Clearing selection must remove the previous outer band.");
      }
      finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(owner); }
    }

    [Test]
    public void Equal_fill_and_selected_band_have_a_contrasting_inset_keyline_without_growing_the_sector()
    {
      var owner = new GameObject("Selected contrast mesh", typeof(RectTransform), typeof(CanvasRenderer), typeof(NativeRadialGraphic));
      var mesh = new Mesh();
      try
      {
        var graphic = owner.GetComponent<NativeRadialGraphic>();
        var geometry = new RadialRingGeometry(0, 5, 40, 100, 315, 360, 6).Segments[0];
        graphic.SetGeometry(geometry); graphic.color = Color.blue; graphic.BorderColor = Color.white; graphic.BorderWidth = 1;
        SetOuterHighlight(graphic, 4, Color.blue); Populate(graphic, mesh);
        bool keyline = false, touchesBand = false; var positions = mesh.vertices; var colors = mesh.colors32;
        for (int i = 0; i < positions.Length; i++)
        {
          float radius = new Vector2(positions[i].x, positions[i].y).magnitude;
          Assert.That(radius, Is.LessThanOrEqualTo(geometry.OuterRadius + .01f));
          if (!colors[i].Equals((Color32)Color.white)) continue;
          keyline |= radius < geometry.OuterRadius - 4 - .01f && radius >= geometry.OuterRadius - 8;
          touchesBand |= Mathf.Abs(radius - (geometry.OuterRadius - 4)) < .01f;
        }
        Assert.That(keyline && touchesBand, Is.True, "The Label-role contrast keyline belongs immediately inside the blue band when the selected fill is also blue.");
      }
      finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(owner); }
    }

    private static void SetOuterHighlight(NativeRadialGraphic graphic, float width, Color color)
    {
      var widthProperty = typeof(NativeRadialGraphic).GetProperty("OuterHighlightWidth");
      var colorProperty = typeof(NativeRadialGraphic).GetProperty("OuterHighlightColor");
      Assert.That(widthProperty, Is.Not.Null, "Native sectors require an optional inset outer selection band.");
      Assert.That(colorProperty, Is.Not.Null);
      widthProperty.SetValue(graphic, width); colorProperty.SetValue(graphic, color);
    }

    private static void Populate(NativeRadialGraphic graphic, Mesh mesh)
    {
      using (var helper = new VertexHelper())
      {
        typeof(NativeRadialGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
          null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
        helper.FillMesh(mesh);
      }
    }
  }
}
