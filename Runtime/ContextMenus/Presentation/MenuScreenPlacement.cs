using UnityEngine;
using MToolKit.ContextMenus.Configuration;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>Placement never changes user scale, font size or hit-target dimensions.</summary>
  public static class MenuScreenPlacement
  {
    public static bool TryClamp(Rect viewport, Rect localBounds, Vector2 requested, out Vector2 point)
    {
      point = requested;
      if (!Finite(requested.x) || !Finite(requested.y) || !Finite(viewport) || !Finite(localBounds) ||
          localBounds.width > viewport.width || localBounds.height > viewport.height || localBounds.width <= 0 || localBounds.height <= 0 ||
          viewport.width <= 0 || viewport.height <= 0) return false;
      point.x = Mathf.Clamp(point.x, viewport.xMin - localBounds.xMin, viewport.xMax - localBounds.xMax);
      point.y = Mathf.Clamp(point.y, viewport.yMin - localBounds.yMin, viewport.yMax - localBounds.yMax);
      return true;
    }

    public static bool TryPlace(RectTransform root, Canvas canvas, Vector2 screenPosition,
      Vector2 logicalSize, out string diagnostic, MenuAnchorPolicy anchor = MenuAnchorPolicy.Centered)
    {
      root.sizeDelta = logicalSize;
      return TryPlaceBounds(root, canvas, screenPosition, root.rect, out diagnostic, anchor);
    }

    /// <summary>Places an actual composition envelope expressed in the menu root's coordinates.</summary>
    public static bool TryPlaceBounds(RectTransform root, Canvas canvas, Vector2 screenPosition,
      Rect localBounds, out string diagnostic, MenuAnchorPolicy anchor = MenuAnchorPolicy.Centered)
    {
      diagnostic = null;
      var viewport = (RectTransform)canvas.transform;
      Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
      if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPosition, camera, out var requested))
      { diagnostic = "Menu opening position could not be converted into the canvas coordinate space."; return false; }

      var rect = localBounds;
      var minimum = new Vector2(float.MaxValue, float.MaxValue);
      var maximum = new Vector2(float.MinValue, float.MinValue);
      // Transform vectors, not only a canvas scale factor: rotations and nonuniform ancestors matter.
      for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        {
          var corner = new Vector3(x == 0 ? rect.xMin : rect.xMax, y == 0 ? rect.yMin : rect.yMax);
          Vector2 transformed = viewport.InverseTransformVector(root.TransformVector(corner));
          minimum = Vector2.Min(minimum, transformed);
          maximum = Vector2.Max(maximum, transformed);
        }
      var bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
      switch (anchor)
      {
        case MenuAnchorPolicy.CursorTopLeft: requested -= new Vector2(bounds.xMin, bounds.yMax); break;
        case MenuAnchorPolicy.TopRight: requested -= new Vector2(bounds.xMax, bounds.yMax); break;
        case MenuAnchorPolicy.BottomLeft: requested -= new Vector2(bounds.xMin, bounds.yMin); break;
        case MenuAnchorPolicy.BottomRight: requested -= new Vector2(bounds.xMax, bounds.yMin); break;
        case MenuAnchorPolicy.TopCenter: requested -= new Vector2(bounds.center.x, bounds.yMax); break;
        case MenuAnchorPolicy.BottomCenter: requested -= new Vector2(bounds.center.x, bounds.yMin); break;
        case MenuAnchorPolicy.Centered: requested -= bounds.center; break;
        default: diagnostic = "The requested menu anchor is not supported."; return false;
      }
      if (!TryClamp(viewport.rect, bounds, requested, out var placed))
      { diagnostic = "Menu exceeds available canvas capacity; use pagination or the accessible list presentation."; return false; }
      var world = viewport.TransformPoint(new Vector3(placed.x, placed.y, 0));
      var parent = root.parent;
      var local = parent != null ? parent.InverseTransformPoint(world) : world;
      local.z = root.localPosition.z;
      root.localPosition = local;
      return true;
    }

    public static Vector2 LogicalCapacity(RectTransform root, Canvas canvas)
    {
      var viewport = (RectTransform)canvas.transform;
      Vector3 horizontal = viewport.InverseTransformVector(root.TransformVector(Vector3.right));
      Vector3 vertical = viewport.InverseTransformVector(root.TransformVector(Vector3.up));
      float scaleX = Mathf.Max(.0001f, horizontal.magnitude);
      float scaleY = Mathf.Max(.0001f, vertical.magnitude);
      return new Vector2(viewport.rect.width / scaleX, viewport.rect.height / scaleY);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Rect value) => Finite(value.x) && Finite(value.y) && Finite(value.width) && Finite(value.height);
  }
}
