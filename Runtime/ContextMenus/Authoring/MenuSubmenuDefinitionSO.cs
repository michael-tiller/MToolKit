using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.ContextMenus.Authoring
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Submenu", fileName = "Submenu")]
  public sealed class MenuSubmenuDefinitionSO : MenuActionDefinitionSO
  {
    public MenuActionDefinitionSO[] Children = Array.Empty<MenuActionDefinitionSO>();
    public MenuChildrenProviderSO Provider;
    public MenuActionDefinitionSO Primary;
    public MenuProfile ProfileOverride;
    protected override MenuAction CreateAction(MenuContext context)
      => new MenuAction(Id, LabelDescriptor, new MenuSubmenuBehavior(),
        MenuDefinitionPlacement.CreateDefinitions(Children, context, Id + " children"), Provider,
        primary: Primary != null ? new AuthoredPrimary(Primary.Create(context)) : null,
        presentationOverride: ProfileOverride);

    private sealed class AuthoredPrimary : IMenuActionBehavior
    {
      private readonly MenuAction action;
      public AuthoredPrimary(MenuAction action) => this.action = action;
      public bool IsInteractive => action.Behavior.IsInteractive;
      public UniTask<MenuActionOutcome> ActivateAsync(MenuAction _, MenuContext context, CancellationToken cancellationToken)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var state = action.Evaluate(context);
        if (!state.Visible || !state.Enabled || !IsInteractive) return UniTask.FromResult(MenuActionOutcome.Keep);
        return action.Behavior.ActivateAsync(action, context, cancellationToken);
      }
    }
  }
}
