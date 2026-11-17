using System;
using MToolKit.ContextMenus.Geometry;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Rendering
{
  /// <summary>Native annular sector. Rendering and raycasting consume the same cached geometry.</summary>
  [RequireComponent(typeof(CanvasRenderer))]
  public sealed class NativeRadialGraphic : MaskableGraphic, ICanvasRaycastFilter
  {
    [SerializeField, Range(.25f, 12)] private float degreesPerEdge = 3;
    [SerializeField, Min(0)] private float borderWidth = 1;
    [SerializeField] private Color borderColor = new Color(1, 1, 1, .4f);
    [SerializeField] private bool radialBorders = true;
    private RadialSegmentGeometry geometry;
    private bool hasGeometry;

    public RadialSegmentGeometry Geometry => geometry;
    public float BorderWidth
    {
      get => borderWidth;
      set { value = Mathf.Max(0, value); if (borderWidth == value) return; borderWidth = value; SetVerticesDirty(); }
    }
    public Color BorderColor
    {
      get => borderColor;
      set { if (borderColor == value) return; borderColor = value; SetVerticesDirty(); }
    }

    public void SetGeometry(RadialSegmentGeometry value)
    {
      bool unchanged = hasGeometry && geometry.InnerRadius == value.InnerRadius && geometry.OuterRadius == value.OuterRadius
          && geometry.StartAngle == value.StartAngle && geometry.SweepAngle == value.SweepAngle;
      geometry = value;
      hasGeometry = value.OuterRadius > value.InnerRadius && Math.Abs(value.SweepAngle) > 0;
      // Rectangles are broad-phase bounds only. Shape-aware filtering below owns the actual hit area.
      if (hasGeometry)
      {
        rectTransform.pivot = Vector2.one * .5f;
        rectTransform.sizeDelta = Vector2.one * (value.OuterRadius * 2);
      }
      if (unchanged) return;
      SetVerticesDirty();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
      => hasGeometry && RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
        screenPoint, eventCamera, out var localPoint) && geometry.Contains(localPoint);

    protected override void OnPopulateMesh(VertexHelper helper)
    {
      helper.Clear();
      if (!hasGeometry) return;
      float sweep = geometry.SweepAngle;
      int edges = Math.Max(1, Math.Min(2048, (int)Math.Ceiling(Math.Abs(sweep) / Math.Max(.25f, degreesPerEdge))));
      AddBand(helper, geometry.InnerRadius, geometry.OuterRadius, geometry.StartAngle, sweep, edges, color);
      float width = Math.Min(borderWidth, (geometry.OuterRadius - geometry.InnerRadius) * .5f);
      if (width <= 0 || borderColor.a <= 0) return;
      AddBand(helper, Math.Max(geometry.InnerRadius, geometry.OuterRadius - width), geometry.OuterRadius,
        geometry.StartAngle, sweep, edges, borderColor);
      if (geometry.InnerRadius > 0)
        AddBand(helper, geometry.InnerRadius, geometry.InnerRadius + width,
          geometry.StartAngle, sweep, edges, borderColor);
      if (!radialBorders || Math.Abs(sweep) >= 360) return;
      // Side decoration is inset into the sector envelope, including the angular gap.
      float edgeSweep = Math.Sign(sweep) * Math.Min(Math.Abs(sweep) * .5f,
        width / Math.Max(1, geometry.InnerRadius) * Mathf.Rad2Deg);
      AddBand(helper, geometry.InnerRadius, geometry.OuterRadius,
        geometry.StartAngle, edgeSweep, 1, borderColor);
      AddBand(helper, geometry.InnerRadius, geometry.OuterRadius,
        geometry.StartAngle + sweep - edgeSweep, edgeSweep, 1, borderColor);
    }

    private static void AddBand(VertexHelper helper, float inner, float outer, float start,
      float sweep, int edges, Color tint)
    {
      if (outer <= inner) return;
      int offset = helper.currentVertCount;
      for (int edge = 0; edge <= edges; edge++)
      {
        double angle = (start + (double)sweep * edge / edges) * Math.PI / 180;
        var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        helper.AddVert(direction * inner, tint, Vector2.zero);
        helper.AddVert(direction * outer, tint, Vector2.one);
        if (edge == 0) continue;
        int current = offset + edge * 2;
        helper.AddTriangle(current - 2, current - 1, current);
        helper.AddTriangle(current, current - 1, current + 1);
      }
    }
  }
}
