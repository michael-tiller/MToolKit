using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Authoring/Nested Wheel Theme Set", fileName = "NestedWheelMenuThemeSet")]
  public sealed class NestedWheelMenuThemeSet : MenuPresentationThemeSet
  {
    public MenuNestedWheelLayout Layout = MenuNestedWheelLayout.Default;
    public override void ApplyTo(MenuProfile profile) { Layout.ApplyTo(profile); ApplyComposition(profile); }
  }
}
