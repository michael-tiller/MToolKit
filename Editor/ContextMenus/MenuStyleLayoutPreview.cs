using System;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Geometry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.Editor.ContextMenus
{
  /// <summary>Read-only geometry sketch. It does not instantiate a presenter, input router, or scene.</summary>
  public static class MenuStyleLayoutPreview
  {
    public static void Draw(Rect area, MenuCommonSettings common, MenuStyleRecipe recipe)
    {
      EditorGUI.DrawRect(area, new Color(.09f, .10f, .13f));
      if (common == null || recipe == null)
      { GUI.Label(area, "Select common settings and a style.", CenteredLabel()); return; }
      if (recipe is CustomMenuStyleRecipe)
      { GUI.Label(area, "Custom presenter owns its layout.\nIts prefab and private children are retained.", CenteredLabel()); return; }
      MenuProfile profile = null;
      Color previous = Handles.color;
      bool handlesBegun = false;
      try
      {
        profile = MenuStyleAuthoring.CreateCandidate(common, recipe);
        if (!profile.Validate(out string diagnostic, false))
        { GUI.Label(area, diagnostic, CenteredLabel()); return; }
        if (!recipe.UsesRadialLayout) { DrawList(area, profile); return; }
        float extent = profile.InnerRadius + profile.RingWidth;
        bool concentric = profile.Navigation == MenuNavigationPolicy.Concentric;
        if (concentric) extent += profile.RingGap + profile.ChildRingWidth;
        extent += Mathf.Max(profile.WidgetSize.x, profile.WidgetSize.y) * .5f;
        float scale = Mathf.Min(area.width, area.height) / (extent * 2 + 32);
        Vector2 center = area.center;
        float sign = profile.Clockwise ? -1 : 1;
        int count = Mathf.Min(profile.MaximumVisibleItems, 12);
        var inner = new RadialRingGeometry(0, count, profile.InnerRadius,
          profile.InnerRadius + profile.RingWidth, profile.StartAngle, sign * 360, profile.AngularGap);
        Handles.BeginGUI();
        handlesBegun = true;
        DrawRing(inner, profile, center, scale, 0);
        if (concentric)
        {
          float parentCenter = inner.Segments[0].CenterAngle;
          float start = parentCenter - sign * profile.ChildArcDegrees * .5f;
          var outer = new RadialRingGeometry(1, count,
            profile.InnerRadius + profile.RingWidth + profile.RingGap,
            profile.InnerRadius + profile.RingWidth + profile.RingGap + profile.ChildRingWidth,
            start, sign * profile.ChildArcDegrees, profile.AngularGap);
          DrawRing(outer, profile, center, scale, 0);
        }
        Handles.EndGUI();
        handlesBegun = false;
        if (profile.ShowCenter)
        {
          Vector2 size = profile.CenterPrefab != null && profile.CenterPrefab.TryGetComponent<RectTransform>(out var rect)
            ? rect.rect.size : new Vector2(170, 150);
          var bounds = new Rect(center - size * scale * .5f, size * scale);
          EditorGUI.DrawRect(bounds, ColorFor(profile, MenuVisualRole.Center, new Color(.12f, .16f, .23f)));
          GUI.Label(bounds, "Center", CenteredLabel());
        }
      }
      catch (Exception error) { GUI.Label(area, error.Message, CenteredLabel()); }
      finally
      {
        if (handlesBegun) Handles.EndGUI();
        Handles.color = previous;
        if (profile != null) Object.DestroyImmediate(profile);
      }
    }

    private static void DrawRing(RadialRingGeometry ring, MenuProfile profile, Vector2 center, float scale, int selected)
    {
      var quad = new Vector3[4];
      foreach (var segment in ring.Segments)
      {
        if (profile.DrawSegments)
        {
          Handles.color = ColorFor(profile, segment.SegmentIndex == selected ? MenuVisualRole.Selected : MenuVisualRole.Normal,
            segment.SegmentIndex == selected ? new Color(.15f, .45f, .8f) : new Color(.22f, .25f, .31f));
          int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(segment.SweepAngle) / 6));
          for (int i = 0; i < steps; i++)
          {
            float first = segment.StartAngle + segment.SweepAngle * i / steps;
            float next = segment.StartAngle + segment.SweepAngle * (i + 1) / steps;
            quad[0] = Point(center, segment.InnerRadius * scale, first);
            quad[1] = Point(center, segment.OuterRadius * scale, first);
            quad[2] = Point(center, segment.OuterRadius * scale, next);
            quad[3] = Point(center, segment.InnerRadius * scale, next);
            Handles.DrawAAConvexPolygon(quad);
          }
        }
        var anchor = center + new Vector2(segment.ContentAnchor.x, -segment.ContentAnchor.y) * scale;
        var bounds = new Rect(anchor - profile.WidgetSize * scale * .5f, profile.WidgetSize * scale);
        if (!profile.DrawSegments) EditorGUI.DrawRect(bounds, new Color(.22f, .25f, .31f));
        Handles.color = Color.white;
        Handles.DrawWireCube(bounds.center, bounds.size);
        GUI.Label(bounds, (segment.SegmentIndex + 1).ToString(), CenteredLabel());
      }
    }

    private static void DrawList(Rect area, MenuProfile profile)
    {
      int count = Mathf.Min(profile.MaximumVisibleItems, 6);
      float scale = Mathf.Min((area.width - 24) / profile.ListWidth, (area.height - 24) / (count * profile.RowHeight));
      var size = new Vector2(profile.ListWidth, profile.RowHeight) * scale;
      var start = area.center - new Vector2(size.x, size.y * count) * .5f;
      for (int i = 0; i < count; i++)
      {
        var row = new Rect(start + new Vector2(0, size.y * i), size);
        EditorGUI.DrawRect(row, ColorFor(profile, i == 0 ? MenuVisualRole.Selected : MenuVisualRole.Normal,
          i == 0 ? new Color(.15f, .45f, .8f) : new Color(.22f, .25f, .31f)));
        GUI.Label(row, i == 1 ? "  ▸ Nested action" : "Action " + (i + 1), CenteredLabel());
      }
    }

    private static Vector3 Point(Vector2 center, float radius, float angle)
      => center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), -Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
    private static Color ColorFor(MenuProfile profile, MenuVisualRole role, Color fallback)
    {
      if (profile.Skin == null && profile.SharedThemeOverrides == null && profile.ThemeOverrides == null) return fallback;
      MenuThemeState state = role == MenuVisualRole.Normal ? MenuThemeState.Normal
        : role == MenuVisualRole.Hovered ? MenuThemeState.Hovered
        : role == MenuVisualRole.Selected ? MenuThemeState.Selected
        : role == MenuVisualRole.Disabled ? MenuThemeState.Disabled : MenuThemeState.Any;
      return profile.GetTheme().GetColor(role, state);
    }
    private static GUIStyle CenteredLabel()
      => new GUIStyle(EditorStyles.whiteLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
  }
}
