using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>Accessible hierarchy with bounded pagination or native clipped scrolling.</summary>
  public sealed class ListMenuPresenter : MenuPresenterBase, IScrollHandler
  {
    private bool pointerTarget = true;
    private int visible;
    private int parentWidgetCount;
    private int viewportRows;
    private bool pointerParentTarget, selectingFromPointer;
    private long bindingRevision;
    private MenuActionId lastSelection, lastParent;
    private int lastSelectionIndex = -1;
    private readonly List<MenuWidgetInstance> ownedRows = new List<MenuWidgetInstance>();
    private ScrollRect currentScroll, parentScroll;

    private bool Scrolling => Profile != null && Profile.ListOverflow == MenuListOverflowPolicy.Scrolling;
    protected override bool PreferNativeItemPointer => Scrolling;
    protected override bool AcceptNativePointerHover => !Scrolling || Session.InteractionMode == MenuInteractionMode.Pointer ||
      Context.ResolveInputAction(Profile.InputBindings.Point) == null;

    protected override bool NativeItemPointerOwnsConfirmation()
    {
      if (!Scrolling || PointerHitObject == null) return false;
      var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(PointerHitObject);
      if (handler == null) return false;
      for (int i = 0; i < ownedRows.Count; i++)
      {
        var item = ownedRows[i];
        if (item.State != null && item.GameObject != null && item.GameObject.activeInHierarchy &&
            handler.transform.IsChildOf(item.Rect)) return true;
      }
      return false;
    }

    protected override void Render(bool forceLayout)
    {
      var capacity = MenuScreenPlacement.LogicalCapacity(Root, Context.Canvas);
      int rows = Math.Min(Profile.MaximumVisibleItems,
        (int)((capacity.y - (Profile.ShowBackControl ? 44 : 0)) / Profile.RowHeight));
      if (rows < 1 || capacity.x < Profile.ListWidth)
      { ReportCannotFit("The list needs at least one full readable row and its configured width."); return; }
      var renderingSession = Session;
      long revision = bindingRevision;
      bool scrolling = Scrolling;
      Session.PageSize = scrolling ? int.MaxValue : rows;
      if (!Owns(renderingSession, revision)) return;
      bool flyout = Profile.Navigation == MenuNavigationPolicy.Flyout && Session.ParentActions.Count > 0;
      float width = Profile.ListWidth * (flyout ? 2 : 1);
      if (width > capacity.x) { flyout = false; width = Profile.ListWidth; }
      SetFooter(Vector2.zero, width, !scrolling);
      if (!Owns(renderingSession, revision)) return;
      float footerHeight = NavigationControlSize.y > 0 ? Mathf.Max(44, NavigationControlSize.y + 8) : 0;
      int fittingRows = Math.Min(rows, (int)((capacity.y - footerHeight) / Profile.RowHeight));
      if (fittingRows < 1)
      { ReportCannotFit("The list needs one full row and its readable navigation controls."); return; }
      if (fittingRows < rows)
      {
        rows = fittingRows;
        Session.PageSize = scrolling ? int.MaxValue : rows;
        if (!Owns(renderingSession, revision)) return;
        SetFooter(Vector2.zero, width, !scrolling);
        if (!Owns(renderingSession, revision)) return;
      }
      viewportRows = rows;
      visible = Session.Rows.Count;
      width = Mathf.Max(width, NavigationControlSize.x);
      int parentCount = flyout ? (scrolling ? Session.ParentActions.Count : Math.Min(rows, Session.ParentActions.Count)) : 0;
      parentWidgetCount = parentCount;
      int displayedRows = Math.Max(1, Math.Max(visible, parentCount));
      if (scrolling) displayedRows = Math.Min(rows, displayedRows);
      float height = displayedRows * Profile.RowHeight + footerHeight;
      if (!MenuScreenPlacement.TryPlace(Root, Context.Canvas, Context.ScreenPosition,
          new Vector2(width, height), out var diagnostic, Context.ResolveAnchor(Profile.Anchor)))
      { ReportCannotFit(diagnostic); return; }
      FitsViewport = true; PlacementDiagnostic = null;
      float contentHeight = displayedRows * Profile.RowHeight;
      if (scrolling)
      {
        ConfigureScroll(ref currentScroll, "Menu rows", flyout ? Profile.ListWidth * .5f : 0,
          contentHeight, visible, footerHeight);
        if (flyout) ConfigureScroll(ref parentScroll, "Menu parents", -Profile.ListWidth * .5f,
          contentHeight, parentCount, footerHeight);
        else if (parentScroll != null) parentScroll.gameObject.SetActive(false);
      }
      else
      {
        // Only framework-owned widget roots move; custom prefab descendants retain their layout.
        for (int i = 0; i < ownedRows.Count; i++)
          if (ownedRows[i].GameObject != null && ownedRows[i].Rect.parent != Root) ownedRows[i].Rect.SetParent(Root, false);
        if (currentScroll != null) currentScroll.gameObject.SetActive(false);
        if (parentScroll != null) parentScroll.gameObject.SetActive(false);
      }
      int widgetIndex = 0;
      if (flyout)
      {
        for (int i = 0; i < parentCount; i++)
        {
          var parent = Session.ParentActions[i];
          var item = RowItem(widgetIndex++);
          PlaceRow(item, scrolling ? parentScroll : null, -Profile.ListWidth * .5f, height, i, 0);
          var selectedParent = parent;
          item.Bind(StateFor(parent, parent == Session.ParentSelected,
            Profile.InputBindings.RoutePointerClick && !scrolling ? null : () => EnterSiblingAsync(selectedParent).Forget(),
            () => EnterSiblingAsync(selectedParent).Forget()));
          if (!Owns(renderingSession, revision)) return;
          ConfigureNavigation(item);
        }
      }
      for (int i = 0; i < visible; i++)
      {
        var row = Session.Rows[i];
        var action = row.Action;
        var item = RowItem(widgetIndex++);
        float indent = Math.Min(Profile.ListWidth * .35f, row.Depth * 18);
        PlaceRow(item, scrolling ? currentScroll : null, flyout ? Profile.ListWidth * .5f : 0, height, i, indent);
        var state = StateFor(action, Session.Selected == action, PointerActivation(action), () => SelectRow(action));
        if (item.DefaultWidget != null && action.HasChildren)
          state = state.WithLabel((row.Expanded ? "− " : "+ ") + state.Label);
        item.Bind(state);
        if (!Owns(renderingSession, revision)) return;
        ConfigureNavigation(item);
      }
      FinishItems(widgetIndex);
      if (!Owns(renderingSession, revision)) return;
      PositionFooter(new Vector2(0, -height * .5f + footerHeight * .5f));
      BindCenter(false);
      if (!Owns(renderingSession, revision)) return;
      int selectedIndex = SelectedIndex();
      var selectedId = Session.Selected?.Id ?? default;
      var parentId = Session.ParentSelected?.Id ?? default;
      bool branchChanged = parentId != lastParent;
      if (scrolling && !selectingFromPointer &&
          (selectedId != lastSelection || selectedIndex != lastSelectionIndex || branchChanged)) RevealSelected();
      if (scrolling && flyout && branchChanged) RevealParent();
      lastSelection = selectedId; lastSelectionIndex = selectedIndex; lastParent = parentId;
    }

    private bool Owns(MenuSession session, long revision)
      => IsBound && ReferenceEquals(Session, session) && bindingRevision == revision;

    private MenuWidgetInstance RowItem(int index)
    {
      var item = Item(index);
      while (ownedRows.Count <= index) ownedRows.Add(item);
      return item;
    }

    protected override void OnNavigationLayoutChanged() => Refresh(false);

    private void ConfigureScroll(ref ScrollRect scroll, string name, float x, float height, int count, float footerHeight)
    {
      if (scroll == null)
      {
        var root = MenuWidgetFactory.Rect(name, Root);
        scroll = root.gameObject.AddComponent<ScrollRect>();
        var viewport = MenuWidgetFactory.Rect("Viewport", root);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = MenuWidgetFactory.Rect("Content", viewport);
        content.anchorMin = content.anchorMax = new Vector2(.5f, 1);
        content.pivot = new Vector2(.5f, 1);
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = Profile.RowHeight;
        var input = root.gameObject.AddComponent<MenuListScrollPointer>();
        var bindingSession = Session;
        long revision = bindingRevision;
        input.Configure(position => { if (Owns(bindingSession, revision)) Point(position); });
      }
      scroll.gameObject.SetActive(true);
      MenuWidgetFactory.Place((RectTransform)scroll.transform, new Vector2(x, footerHeight * .5f), new Vector2(Profile.ListWidth, height));
      scroll.content.sizeDelta = new Vector2(Profile.ListWidth, Math.Max(1, count) * Profile.RowHeight);
      var position = scroll.content.anchoredPosition;
      position.x = 0; position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, scroll.content.rect.height - height));
      scroll.content.anchoredPosition = position;
    }

    private void PlaceRow(MenuWidgetInstance item, ScrollRect scroll, float x, float height, int index, float indent)
    {
      var parent = scroll != null ? scroll.content : Root;
      if (item.Rect.parent != parent) item.Rect.SetParent(parent, false);
      float top = scroll != null ? scroll.content.rect.height * .5f : height * .5f;
      MenuWidgetFactory.Place(item.Rect, new Vector2((scroll != null ? 0 : x) + indent * .5f,
        top - Profile.RowHeight * (index + .5f)), new Vector2(Profile.ListWidth - indent - 4, Profile.RowHeight));
    }

    private int SelectedIndex()
    {
      for (int i = 0; i < Session.Rows.Count; i++) if (Session.Rows[i].Action == Session.Selected) return i;
      return -1;
    }

    private void RevealSelected()
    {
      int selected = SelectedIndex();
      if (selected >= 0 && parentWidgetCount + selected < ownedRows.Count)
        ScrollIntoViewOnSelect.EnsureVisible(ownedRows[parentWidgetCount + selected].Rect);
    }

    private void RevealParent()
    {
      for (int i = 0; i < parentWidgetCount; i++)
        if (ownedRows[i].State?.Action == Session.ParentSelected) { ScrollIntoViewOnSelect.EnsureVisible(ownedRows[i].Rect); return; }
    }

    private void SelectRow(MenuAction action)
    {
      if (!IsBound) return;
      bool previous = selectingFromPointer;
      selectingFromPointer = Session.InteractionMode == MenuInteractionMode.Pointer;
      try { SelectAction(action); }
      finally { selectingFromPointer = previous; }
    }

    public override void Confirm()
    {
      if (!IsBound || TryRouteControlSubmit() || Session.Selected == null ||
          Session.InteractionMode == MenuInteractionMode.Pointer && (!pointerTarget || pointerParentTarget)) return;
      var action = Session.Selected;
      if (action.HasChildren && Profile.Navigation == MenuNavigationPolicy.Inline)
      {
        for (int i = 0; i < Session.Rows.Count; i++)
          if (Session.Rows[i].Action == action && Session.Rows[i].Expanded) { Session.Collapse(action.Id); return; }
        Session.ExpandAsync(action.Id).Forget();
      }
      else if (action.HasChildren) Session.EnterSubmenuAsync(action.Id).Forget();
      else Session.ActivateAsync().Forget();
    }

    public override void Navigate(Vector2 direction, bool analog)
    {
      if (!IsBound) return;
      if (TryRouteControlNavigation(direction, analog)) { CancelSelectionReset(); return; }
      if (HandleSelectionReset(direction, analog)) return;
      pointerTarget = true;
      pointerParentTarget = false;
      if (Math.Abs(direction.x) > Math.Abs(direction.y) && Session.Selected != null)
      {
        Session.InteractionMode = analog ? MenuInteractionMode.Controller : MenuInteractionMode.Keyboard;
        SetPointerHovered(null);
        if (direction.x > 0 && Session.Selected.HasChildren) Confirm();
        else if (direction.x < 0) Back();
        return;
      }
      base.Navigate(direction, analog);
    }

    public override void Point(Vector2 screenPosition)
    {
      base.Point(screenPosition);
      if (!IsBound) return;
      if (PointerControlOwnsInput) return;
      pointerTarget = false;
      pointerParentTarget = false;
      MenuAction hovered = null;
      var camera = Context.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Context.Canvas.worldCamera;
      for (int i = 0; i < parentWidgetCount; i++)
      {
        var item = RowItem(i);
        if (!HitRow(item, Scrolling ? parentScroll : null, screenPosition, camera)) continue;
        pointerParentTarget = true;
        var parent = item.State?.Action;
        if (parent != null) EnterSiblingAsync(parent).Forget();
        return;
      }
      for (int i = 0; i < visible && i < Session.Rows.Count; i++)
      {
        var row = Session.Rows[i];
        var item = RowItem(parentWidgetCount + i);
        if (!HitRow(item, Scrolling ? currentScroll : null, screenPosition, camera)) continue;
        hovered = row.Action;
        break;
      }
      if (hovered != null && !hovered.Behavior.IsInteractive) hovered = null;
      bool changed = SetPointerHovered(hovered);
      var before = Session.Selected;
      if (hovered != null) { SelectRow(hovered); pointerTarget = Session.Selected == hovered; }
      else ClearPointerSelection();
      if (changed && ReferenceEquals(before, Session.Selected)) Refresh(false);
    }

    public void OnScroll(PointerEventData eventData)
    {
      // A child ScrollRect owns wheel/drag input in scrolling mode; do not also turn a page.
      if (Scrolling) return;
      if (eventData.scrollDelta.y == 0) return;
      Page(eventData.scrollDelta.y < 0 ? 1 : -1);
      eventData.Use();
    }

    private static bool HitRow(MenuWidgetInstance item, ScrollRect scroll, Vector2 point, Camera camera)
      => item.GameObject != null && item.GameObject.activeInHierarchy &&
        (scroll == null || scroll.viewport != null && RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, camera)) &&
        RectTransformUtility.RectangleContainsScreenPoint(item.Rect, point, camera);

    public override void Page(int direction)
    {
      if (!IsBound || !Scrolling) { base.Page(direction); return; }
      pointerTarget = true; pointerParentTarget = false;
      if (Session.InteractionMode == MenuInteractionMode.Pointer) Session.InteractionMode = MenuInteractionMode.Keyboard;
      SetPointerHovered(null);
      int selected = SelectedIndex();
      int next = Mathf.Clamp(Math.Max(0, selected) + (direction > 0 ? viewportRows : -viewportRows), 0, Session.Rows.Count - 1);
      int step = direction > 0 ? 1 : -1;
      for (int i = next; i >= 0 && i < Session.Rows.Count; i += step)
        if (Session.Rows[i].Action.Behavior.IsInteractive && Session.Select(Session.Rows[i].Action.Id)) break;
      RevealSelected();
    }

    public override void Unbind()
    {
      bindingRevision++;
      var oldCurrent = currentScroll; var oldParents = parentScroll;
      currentScroll = parentScroll = null;
      ownedRows.Clear(); lastSelection = lastParent = default; lastSelectionIndex = -1;
      pointerParentTarget = selectingFromPointer = false; pointerTarget = true;
      visible = parentWidgetCount = viewportRows = 0;
      try { base.Unbind(); }
      finally
      {
        DestroyScroll(oldCurrent); DestroyScroll(oldParents);
      }
    }

    private static void DestroyScroll(ScrollRect scroll)
    {
      if (scroll == null) return;
      scroll.StopMovement(); scroll.gameObject.SetActive(false);
      if (Application.isPlaying) Destroy(scroll.gameObject); else DestroyImmediate(scroll.gameObject);
    }

    private async UniTask EnterSiblingAsync(MenuAction parent)
    {
      if (!IsBound || Session.ParentSelected == parent) return;
      var session = Session;
      session.GoBack();
      if (session.Select(parent.Id) && parent.HasChildren) await session.EnterSubmenuAsync(parent.Id);
    }
  }

  /// <summary>Deliberate native scroll gestures hand off pointer mode even when position is stationary.</summary>
  internal sealed class MenuListScrollPointer : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler
  {
    private Action<Vector2> point;
    internal void Configure(Action<Vector2> callback) => point = callback;
    public void OnScroll(PointerEventData eventData)
    { if (eventData.scrollDelta != Vector2.zero) point?.Invoke(eventData.position); }
    public void OnBeginDrag(PointerEventData eventData)
    { if (eventData.button == PointerEventData.InputButton.Left) point?.Invoke(eventData.position); }
    public void OnDrag(PointerEventData eventData)
    { if (eventData.button == PointerEventData.InputButton.Left) point?.Invoke(eventData.position); }
    private void OnDestroy() => point = null;
  }
}
