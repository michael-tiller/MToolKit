using System;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Presentation;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  /// <summary>Example complete presenter replacement: an authored two-column card panel, with shared session behavior.</summary>
  public sealed class GalleryCustomPresenter : MenuPresenterBase
  {
    private int count;
    private bool pointerTarget;
    protected override void Render(bool forceLayout)
    {
      var capacity = MenuScreenPlacement.LogicalCapacity(Root, Context.Canvas);
      float cardHeight = Math.Max(72, Profile.RowHeight);
      float width = Math.Min(Profile.ListWidth * 1.5f, capacity.x);
      if (width < Profile.MinimumTargetSize * 2 || capacity.y < cardHeight + 44)
      { ReportCannotFit("The custom card panel needs two readable targets and one complete row."); return; }
      int rows = Math.Max(1, Math.Min(Profile.MaximumVisibleItems / 2, (int)((capacity.y - 44) / cardHeight)));
      Session.PageSize = rows * 2;
      count = Session.Rows.Count;
      float height = Math.Max(1, (count + 1) / 2) * cardHeight + 44;
      if (!MenuScreenPlacement.TryPlace(Root, Context.Canvas, Context.ScreenPosition,
          new Vector2(width, height), out var diagnostic, Profile.Anchor))
      { ReportCannotFit(diagnostic); return; }
      FitsViewport = true; PlacementDiagnostic = null;
      for (int i = 0; i < count; i++)
      {
        var action = Session.Rows[i].Action;
        var item = Item(i);
        Place(item.Rect, new Vector2((i % 2 == 0 ? -1 : 1) * width * .25f,
          height * .5f - cardHeight * (i / 2 + .5f)), new Vector2(width * .5f - 8, cardHeight - 8));
        item.Bind(StateFor(action, Session.Selected == action, PointerActivation(action), () => SelectAction(action)));
        ConfigureNavigation(item);
      }
      FinishItems(count);
      SetFooter(new Vector2(0, -height * .5f + 20), width);
      BindCenter(false);
    }
    public override void Point(Vector2 position)
    {
      base.Point(position);
      if (!IsBound) return;
      pointerTarget = false;
      var camera = Context.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Context.Canvas.worldCamera;
      for (int i = 0; i < count; i++)
        if (RectTransformUtility.RectangleContainsScreenPoint(Item(i).Rect, position, camera))
        { pointerTarget = true; Session.Select(Session.Rows[i].Action.Id); break; }
    }
    public override void Navigate(Vector2 direction, bool analog)
    { pointerTarget = true; base.Navigate(direction, analog); }
    public override void Confirm()
    {
      if (!IsBound || Session.Selected == null || Session.InteractionMode == MenuInteractionMode.Pointer && !pointerTarget) return;
      if (Session.Selected.HasChildren) Session.EnterSubmenuAsync(Session.Selected.Id).Forget();
      else Session.ActivateAsync().Forget();
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
      rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
      rect.anchoredPosition = position; rect.sizeDelta = size;
    }
  }
}
