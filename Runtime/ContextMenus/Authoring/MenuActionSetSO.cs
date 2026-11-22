using System;
using System.Collections.Generic;
using UnityEngine;

namespace MToolKit.ContextMenus.Authoring
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Action Set", fileName = "ActionSet")]
  public sealed class MenuActionSetSO : ScriptableObject
  {
    public MenuActionDefinitionSO[] Actions = Array.Empty<MenuActionDefinitionSO>();
    public IReadOnlyList<MenuAction> Create(MenuContext context)
      => MenuDefinitionPlacement.CreateDefinitions(Actions, context, "action set '" + name + "'");
  }
}
