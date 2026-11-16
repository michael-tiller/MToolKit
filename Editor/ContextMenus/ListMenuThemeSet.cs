using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Authoring/List Theme Set", fileName = "ListMenuThemeSet")]
  public sealed class ListMenuThemeSet : MenuPresentationThemeSet
  {
    public MenuListLayout Layout = MenuListLayout.Default;
    public ListMenuThemeSet() { ShowCenter = false; }
    public override void ApplyTo(MenuProfile profile) { Layout.ApplyTo(profile); ApplyComposition(profile); }
  }
}
