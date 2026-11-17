using UnityEngine;

namespace MToolKit.ContextMenus.Authoring
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Header or Separator", fileName = "Header")]
  public sealed class MenuHeaderDefinitionSO : MenuActionDefinitionSO
  {
    protected override MenuAction CreateAction(MenuContext context)
      => new MenuAction(Id, LabelDescriptor, new MenuHeaderBehavior());
  }
}
