using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.ContextMenus
{
  /// <summary>A concrete command remains captured by the caller; no generic message erasure occurs.</summary>
  public class MenuCommandBehavior : IMenuActionBehavior
  {
    private readonly Func<MenuContext, CancellationToken, UniTask<MenuActionOutcome>> execute;
    public MenuCommandBehavior(Action<MenuContext> execute, MenuActionOutcome outcome = MenuActionOutcome.Close)
    {
      if (execute == null) throw new ArgumentNullException(nameof(execute));
      this.execute = (context, _) => { execute(context); return UniTask.FromResult(outcome); };
    }
    public MenuCommandBehavior(Func<MenuContext, CancellationToken, UniTask<MenuActionOutcome>> execute)
      => this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
    public virtual bool IsInteractive => true;
    public virtual UniTask<MenuActionOutcome> ActivateAsync(MenuAction action, MenuContext context, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (context == null || !context.IsValid) return UniTask.FromResult(MenuActionOutcome.Keep);
      return execute(context, cancellationToken);
    }
  }

  public sealed class MenuToggleBehavior : MenuCommandBehavior
  {
    public MenuToggleBehavior(Action<MenuContext> toggle, MenuActionOutcome outcome = MenuActionOutcome.Refresh) : base(toggle, outcome) { }
  }

  public sealed class MenuChoiceBehavior : MenuCommandBehavior
  {
    public MenuChoiceBehavior(Action<MenuContext> select, MenuActionOutcome outcome = MenuActionOutcome.Refresh) : base(select, outcome) { }
  }

  public sealed class MenuSubmenuBehavior : IMenuActionBehavior
  {
    public bool IsInteractive => true;
    public UniTask<MenuActionOutcome> ActivateAsync(MenuAction action, MenuContext context, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return UniTask.FromResult(MenuActionOutcome.Enter);
    }
  }

  public sealed class MenuHeaderBehavior : IMenuActionBehavior
  {
    public bool IsInteractive => false;
    public UniTask<MenuActionOutcome> ActivateAsync(MenuAction action, MenuContext context, CancellationToken cancellationToken)
      => UniTask.FromResult(MenuActionOutcome.Keep);
  }

  public sealed class DelegateMenuChildrenProvider : IMenuChildrenProvider
  {
    private readonly Func<MenuContext, CancellationToken, UniTask<IReadOnlyList<MenuAction>>> resolve;
    public DelegateMenuChildrenProvider(Func<MenuContext, CancellationToken, UniTask<IReadOnlyList<MenuAction>>> resolve)
      => this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    public UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return resolve(context, cancellationToken);
    }
  }
}
