using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Authoring/Pie Theme Set", fileName = "PieMenuThemeSet")]
  public sealed class PieMenuThemeSet : MenuPresentationThemeSet
  {
    public MenuWheelLayout Layout = new MenuWheelLayout { InnerRadius = 166, RingWidth = 120,
      StartAngle = 90, Clockwise = true, AngularGap = 3, DrawSegments = false };
    public PieMenuThemeSet() { WidgetSize = new Vector2(144, 56); ShowCenter = false; }
    public override void ApplyTo(MenuProfile profile) { Layout.ApplyTo(profile); ApplyComposition(profile); }
  }
}
