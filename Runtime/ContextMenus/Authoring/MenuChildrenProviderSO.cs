using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MToolKit.ContextMenus.Authoring
{
  /// <summary>Resolve fresh runtime children on entry or explicit refresh; never store target state on the asset.</summary>
  public abstract class MenuChildrenProviderSO : ScriptableObject, IMenuChildrenProvider
  {
    public abstract UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken);
  }
}
