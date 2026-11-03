using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MToolKit.Runtime.UI.Scaling
{
  /// <summary>Keeps pointer input stable when a live preference moves or resizes its own slider.</summary>
  public sealed class StableDragSlider : Slider, IEndDragHandler
  {
    private const int NoPointer = int.MinValue;
    private readonly Vector3[] corners = new Vector3[4];
    private int pointerId = NoPointer;
    private PointerEventData pressedEvent;
    private Vector2 pressPosition;
    private Vector2 screenAxis;
    private Vector2Int screenSize;
    private float initialNormalizedValue;
    private float axisLengthSquared;
    private bool reverse;
    private bool captured;
    private bool suppressPointerDownValue;

    public override void OnPointerDown(PointerEventData eventData)
    {
      if (!MayDrag(eventData) || (pointerId != NoPointer && pointerId != eventData.pointerId)) return;
      ClearCapture();
      pointerId = eventData.pointerId;
      pressedEvent = eventData;
      pressPosition = eventData.position;
      screenSize = new Vector2Int(Screen.width, Screen.height);
      bool horizontal = direction == Direction.LeftToRight || direction == Direction.RightToLeft;
      reverse = direction == Direction.RightToLeft || direction == Direction.TopToBottom;
      var track = (handleRect != null ? handleRect.parent : fillRect != null ? fillRect.parent : null) as RectTransform;
      float clickedValue = normalizedValue;
      bool handlePressed = handleRect != null && RectTransformUtility.RectangleContainsScreenPoint(
        handleRect, pressPosition, eventData.pressEventCamera);
      if (track != null)
      {
        track.GetWorldCorners(corners);
        Vector2 start = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, corners[0]);
        Vector2 end = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, corners[horizontal ? 3 : 1]);
        screenAxis = end - start;
        axisLengthSquared = screenAxis.sqrMagnitude;
        captured = axisLengthSquared > 0.0001f && !float.IsInfinity(axisLengthSquared);
        if (captured && !handlePressed)
        {
          float fraction = Vector2.Dot(pressPosition - start, screenAxis) / axisLengthSquared;
          clickedValue = reverse ? 1f - fraction : fraction;
        }
      }

      // Native selection/pressed state is retained, but selection can synchronously reflow
      // the track. Apply its initial value once, against the geometry the player pressed.
      suppressPointerDownValue = true;
      try { base.OnPointerDown(eventData); }
      finally { suppressPointerDownValue = false; }
      if (!captured || !MayDrag(eventData) || pointerId != eventData.pointerId) return;
      if (!handlePressed) normalizedValue = clickedValue;
      initialNormalizedValue = normalizedValue;
    }

    protected override void Set(float input, bool sendCallback = true)
    {
      if (!suppressPointerDownValue) base.Set(input, sendCallback);
    }

    public override void OnDrag(PointerEventData eventData)
    {
      if (!captured || pointerId != eventData.pointerId || !MayDrag(eventData)) return;
      if (screenSize.x != Screen.width || screenSize.y != Screen.height)
      {
        // A new display coordinate system needs a fresh press, not a jump to the new track.
        captured = false;
        return;
      }
      float delta = Vector2.Dot(eventData.position - pressPosition, screenAxis) / axisLengthSquared;
      normalizedValue = initialNormalizedValue + (reverse ? -delta : delta);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
      if (eventData.button != PointerEventData.InputButton.Left) return;
      if (pointerId != NoPointer && pointerId != eventData.pointerId) return;
      ClearCapture();
      base.OnPointerUp(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
      if (eventData.button != PointerEventData.InputButton.Left) return;
      if (pointerId != eventData.pointerId) return;
      ClearCapture();
      base.OnPointerUp(eventData);
    }

    private bool MayDrag(PointerEventData eventData) => IsActive() && IsInteractable()
      && eventData.button == PointerEventData.InputButton.Left;

    private void ClearCapture()
    {
      captured = false;
      pointerId = NoPointer;
      pressedEvent = null;
    }

    private void CancelPointer()
    {
      var releasedEvent = pressedEvent;
      ClearCapture();
      if (releasedEvent != null) base.OnPointerUp(releasedEvent);
    }

    private void OnApplicationFocus(bool focused)
    {
      if (!focused) CancelPointer();
    }

    private void OnApplicationPause(bool paused)
    {
      if (paused) CancelPointer();
    }

    protected override void OnDisable()
    {
      ClearCapture();
      base.OnDisable();
    }
  }
}
