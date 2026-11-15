using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Geometry;
using MToolKit.ContextMenus.Rendering;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>Single, replacement and sliding concentric windows over the same session hierarchy.</summary>
  public sealed class RadialMenuPresenter : MenuPresenterBase, ICanvasElement
  {
    private readonly List<MenuAction> innerActions = new List<MenuAction>();
    private readonly List<MenuAction> outerActions = new List<MenuAction>();
    private readonly List<NativeRadialGraphic> graphics = new List<NativeRadialGraphic>();
    private RadialAnalogSelector selector;
    private (int, int, float, float, float, float, float, bool, float, float, float, float, float) shapeKey;
    private bool hasShape, hasTarget = true;
    private int focusedRing;
    private int parentStart;
    private long branchRequest;
    private readonly List<RectTransform> contentRects = new List<RectTransform>();
    private readonly Vector3[] contentCorners = new Vector3[4];
    private bool layoutCheckQueued, layoutPostScheduled;
    private long boundsBindingGeneration;
    private MenuSession layoutSession;
    public RadialRingGeometry InnerGeometry { get; private set; }
    public RadialRingGeometry OuterGeometry { get; private set; }
    public int FocusedRing => focusedRing;

    protected override void Render(bool forceLayout)
    {
      if (Profile.RingWidth < Profile.MinimumTargetSize)
      { ReportCannotFit("The configured radial band is thinner than the minimum hit target; use the accessible fallback profile."); return; }
      bool concentric = Profile.Navigation == MenuNavigationPolicy.Concentric && Session.ParentActions.Count > 0;
      if (Profile.ChildRingWidth < Profile.MinimumTargetSize) concentric = false;
      float outerRadius = Profile.InnerRadius + Profile.RingWidth;
      float maximumRadius = concentric ? outerRadius + Profile.RingGap + Profile.ChildRingWidth : outerRadius;
      float extent = maximumRadius + Mathf.Max(Profile.WidgetSize.x, Profile.WidgetSize.y) * .5f;
      if (!MenuScreenPlacement.TryPlace(Root, Context.Canvas, Context.ScreenPosition,
          new Vector2(extent * 2, extent * 2 + 72), out var diagnostic, Context.ResolveAnchor(Profile.Anchor)))
      {
        // Preserve the hierarchy and use a one-ring sliding window if only the outer arc cannot fit.
        if (concentric)
        {
          concentric = false;
          maximumRadius = outerRadius;
          extent = maximumRadius + Mathf.Max(Profile.WidgetSize.x, Profile.WidgetSize.y) * .5f;
        }
        if (!MenuScreenPlacement.TryPlace(Root, Context.Canvas, Context.ScreenPosition,
            new Vector2(extent * 2, extent * 2 + 72), out diagnostic, Context.ResolveAnchor(Profile.Anchor)))
        { ReportCannotFit(diagnostic); return; }
      }
      FitsViewport = true; PlacementDiagnostic = null;
      int innerCapacity = Capacity(360, Profile.InnerRadius + Profile.RingWidth * .5f);
      int currentCapacity = concentric ? Capacity(Profile.ChildArcDegrees,
        outerRadius + Profile.RingGap + Profile.ChildRingWidth * .5f) : innerCapacity;
      Session.PageSize = currentCapacity;
      innerActions.Clear(); outerActions.Clear();
      if (concentric)
      {
        int selectedParent = 0;
        for (int i = 0; i < Session.ParentActions.Count; i++)
          if (Session.ParentActions[i] == Session.ParentSelected) { selectedParent = i; break; }
        parentStart = selectedParent / innerCapacity * innerCapacity;
        for (int i = parentStart; i < Session.ParentActions.Count && innerActions.Count < innerCapacity; i++)
          innerActions.Add(Session.ParentActions[i]);
        for (int i = 0; i < Session.Rows.Count; i++) outerActions.Add(Session.Rows[i].Action);
      }
      else
      {
        parentStart = 0;
        for (int i = 0; i < Session.Rows.Count; i++) innerActions.Add(Session.Rows[i].Action);
      }
      float sign = Profile.Clockwise ? -1 : 1;
      int localParent = 0;
      for (int i = 0; i < innerActions.Count; i++) if (innerActions[i] == Session.ParentSelected) { localParent = i; break; }
      float parentCenter = Profile.StartAngle + sign * 360 / Math.Max(1, innerActions.Count) * (localParent + .5f);
      float childStart = parentCenter - sign * Profile.ChildArcDegrees * .5f;
      var key = (innerActions.Count, outerActions.Count, Profile.InnerRadius,
        Profile.RingWidth, Profile.RingGap, Profile.ChildRingWidth, Profile.StartAngle, Profile.Clockwise,
        Profile.AngularGap, Profile.ChildArcDegrees, concentric ? childStart : 0, Profile.AnalogDeadZone, Profile.AnalogHysteresis);
      if (!hasShape || !key.Equals(shapeKey))
      {
        bool hadOuter = OuterGeometry != null;
        InnerGeometry = innerActions.Count == 0 ? null : new RadialRingGeometry(0, innerActions.Count,
          Profile.InnerRadius, outerRadius, Profile.StartAngle, sign * 360, Profile.AngularGap);
        OuterGeometry = outerActions.Count == 0 ? null : new RadialRingGeometry(1, outerActions.Count,
          outerRadius + Profile.RingGap, outerRadius + Profile.RingGap + Profile.ChildRingWidth,
          childStart, sign * Profile.ChildArcDegrees, Profile.AngularGap);
        selector = new RadialAnalogSelector(Profile.AnalogDeadZone, Profile.AnalogHysteresis);
        if (!hadOuter && OuterGeometry != null) focusedRing = 1;
        if (OuterGeometry == null) focusedRing = 0;
        shapeKey = key; hasShape = true;
      }
      int index = 0;
      RenderRing(InnerGeometry, innerActions, 0, ref index);
      RenderRing(OuterGeometry, outerActions, 1, ref index);
      for (int i = index; i < graphics.Count; i++) graphics[i].gameObject.SetActive(false);
      FinishItems(index);
      SetFooter(new Vector2(0, -extent - 18), Math.Max(240, outerRadius * 1.5f));
      BindCenter(true);
      PlaceBoundContent(false);
      QueueContentLayoutCheck();
    }

    private void PlaceBoundContent(bool notify)
    {
      if (!IsBound) return;
      bool fitted = FitsViewport;
      string previousDiagnostic = PlacementDiagnostic;
      contentRects.Clear();
      // Include inactive candidates while the host binds an inactive root, but exclude
      // hidden pooled widgets and their descendants using their authored activeSelf path.
      Root.GetComponentsInChildren(true, contentRects);
      Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
      Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
      for (int i = 0; i < contentRects.Count; i++)
      {
        var rect = contentRects[i];
        if (rect == null || !InActiveComposition(rect)) continue;
        rect.GetWorldCorners(contentCorners);
        for (int corner = 0; corner < contentCorners.Length; corner++)
        {
          Vector2 point = Root.InverseTransformPoint(contentCorners[corner]);
          minimum = Vector2.Min(minimum, point);
          maximum = Vector2.Max(maximum, point);
        }
      }
      var bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
      if (MenuScreenPlacement.TryPlaceBounds(Root, Context.Canvas, Context.ScreenPosition,
          bounds, out var diagnostic, Context.ResolveAnchor(Profile.Anchor)))
      { FitsViewport = true; PlacementDiagnostic = null; }
      else
      {
        diagnostic = "Bound menu content, including center and auxiliary widgets, cannot fit. " + diagnostic;
        if (FitsViewport || !string.Equals(PlacementDiagnostic, diagnostic, StringComparison.Ordinal)) ReportCannotFit(diagnostic);
      }
      if (notify && (fitted != FitsViewport || !string.Equals(previousDiagnostic, PlacementDiagnostic, StringComparison.Ordinal)))
        NotifyContainmentChanged();
    }

    private bool InActiveComposition(Transform item)
    {
      for (var current = item; current != null && current != Root; current = current.parent)
        if (!current.gameObject.activeSelf) return false;
      return true;
    }

    private void QueueContentLayoutCheck()
    {
      if (!IsBound || layoutCheckQueued) return;
      if (!PlayerLoopHelper.IsMainThread || CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
      {
        if (layoutPostScheduled) return;
        layoutPostScheduled = true;
        long binding = boundsBindingGeneration;
        var observed = Session;
        UniTask.Post(() =>
        {
          if (this == null || binding != boundsBindingGeneration || !ReferenceEquals(observed, Session)) return;
          layoutPostScheduled = false;
          QueueContentLayoutCheck();
        });
        return;
      }
      layoutSession = Session;
      layoutCheckQueued = CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(this);
    }

    public void Rebuild(CanvasUpdate executing)
    {
      if (executing != CanvasUpdate.PostLayout || !layoutCheckQueued) return;
      var observed = layoutSession;
      layoutCheckQueued = false; layoutSession = null;
      if (this == null || !IsBound || !ReferenceEquals(observed, Session)) return;
      PlaceBoundContent(true);
    }
    public void LayoutComplete() { layoutCheckQueued = false; layoutSession = null; }
    public void GraphicUpdateComplete() { }
    public bool IsDestroyed() => this == null;

    private int Capacity(float arcDegrees, float radius)
    {
      double availableArc = arcDegrees * Math.PI / 180 * radius;
      double target = Math.Max(Profile.MinimumTargetSize, Profile.WidgetSize.x);
      int count = Math.Max(1, Math.Min(Profile.MaximumVisibleItems, (int)Math.Floor(availableArc / target)));
      while (count > 1 && 2 * radius * Math.Sin(Math.Min(Math.PI, arcDegrees * Math.PI / 180 / count) * .5) < target) count--;
      return count;
    }

    private void RenderRing(RadialRingGeometry ring, List<MenuAction> actions, int ringIndex, ref int index)
    {
      if (ring == null) return;
      for (int i = 0; i < actions.Count; i++, index++)
      {
        var action = actions[i];
        var geometry = ring.Segments[i];
        while (graphics.Count <= index)
        {
          var rect = MenuWidgetFactory.Rect("Native menu wedge", Root);
          rect.SetAsFirstSibling();
          graphics.Add(rect.gameObject.AddComponent<NativeRadialGraphic>());
        }
        var graphic = graphics[index];
        graphic.gameObject.SetActive(Profile.DrawSegments);
        graphic.rectTransform.anchoredPosition = Vector2.zero;
        graphic.SetGeometry(geometry);
        graphic.raycastTarget = true;
        bool selected = ringIndex == 0 && OuterGeometry != null ? action == Session.ParentSelected
          : hasTarget && action == Session.Selected;
        var availability = action.Evaluate(Session.Context);
        graphic.color = ColorFor(!availability.Enabled ? MenuVisualRole.Disabled : IsPointerHovered(action)
          ? MenuVisualRole.Hovered : selected ? MenuVisualRole.Selected : MenuVisualRole.Normal);
        graphic.BorderColor = ColorFor(selected ? MenuVisualRole.Label : MenuVisualRole.OuterBorder);
        graphic.BorderWidth = selected ? 3 : 1;
        var item = Item(index);
        MenuWidgetFactory.Place(item.Rect, geometry.ContentAnchor, Profile.WidgetSize);
        var filter = item.GameObject.GetComponent<MenuSectorHitFilter>();
        if (filter == null) filter = item.GameObject.AddComponent<MenuSectorHitFilter>();
        filter.enabled = Profile.DrawSegments;
        filter.Configure(Root, geometry);
        int selectedRing = ringIndex;
        var pointer = graphic.GetComponent<MenuSectorPointer>();
        if (pointer == null) pointer = graphic.gameObject.AddComponent<MenuSectorPointer>();
        pointer.Configure(() => SelectPointerAction(action, selectedRing), Profile.InputBindings.RoutePointerClick || !action.Behavior.IsInteractive ? null
          : () => ActivateRingActionAsync(action, selectedRing).Forget(), () => ClearPointerHover(action));
        item.Bind(StateFor(action, selected,
          Profile.InputBindings.RoutePointerClick || !action.Behavior.IsInteractive ? null : () => ActivateRingActionAsync(action, selectedRing).Forget(),
          () => SelectRingAction(action, selectedRing)));
        ConfigureNavigation(item);
        if (item.DefaultWidget?.Background != null && Profile.DrawSegments)
          item.DefaultWidget.Background.color = Color.clear;
      }
    }

    private void SelectPointerAction(MenuAction action, int ring)
    {
      if (!IsBound) return;
      var before = Session.Selected; var mode = Session.InteractionMode;
      bool changed = SetPointerHovered(action);
      Session.InteractionMode = MenuInteractionMode.Pointer;
      SelectRingAction(action, ring);
      if (changed && ReferenceEquals(before, Session.Selected) && mode == Session.InteractionMode) Refresh(false);
    }

    private void SelectRingAction(MenuAction action, int ring)
    {
      if (!IsBound) return;
      bool feedbackChanged = !hasTarget || focusedRing != ring;
      hasTarget = action.Behavior.IsInteractive;
      focusedRing = ring;
      if (!hasTarget) { Refresh(false); return; }
      if (ring == 0 && OuterGeometry != null)
      {
        if (action != Session.ParentSelected) ChangeParentAsync(action, 0).Forget();
        else if (feedbackChanged) Refresh(false);
      }
      else
      {
        bool same = Session.Selected == action;
        if (!Session.Select(action.Id)) hasTarget = false;
        if (same && feedbackChanged || !hasTarget) Refresh(false);
      }
    }

    private async UniTask ActivateRingActionAsync(MenuAction action, int ring)
    {
      if (!IsBound) return;
      hasTarget = true;
      if (ring == 0 && OuterGeometry != null)
      {
        if (action != Session.ParentSelected) await ChangeParentAsync(action, 1);
        else { focusedRing = 1; Refresh(false); }
        return;
      }
      focusedRing = ring;
      if (Session.Select(action.Id)) ConfirmChosenWidget();
    }

    private async UniTask ChangeParentAsync(MenuAction action, int focus)
    {
      if (!IsBound) return;
      long request = ++branchRequest;
      var session = Session;
      session.GoBack();
      if (!session.Select(action.Id)) return;
      if (action.HasChildren) await session.EnterSubmenuAsync(action.Id);
      if (!IsBound || Session != session || request != branchRequest) return;
      focusedRing = OuterGeometry != null ? focus : 0;
      Refresh(false);
    }

    public override void Point(Vector2 screenPosition)
    {
      base.Point(screenPosition);
      if (!IsBound) return;
      if (PointerControlOwnsInput) return;
      var camera = Context.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Context.Canvas.worldCamera;
      if (!Profile.DrawSegments)
      {
        // A button-only composition owns its complete authored widget hit shape. The annulus remains
        // the directional keyboard/controller layout, but cannot clip a visible Sims-style button.
        for (int i = VisibleWidgetCount - 1; i >= 0; i--)
        {
          var item = Item(i);
          bool hit = PointerHitObject != null ? PointerHitObject.transform.IsChildOf(item.Rect)
            : RectTransformUtility.RectangleContainsScreenPoint(item.Rect, screenPosition, camera);
          if (!hit) continue;
          int ring = i < innerActions.Count ? 0 : 1;
          var action = ring == 0 ? innerActions[i] : outerActions[i - innerActions.Count];
          SelectPointerAction(action, ring);
          return;
        }
        bool hoverChanged = SetPointerHovered(null);
        if (hasTarget || hoverChanged) { hasTarget = false; Refresh(false); }
        return;
      }
      if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPosition, camera, out var point))
      {
        bool hoverChanged = SetPointerHovered(null);
        if (hasTarget || hoverChanged) { hasTarget = false; Refresh(false); }
        return;
      }
      int outer = OuterGeometry?.HitTest(point) ?? -1;
      if (outer >= 0) { SelectPointerAction(outerActions[outer], 1); return; }
      int inner = InnerGeometry?.HitTest(point) ?? -1;
      if (inner >= 0)
      {
        var action = innerActions[inner];
        bool preview = Profile.Navigation == MenuNavigationPolicy.Concentric && OuterGeometry == null && action.HasChildren;
        SelectPointerAction(action, 0);
        if (preview) Session.EnterSubmenuAsync(action.Id).Forget();
        return;
      }
      bool clearedHover = SetPointerHovered(null);
      if (hasTarget || clearedHover) { hasTarget = false; Refresh(false); }
    }

    public override void Navigate(Vector2 direction, bool analog)
    {
      if (!IsBound) return;
      if (TryRouteControlNavigation(direction, analog)) return;
      Session.InteractionMode = analog ? MenuInteractionMode.Controller : MenuInteractionMode.Keyboard;
      SetPointerHovered(null);
      if (!analog)
      {
        hasTarget = true;
        if (focusedRing == 0 && OuterGeometry != null && innerActions.Count > 0 && direction != Vector2.zero)
        {
          int current = innerActions.IndexOf(Session.ParentSelected);
          int step = direction.y > 0 || direction.x < 0 ? -1 : 1;
          for (int offset = 1; offset <= innerActions.Count; offset++)
          {
            int candidate = (Math.Max(0, current) + step * offset + innerActions.Count) % innerActions.Count;
            if (!innerActions[candidate].Behavior.IsInteractive) continue;
            SelectRingAction(innerActions[candidate], 0);
            break;
          }
          return;
        }
        base.Navigate(direction, false);
        return;
      }
      var ring = focusedRing == 1 ? OuterGeometry : InnerGeometry;
      int selected = selector == null ? -1 : selector.Select(direction, ring);
      hasTarget = selected >= 0;
      if (selected >= 0)
      {
        var actions = focusedRing == 1 ? outerActions : innerActions;
        SelectRingAction(actions[selected], focusedRing);
      }
      else Refresh(false);
    }

    public override void Confirm()
    {
      if (!IsBound || TryRouteControlSubmit() || !hasTarget || Session.Selected == null) return;
      if (focusedRing == 0 && OuterGeometry != null) { focusedRing = 1; Refresh(false); return; }
      if (Session.Selected.HasChildren) Session.EnterSubmenuAsync(Session.Selected.Id).Forget();
      else Session.ActivateAsync().Forget();
    }

    public override void Back()
    {
      if (!IsBound) return;
      hasTarget = true;
      selector?.Reset();
      base.Back();
    }

    public override void SwitchRing()
    {
      if (!IsBound || OuterGeometry == null) return;
      focusedRing = focusedRing == 0 ? 1 : 0;
      selector?.Reset();
      hasTarget = true;
      Refresh(false);
    }

    public override void Page(int direction)
    {
      if (IsBound && focusedRing == 0 && OuterGeometry != null) Session.GoBack();
      selector?.Reset();
      base.Page(direction);
    }

    public override void EndHold()
    {
      if (!hasTarget && !PointerControlOwnsInput && !NativeControlHasFocus) Cancel();
      base.EndHold();
    }

    public override void InvokeSecondary(int index)
    { if (hasTarget || PointerControlOwnsInput || NativeControlHasFocus) base.InvokeSecondary(index); }

    public override void BeginHold()
    {
      base.BeginHold();
      hasTarget = false;
      selector?.Reset();
      Refresh(false);
    }

    public override void Unbind()
    {
      branchRequest++;
      boundsBindingGeneration++;
      bool registered = layoutCheckQueued;
      layoutCheckQueued = false; layoutPostScheduled = false; layoutSession = null;
      // During a registry callback the queue owns iteration. Leave the entry inert until
      // that pass clears it; otherwise unregister immediately on disable/replacement.
      if (registered && !CanvasUpdateRegistry.IsRebuildingLayout() && !CanvasUpdateRegistry.IsRebuildingGraphics())
        CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
      contentRects.Clear();
      for (int i = 0; i < graphics.Count; i++)
      {
        if (graphics[i] == null) continue;
        if (Application.isPlaying) Destroy(graphics[i].gameObject); else DestroyImmediate(graphics[i].gameObject);
      }
      graphics.Clear(); innerActions.Clear(); outerActions.Clear();
      InnerGeometry = OuterGeometry = null;
      selector = null; hasShape = false; hasTarget = true; focusedRing = 0;
      base.Unbind();
    }
  }

  /// <summary>Rectangular content roots still obey the exact native wedge when wedge visuals are enabled.</summary>
  public sealed class MenuSectorHitFilter : MonoBehaviour, ICanvasRaycastFilter
  {
    private RectTransform coordinateFrame;
    private RadialSegmentGeometry geometry;
    public void Configure(RectTransform frame, RadialSegmentGeometry value) { coordinateFrame = frame; geometry = value; }
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
      => !enabled || coordinateFrame != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
        coordinateFrame, screenPoint, eventCamera, out var localPoint) && geometry.Contains(localPoint);
  }

  /// <summary>Whole visible wedges receive native pointer events; action execution remains in the session.</summary>
  public sealed class MenuSectorPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
  {
    private Action select, activate, exit;
    public void Configure(Action selection, Action activation, Action pointerExit = null)
    { select = selection; activate = activation; exit = pointerExit; }
    public void OnPointerEnter(PointerEventData eventData) => select?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => exit?.Invoke();
    public void OnPointerClick(PointerEventData eventData)
    { if (eventData.button == PointerEventData.InputButton.Left) activate?.Invoke(); }
  }
}
