using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Authoring/Wheel Theme Set", fileName = "WheelMenuThemeSet")]
  public sealed class WheelMenuThemeSet : MenuPresentationThemeSet
  {
    public MenuWheelLayout Layout = MenuWheelLayout.Default;
    public override void ApplyTo(MenuProfile profile) { Layout.ApplyTo(profile); ApplyComposition(profile); }
  }
}
