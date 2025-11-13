using UnityEngine;

namespace MToolKit.Runtime.Utilities
{
  [RequireComponent(typeof(RectTransform))]
  public class SafeArea : MonoBehaviour
  {
    private Rect lastSafeArea = new(0, 0, 0, 0);
    private Canvas pixelRectCanvas;
    private RectTransform rectTransform;

    private void Awake()
    {
      Refresh();
      pixelRectCanvas = GetComponentInParent<Canvas>();
    }

    private void Reset()
    {
      rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
      Refresh();
    }

    private void Refresh()
    {
      Rect safeArea = Screen.safeArea;

      if (safeArea != lastSafeArea)
        ApplySafeArea(safeArea);
    }

    private void ApplySafeArea(Rect safeArea)
    {
      if (rectTransform is null)
        return;

      // Convert safe area rectangle from absolute pixels to a normalized anchor rectangle relative to the Canvas
      Vector2 anchorMin = safeArea.position;
      Vector2 anchorMax = safeArea.position + safeArea.size;
      Rect pixelRect = pixelRectCanvas.pixelRect;
      anchorMin.x /= pixelRect.width;
      anchorMin.y /= pixelRect.height;
      anchorMax.x /= pixelRect.width;
      anchorMax.y /= pixelRect.height;

      rectTransform.anchorMin = anchorMin;
      rectTransform.anchorMax = anchorMax;

      lastSafeArea = safeArea;
    }
  }
}