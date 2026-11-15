using System;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>Accessible list with inline foldouts, replacement or side-column hierarchy, and bounded paging.</summary>
  public sealed class ListMenuPresenter : MenuPresenterBase, IScrollHandler
  {
    private bool pointerTarget = true;
    private int visible;
    private int parentWidgetCount;

    protected override void Render(bool forceLayout)
    {
      var capacity = MenuScreenPlacement.LogicalCapacity(Root, Context.Canvas);
      int rows = Math.Min(Profile.MaximumVisibleItems, (int)((capacity.y - 44) / Profile.RowHeight));
      if (rows < 1 || capacity.x < Profile.ListWidth)
      { ReportCannotFit("The list needs at least one full readable row and its configured width."); return; }
      Session.PageSize = rows;
      visible = Session.Rows.Count;
      bool flyout = Profile.Navigation == MenuNavigationPolicy.Flyout && Session.ParentActions.Count > 0;
      float width = Profile.ListWidth * (flyout ? 2 : 1);
      if (width > capacity.x) { flyout = false; width = Profile.ListWidth; }
      int parentCount = flyout ? Math.Min(rows, Session.ParentActions.Count) : 0;
      parentWidgetCount = parentCount;
      float height = Math.Max(1, Math.Max(visible, parentCount)) * Profile.RowHeight + 44;
      if (!MenuScreenPlacement.TryPlace(Root, Context.Canvas, Context.ScreenPosition,
          new Vector2(width, height), out var diagnostic, Context.ResolveAnchor(Profile.Anchor)))
      { ReportCannotFit(diagnostic); return; }
      FitsViewport = true; PlacementDiagnostic = null;
      int widgetIndex = 0;
      if (flyout)
      {
        for (int i = 0; i < parentCount; i++)
        {
          var parent = Session.ParentActions[i];
          var item = Item(widgetIndex++);
          MenuWidgetFactory.Place(item.Rect, new Vector2(-Profile.ListWidth * .5f,
            height * .5f - Profile.RowHeight * (i + .5f)), new Vector2(Profile.ListWidth - 4, Profile.RowHeight));
          var selectedParent = parent;
          item.Bind(StateFor(parent, parent == Session.ParentSelected,
            Profile.InputBindings.RoutePointerClick ? null : () => EnterSiblingAsync(selectedParent).Forget(),
            () => EnterSiblingAsync(selectedParent).Forget()));
          ConfigureNavigation(item);
        }
      }
      for (int i = 0; i < visible; i++)
      {
        var row = Session.Rows[i];
        var action = row.Action;
        var item = Item(widgetIndex++);
        float indent = Math.Min(Profile.ListWidth * .35f, row.Depth * 18);
        MenuWidgetFactory.Place(item.Rect, new Vector2((flyout ? Profile.ListWidth * .5f : 0) + indent * .5f,
          height * .5f - Profile.RowHeight * (i + .5f)), new Vector2(Profile.ListWidth - indent - 4, Profile.RowHeight));
        var state = StateFor(action, Session.Selected == action, PointerActivation(action), () => SelectAction(action));
        if (item.DefaultWidget != null && action.HasChildren)
          state = state.WithLabel((row.Expanded ? "− " : "+ ") + state.Label);
        item.Bind(state);
        ConfigureNavigation(item);
      }
      FinishItems(widgetIndex);
      SetFooter(new Vector2(0, -height * .5f + 20), width);
      BindCenter(false);
    }

    public override void Confirm()
    {
      if (!IsBound || TryRouteControlSubmit() || Session.Selected == null || Session.InteractionMode == MenuInteractionMode.Pointer && !pointerTarget) return;
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
      if (TryRouteControlNavigation(direction, analog)) return;
      pointerTarget = true;
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
      MenuAction hovered = null;
      var camera = Context.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Context.Canvas.worldCamera;
      for (int i = 0; i < visible && i < Session.Rows.Count; i++)
      {
        var row = Session.Rows[i];
        var item = Item(parentWidgetCount + i);
        if (!RectTransformUtility.RectangleContainsScreenPoint(item.Rect, screenPosition, camera)) continue;
        hovered = row.Action;
        break;
      }
      bool changed = SetPointerHovered(hovered);
      var before = Session.Selected;
      if (hovered != null) pointerTarget = Session.Select(hovered.Id);
      if (changed && ReferenceEquals(before, Session.Selected)) Refresh(false);
    }

    public void OnScroll(PointerEventData eventData)
    {
      if (eventData.scrollDelta.y == 0) return;
      Page(eventData.scrollDelta.y < 0 ? 1 : -1);
      eventData.Use();
    }

    private async UniTask EnterSiblingAsync(MenuAction parent)
    {
      if (!IsBound || Session.ParentSelected == parent) return;
      var session = Session;
      session.GoBack();
      if (session.Select(parent.Id) && parent.HasChildren) await session.EnterSubmenuAsync(parent.Id);
    }
  }
}
