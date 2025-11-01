using UnityEngine;

namespace MToolKit.Runtime.Utilities
{
  [RequireComponent(typeof(RectTransform))]
  public class SafeArea : MonoBehaviour
  {
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);

    private void Reset()
    {
      rectTransform = GetComponent<RectTransform>();
    }

    void Awake()
    {
      Refresh();
    }

    void Update()
    {
      Refresh();
    }

    private void Refresh()
    {
      Rect safeArea = Screen.safeArea;

      if (safeArea != lastSafeArea)
      {
        ApplySafeArea(safeArea);
      }
    }

    private void ApplySafeArea(Rect safeArea)
    {
      if (rectTransform == null)
        return;

      // Convert safe area rectangle from absolute pixels to a normalized anchor rectangle relative to the Canvas
      Vector2 anchorMin = safeArea.position;
      Vector2 anchorMax = safeArea.position + safeArea.size;
      var pixelRect = GetComponentInParent<Canvas>().pixelRect;
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