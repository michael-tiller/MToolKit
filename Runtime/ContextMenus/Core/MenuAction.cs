using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.ContextMenus
{
  public interface IMenuActionBehavior
  {
    bool IsInteractive { get; }
    UniTask<MenuActionOutcome> ActivateAsync(MenuAction action, MenuContext context, CancellationToken cancellationToken);
  }

  public interface IMenuChildrenProvider
  {
    UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken);
  }

  /// <summary>Runtime hierarchy descriptor. Mutable gameplay state is evaluated through the supplied state delegate.</summary>
  public sealed class MenuAction
  {
    private readonly Func<MenuContext, MenuActionState> state;
    private Action changed;
    private MenuAction changeSource;
    public MenuAction(MenuActionId id, MenuLabel label, IMenuActionBehavior behavior = null,
      IReadOnlyList<MenuAction> children = null, IMenuChildrenProvider childrenProvider = null,
      IMenuActionBehavior primary = null, IReadOnlyList<MenuAction> secondaryActions = null,
      Func<MenuContext, MenuActionState> state = null, MenuLabel description = default,
      Sprite icon = null, InputActionReference bindingHint = null, object metadata = null,
      object presentationOverride = null)
    {
      if (!id.IsValid) throw new ArgumentException("An action requires a stable identity.", nameof(id));
      Id = id; Label = label; Children = Snapshot(children);
      ChildrenProvider = childrenProvider; PrimaryBehavior = primary;
      Behavior = behavior ?? (HasChildren ? (IMenuActionBehavior)new MenuSubmenuBehavior() : new MenuHeaderBehavior());
      SecondaryActions = Snapshot(secondaryActions); this.state = state;
      Description = description; Icon = icon; BindingHint = bindingHint; Metadata = metadata;
      PresentationOverride = presentationOverride;
    }
    public MenuActionId Id { get; }
    public MenuLabel Label { get; }
    public MenuLabel Description { get; }
    public Sprite Icon { get; }
    public InputActionReference BindingHint { get; }
    public object Metadata { get; }
    /// <summary>Opaque use-case override interpreted by the host; actions never inspect presenter types.</summary>
    public object PresentationOverride { get; }
    public IMenuActionBehavior Behavior { get; }
    public IMenuActionBehavior PrimaryBehavior { get; }
    public IReadOnlyList<MenuAction> Children { get; }
    public IMenuChildrenProvider ChildrenProvider { get; }
    public IReadOnlyList<MenuAction> SecondaryActions { get; }
    public bool HasChildren => ChildrenProvider != null || Children.Count > 0;
    // Authored placements may share a definition while carrying a different stable path.
    // Forward subscriptions directly; the placement creates no hidden subscription of its own.
    public event Action Changed
    {
      add { if (changeSource != null) changeSource.Changed += value; else changed += value; }
      remove { if (changeSource != null) changeSource.Changed -= value; else changed -= value; }
    }
    public MenuActionState Evaluate(MenuContext context)
      => context == null || !context.IsValid ? new MenuActionState(false, false, disabledReason: "invalid_context")
        : state?.Invoke(context) ?? MenuActionState.Available;
    public void NotifyChanged()
    {
      if (changeSource != null) changeSource.NotifyChanged();
      else changed?.Invoke();
    }

    internal MenuAction InheritChanges(MenuAction source)
    {
      if (source == null || ReferenceEquals(source, this)) throw new ArgumentException("A distinct change source is required.", nameof(source));
      if (changeSource != null || changed != null) throw new InvalidOperationException("Assign the change source before subscribing to the descriptor.");
      changeSource = source;
      return this;
    }

    private static IReadOnlyList<MenuAction> Snapshot(IReadOnlyList<MenuAction> source)
    {
      if (source == null || source.Count == 0) return Array.Empty<MenuAction>();
      var copy = new MenuAction[source.Count];
      for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
      return Array.AsReadOnly(copy);
    }
  }
}
