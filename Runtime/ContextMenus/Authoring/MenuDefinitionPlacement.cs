using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.ContextMenus.Authoring
{
  internal static class MenuDefinitionPlacement
  {
    internal static IReadOnlyList<MenuAction> CreateDefinitions(IReadOnlyList<MenuActionDefinitionSO> definitions,
      MenuContext context, string location)
    {
      if (context == null) throw new ArgumentNullException(nameof(context));
      if (definitions == null || definitions.Count == 0) return Array.Empty<MenuAction>();
      var actions = new MenuAction[definitions.Count];
      var ids = new HashSet<MenuActionId>();
      for (int i = 0; i < actions.Length; i++)
      {
        if (definitions[i] == null)
          throw new InvalidOperationException("Missing menu action definition at " + location + "[" + i + "].");
        var action = definitions[i].Create(context);
        if (!ids.Add(action.Id))
          throw new InvalidOperationException("Duplicate menu action Id '" + action.Id + "' at " + location + ".");
        actions[i] = action;
      }
      return Array.AsReadOnly(actions);
    }

    internal static IReadOnlyList<MenuAction> Prefix(IReadOnlyList<MenuAction> source, string prefix)
    {
      if (source == null || source.Count == 0) return Array.Empty<MenuAction>();
      var actions = new MenuAction[source.Count];
      var ids = new HashSet<MenuActionId>();
      for (int i = 0; i < actions.Length; i++)
      {
        var action = source[i] ?? throw new InvalidOperationException("Missing menu action at '" + prefix + "[" + i + "]'.");
        if (!ids.Add(action.Id))
          throw new InvalidOperationException("Duplicate menu action Id '" + action.Id + "' under '" + prefix + "'.");
        actions[i] = new MenuAction(new MenuActionId(prefix + "/" + action.Id.Value), action.Label, action.Behavior,
          Prefix(action.Children, prefix), Prefix(action.ChildrenProvider, prefix), action.PrimaryBehavior,
          Prefix(action.SecondaryActions, prefix), action.Evaluate, action.Description, action.Icon,
          action.BindingHint, action.Metadata, action.PresentationOverride, action.Image).InheritChanges(action);
      }
      return Array.AsReadOnly(actions);
    }

    internal static IMenuChildrenProvider Prefix(IMenuChildrenProvider provider, string prefix)
      => provider == null ? null : new ScopedProvider(provider, prefix);

    private sealed class ScopedProvider : IMenuChildrenProvider
    {
      private readonly IMenuChildrenProvider source;
      private readonly string prefix;
      public ScopedProvider(IMenuChildrenProvider source, string prefix) { this.source = source; this.prefix = prefix; }
      public async UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken)
      {
        bool unityOwner = PlayerLoopHelper.IsMainThread && PlayerLoopHelper.UnitySynchronizationContext != null;
        cancellationToken.ThrowIfCancellationRequested();
        if (context == null || !context.IsValid) return Array.Empty<MenuAction>();
        using var linked = context.CancellationToken.CanBeCanceled
          ? CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken) : null;
        var token = linked?.Token ?? cancellationToken;
        IReadOnlyList<MenuAction> actions;
        try { actions = await source.ResolveAsync(context, token); }
        finally
        {
          if (unityOwner && !PlayerLoopHelper.IsMainThread) await UniTask.SwitchToMainThread();
        }
        token.ThrowIfCancellationRequested();
        if (!context.IsValid) return Array.Empty<MenuAction>();
        return Prefix(actions, prefix);
      }
    }
  }
}
