using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MToolKit.Runtime.UI
{
  /// <summary>Makes native uGUI keyboard/controller selection visible in its scroll owners.</summary>
  [DisallowMultipleComponent]
  public sealed class ScrollIntoViewOnSelect : MonoBehaviour, ISelectHandler
  {
    private bool pending;

    public static void Install(Transform root)
    {
      foreach (var control in root.GetComponentsInChildren<Selectable>(true))
        if (control.GetComponent<ScrollIntoViewOnSelect>() == null)
          control.gameObject.AddComponent<ScrollIntoViewOnSelect>();
    }

    public void OnSelect(BaseEventData eventData)
    {
      if (pending) return;
      pending = true;
      Canvas.willRenderCanvases += Reveal;
    }

    private void Reveal()
    {
      Cancel();
      if (isActiveAndEnabled) EnsureVisible(transform as RectTransform);
    }

    public static void EnsureVisible(RectTransform selected)
    {
      if (selected == null) return;
      var corners = new Vector3[4];
      foreach (var scroll in selected.GetComponentsInParent<ScrollRect>())
      {
        if (!scroll.isActiveAndEnabled || scroll.viewport == null || scroll.content == null ||
            !selected.IsChildOf(scroll.content)) continue;
        var viewport = scroll.viewport;
        // Selection reveals the actionable rectangle. Descendant tooltip sources, templates,
        // text and caret graphics can intentionally extend far beyond that hit region.
        selected.GetWorldCorners(corners);
        var bounds = new Bounds(viewport.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(viewport.InverseTransformPoint(corners[i]));
        var view = viewport.rect;
        Vector2 movement = Vector2.zero;
        if (scroll.horizontal)
          movement.x = bounds.size.x > view.width || bounds.min.x < view.xMin
            ? view.xMin - bounds.min.x : Mathf.Min(0, view.xMax - bounds.max.x);
        if (scroll.vertical)
          movement.y = bounds.size.y > view.height || bounds.max.y > view.yMax
            ? view.yMax - bounds.max.y : Mathf.Max(0, view.yMin - bounds.min.y);
        scroll.StopMovement();
        scroll.content.position += viewport.TransformVector(movement);
      }
    }

    private void Cancel()
    {
      Canvas.willRenderCanvases -= Reveal;
      pending = false;
    }

    private void OnDisable() => Cancel();
  }
}
