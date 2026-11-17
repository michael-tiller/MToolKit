using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.ContextMenus
{
  /// <summary>Presentation-independent menu path, inline expansion, selection, execution and lifecycle.</summary>
  public sealed class MenuSession : IDisposable
  {
    private sealed class Frame
    {
      public readonly IReadOnlyList<MenuAction> Source;
      public readonly List<MenuAction> Visible = new List<MenuAction>();
      public readonly IReadOnlyList<MenuAction> VisibleView;
      public readonly MenuAction Parent;
      public readonly bool SecondaryCollection;
      public MenuAction Selection;
      public bool SelectionCleared;
      public int Page;
      public Frame(IReadOnlyList<MenuAction> source, MenuAction parent = null, bool secondaryCollection = false)
      { Source = source; Parent = parent; SecondaryCollection = secondaryCollection; VisibleView = Visible.AsReadOnly(); }
    }

    private readonly List<Frame> frames = new List<Frame>();
    private readonly List<MenuActionId> path = new List<MenuActionId>();
    private readonly List<MenuRow> allRows = new List<MenuRow>();
    private readonly List<MenuRow> rows = new List<MenuRow>();
    private readonly Dictionary<MenuAction, IReadOnlyList<MenuAction>> expanded = new Dictionary<MenuAction, IReadOnlyList<MenuAction>>();
    private readonly Dictionary<MenuAction, long> expansionRequests = new Dictionary<MenuAction, long>();
    private readonly HashSet<MenuAction> subscribed = new HashSet<MenuAction>();
    private readonly IReadOnlyList<MenuActionId> pathView;
    private readonly IReadOnlyList<MenuRow> rowsView;
    private CancellationTokenSource cancellation;
    private CancellationTokenRegistration cancellationRegistration;
    private IDisposable inputLease;
    private bool disposed;
    private bool rebuilding;
    private bool unityOwner;
    private long generation, navigationRequest, navigationIntent, nextExpansionRequest;
    private int pageSize = int.MaxValue;
    private MenuInteractionMode interactionMode;

    public MenuSession()
    { pathView = path.AsReadOnly(); rowsView = rows.AsReadOnly(); }

    public bool IsOpen { get; private set; }
    public bool IsExecuting { get; private set; }
    public MenuContext Context { get; private set; }
    public MenuAction Selected { get; private set; }
    // Availability/provider refresh also advances navigationRequest; it does not change user intent.
    internal long NavigationRevision => navigationIntent;
    public IReadOnlyList<MenuAction> Actions => frames.Count == 0 ? Array.Empty<MenuAction>() : Current.VisibleView;
    public IReadOnlyList<MenuAction> RootActions => frames.Count == 0 ? Array.Empty<MenuAction>() : frames[0].VisibleView;
    public IReadOnlyList<MenuAction> ParentActions => frames.Count < 2 ? Array.Empty<MenuAction>() : frames[frames.Count - 2].VisibleView;
    public MenuAction ParentSelected => frames.Count < 2 ? null : Current.Parent;
    public IReadOnlyList<MenuActionId> Path => pathView;
    /// <summary>Current parent descriptors, in the same order as Path; includes dynamic and secondary navigation.</summary>
    public IReadOnlyList<MenuAction> PathActions
    {
      get
      {
        if (frames.Count <= 1) return Array.Empty<MenuAction>();
        var parents = new MenuAction[frames.Count - 1];
        for (int i = 0; i < parents.Length; i++) parents[i] = frames[i + 1].Parent;
        return Array.AsReadOnly(parents);
      }
    }
    public IReadOnlyList<MenuRow> Rows => rowsView;
    public MenuInteractionMode InteractionMode
    {
      get => interactionMode;
      set { if (interactionMode == value) return; interactionMode = value; if (IsOpen) Changed?.Invoke(); }
    }
    public int PageSize
    {
      get => pageSize;
      set
      {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "A menu page must contain at least one row.");
        if (pageSize == value) return;
        pageSize = value;
        if (IsOpen) Rebuild();
      }
    }
    public int PageIndex => frames.Count == 0 ? 0 : Current.Page;
    public int PageCount => allRows.Count == 0 ? 1 : (allRows.Count - 1) / pageSize + 1;
    public event Action Changed;
    public event Action Closed;
    private Frame Current => frames[frames.Count - 1];

    public void Open(IReadOnlyList<MenuAction> actions, MenuContext context, IDisposable inputLease = null)
    {
      if (disposed) throw new ObjectDisposedException(nameof(MenuSession));
      if (context == null) throw new ArgumentNullException(nameof(context));
      IReadOnlyList<MenuAction> snapshot;
      try { snapshot = SnapshotAndValidate(actions); }
      catch { inputLease?.Dispose(); throw; }
      if (IsOpen) Close();
      // A Closed callback is allowed to open a newer session and owns its new lease.
      if (IsOpen) { inputLease?.Dispose(); return; }
      bool valid;
      try { valid = context.IsValid; }
      catch { inputLease?.Dispose(); throw; }
      if (!valid) { inputLease?.Dispose(); return; }
      unityOwner = PlayerLoopHelper.IsMainThread && PlayerLoopHelper.UnitySynchronizationContext != null;
      generation++;
      navigationRequest++;
      Context = context;
      this.inputLease = inputLease;
      cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
      frames.Add(new Frame(snapshot));
      IsOpen = true;
      context.Invalidated += Close;
      cancellationRegistration = context.CancellationToken.Register(Close);
      try { if (IsOpen) Rebuild(); }
      catch { Close(); throw; }
    }

    public void Close()
    {
      if (!IsOpen) return;
      if (DispatchToOwner(Close)) return;
      generation++;
      navigationRequest++;
      IsOpen = false;
      IsExecuting = false;
      var oldCancellation = cancellation;
      cancellation = null;
      var oldLease = inputLease;
      inputLease = null;
      if (Context != null) Context.Invalidated -= Close;
      cancellationRegistration.Dispose();
      cancellationRegistration = default;
      foreach (var action in subscribed) action.Changed -= OnActionChanged;
      subscribed.Clear();
      expanded.Clear();
      expansionRequests.Clear();
      frames.Clear(); path.Clear(); allRows.Clear(); rows.Clear();
      Context = null; Selected = null;
      // Restore ownership before canceled callbacks may open a replacement and acquire a new lease.
      try { oldLease?.Dispose(); }
      finally
      {
        try { oldCancellation?.Cancel(); }
        finally
        {
          oldCancellation?.Dispose();
          try { Changed?.Invoke(); }
          finally { Closed?.Invoke(); }
        }
      }
    }

    public bool Select(MenuActionId id)
    {
      if (!EnsureValid()) return false;
      int index = IndexOf(id);
      if (index < 0 || !allRows[index].Action.Behavior.IsInteractive) return false;
      var action = allRows[index].Action;
      if (!action.Evaluate(Context).Visible) return false;
      bool changed = Selected != action || Current.Page != index / pageSize;
      if (changed) { navigationRequest++; navigationIntent++; }
      Current.Selection = action;
      Current.SelectionCleared = false;
      Current.Page = index / pageSize;
      Selected = action;
      RebuildPage();
      if (changed) Changed?.Invoke();
      return true;
    }

    /// <summary>Clear the active action without closing or changing the navigation path.</summary>
    public bool ClearSelection()
    {
      if (!EnsureValid() || Current.SelectionCleared && Selected == null) return false;
      navigationRequest++;
      navigationIntent++;
      Current.SelectionCleared = true;
      Current.Selection = null;
      Selected = null;
      Changed?.Invoke();
      return true;
    }

    public UniTask ActivateAsync(CancellationToken cancellationToken = default)
      => InvokeAsync(Selected, Selected?.Behavior, cancellationToken);

    public UniTask InvokePrimaryAsync(CancellationToken cancellationToken = default)
      => InvokeAsync(Selected, Selected?.PrimaryBehavior ?? (Selected != null && !Selected.HasChildren ? Selected.Behavior : null), cancellationToken);

    public async UniTask EnterSubmenuAsync(MenuActionId id, CancellationToken cancellationToken = default)
    {
      if (!EnsureValid()) return;
      int index = IndexOf(id);
      if (index < 0) return;
      var action = allRows[index].Action;
      if (!action.HasChildren || !CanInvoke(action)) return;
      long opened = generation, request = ++navigationRequest;
      navigationIntent++;
      using var linked = Link(cancellationToken);
      var token = linked?.Token ?? cancellation.Token;
      try
      {
        var children = await ResolveChildrenAsync(action, Context, token);
        if (!IsCurrent(opened) || navigationRequest != request || !CanInvoke(action) || IndexOfReference(action) < 0) return;
        var ancestors = AncestorsOf(action);
        for (int i = 0; i < ancestors.Count; i++)
        {
          var ancestor = ancestors[i];
          Current.Selection = ancestor;
          Current.SelectionCleared = false;
          frames.Add(new Frame(expanded[ancestor], ancestor));
          path.Add(ancestor.Id);
        }
        Current.Selection = action;
        Current.SelectionCleared = false;
        frames.Add(new Frame(children, action));
        path.Add(action.Id);
        Rebuild();
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public bool GoBack()
    {
      if (!EnsureValid()) return false;
      navigationRequest++;
      navigationIntent++;
      if (Selected != null)
      {
        if (expanded.ContainsKey(Selected) && Collapse(Selected.Id)) return true;
        var ancestors = AncestorsOf(Selected);
        if (ancestors.Count > 0 && Collapse(ancestors[ancestors.Count - 1].Id)) return true;
      }
      if (frames.Count > 1)
      {
        var returningTo = Current.Parent;
        frames.RemoveAt(frames.Count - 1);
        path.RemoveAt(path.Count - 1);
        Current.Selection = returningTo;
        Current.SelectionCleared = false;
        Rebuild();
      }
      else Close();
      return true;
    }

    public async UniTask ExpandAsync(MenuActionId id, CancellationToken cancellationToken = default)
    {
      if (!EnsureValid()) return;
      int index = IndexOf(id);
      if (index < 0) return;
      var action = allRows[index].Action;
      if (!action.HasChildren || !CanInvoke(action)) return;
      long opened = generation, request = ++nextExpansionRequest;
      navigationRequest++;
      navigationIntent++;
      expansionRequests[action] = request;
      using var linked = Link(cancellationToken);
      var token = linked?.Token ?? cancellation.Token;
      try
      {
        var children = await ResolveChildrenAsync(action, Context, token);
        if (!IsCurrent(opened) || !expansionRequests.TryGetValue(action, out var latest) || latest != request
          || IndexOfReference(action) < 0 || !CanInvoke(action)) return;
        expanded[action] = children;
        Rebuild();
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public bool Collapse(MenuActionId id)
    {
      if (!EnsureValid()) return false;
      int index = IndexOf(id);
      if (index < 0) return false;
      var action = allRows[index].Action;
      navigationRequest++;
      navigationIntent++;
      expansionRequests.Remove(action);
      if (!expanded.ContainsKey(action)) return false;
      int selected = Selected == null ? -1 : IndexOfReference(Selected);
      if (selected >= index)
      {
        int end = index + 1;
        while (end < allRows.Count && allRows[end].Depth > allRows[index].Depth) end++;
        if (selected < end) { Current.Selection = action; Current.SelectionCleared = false; }
      }
      DropExpansion(action);
      Rebuild();
      return true;
    }

    public bool PageForward() => SetPage(PageIndex + 1);
    public bool PageBackward() => SetPage(PageIndex - 1);

    public UniTask InvokeSecondaryAsync(int index, CancellationToken cancellationToken = default)
    {
      if (!EnsureValid() || Selected == null || !IsVisible(Selected) || index < 0 || index >= Selected.SecondaryActions.Count)
        return UniTask.CompletedTask;
      var action = Selected.SecondaryActions[index];
      return InvokeAsync(action, action?.Behavior, cancellationToken, Selected);
    }

    public async UniTask RefreshAsync(CancellationToken cancellationToken = default)
    {
      if (!EnsureValid()) return;
      long opened = generation;
      // Availability is synchronous: discard inaccessible ancestors before awaiting providers.
      Rebuild();
      if (!IsCurrent(opened)) return;
      long request = ++navigationRequest;
      var refreshed = new List<Frame> { CopyFrame(frames[0], frames[0].Source) };
      var oldFrames = new List<Frame>(frames);
      var oldExpanded = new Dictionary<MenuAction, IReadOnlyList<MenuAction>>(expanded);
      using var linked = Link(cancellationToken);
      var token = linked?.Token ?? cancellation.Token;
      try
      {
        for (int i = 1; i < oldFrames.Count; i++)
        {
          var parent = FindById(refreshed[i - 1].Source, oldFrames[i].Parent.Id);
          if (parent == null || !(oldFrames[i].SecondaryCollection ? IsVisible(parent) : CanInvoke(parent))
            || (!oldFrames[i].SecondaryCollection && !parent.HasChildren)) break;
          var children = oldFrames[i].SecondaryCollection ? SnapshotAndValidate(parent.SecondaryActions)
            : await ResolveChildrenAsync(parent, Context, token);
          if (!IsCurrent(opened) || navigationRequest != request) return;
          refreshed.Add(CopyFrame(oldFrames[i], children, parent));
        }
        var refreshedExpanded = new Dictionary<MenuAction, IReadOnlyList<MenuAction>>();
        var visited = new HashSet<MenuAction>();
        for (int i = 0; i < refreshed.Count && i < oldFrames.Count; i++)
          await RefreshExpandedAsync(refreshed[i].Source, oldFrames[i].Source, oldExpanded,
            refreshedExpanded, visited, opened, request, token);
        if (!IsCurrent(opened) || navigationRequest != request) return;
        frames.Clear(); frames.AddRange(refreshed);
        path.Clear();
        for (int i = 1; i < frames.Count; i++) path.Add(frames[i].Parent.Id);
        expanded.Clear();
        foreach (var pair in refreshedExpanded) expanded.Add(pair.Key, pair.Value);
        Rebuild();
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async UniTask InvokeAsync(MenuAction action, IMenuActionBehavior behavior, CancellationToken external, MenuAction secondaryOwner = null)
    {
      if (!EnsureValid() || IsExecuting || action == null || behavior == null || !behavior.IsInteractive || !CanInvoke(action)) return;
      long opened = generation;
      var context = Context;
      using var linked = Link(external);
      var token = linked?.Token ?? cancellation.Token;
      IsExecuting = true;
      try
      {
        token.ThrowIfCancellationRequested();
        if (!CanInvoke(action)) return;
        MenuActionOutcome outcome;
        bool returnToUnity = unityOwner;
        try { outcome = await behavior.ActivateAsync(action, context, token).AttachExternalCancellation(token); }
        finally { await ReturnToOwnerAsync(returnToUnity); }
        if (!IsCurrent(opened) || token.IsCancellationRequested) return;
        switch (outcome)
        {
          case MenuActionOutcome.Close: Close(); break;
          case MenuActionOutcome.Refresh: await RefreshAsync(token); break;
          case MenuActionOutcome.GoBack: GoBack(); break;
          case MenuActionOutcome.Enter:
            if (secondaryOwner != null)
            {
              if (Selected != secondaryOwner || !IsVisible(secondaryOwner) || !action.HasChildren) break;
              Current.Selection = secondaryOwner;
              Current.SelectionCleared = false;
              navigationIntent++;
              frames.Add(new Frame(SnapshotAndValidate(secondaryOwner.SecondaryActions), secondaryOwner, true));
              path.Add(secondaryOwner.Id);
              Rebuild();
              if (!IsCurrent(opened)) break;
            }
            await EnterSubmenuAsync(action.Id, token);
            break;
          default: Rebuild(); break;
        }
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
      finally { if (IsOpen && generation == opened) IsExecuting = false; }
    }

    private bool SetPage(int page)
    {
      if (!EnsureValid() || page < 0 || page >= PageCount || page == Current.Page) return false;
      navigationRequest++;
      navigationIntent++;
      Current.Page = page;
      Current.Selection = null;
      Current.SelectionCleared = false;
      RebuildPage();
      Changed?.Invoke();
      return true;
    }

    private bool EnsureValid()
    {
      if (!IsOpen) return false;
      if (Context != null && Context.IsValid) return true;
      Close();
      return false;
    }

    private bool IsCurrent(long opened) => IsOpen && generation == opened && EnsureValid();
    private bool CanInvoke(MenuAction action)
    {
      if (action == null || Context == null || !Context.IsValid) return false;
      var state = action.Evaluate(Context);
      return state.Visible && state.Enabled;
    }

    // Highlighting and auxiliary controls do not depend on a primary command's enabled state.
    private bool IsVisible(MenuAction action)
      => action != null && Context != null && Context.IsValid && action.Evaluate(Context).Visible;

    private CancellationTokenSource Link(CancellationToken external)
      => external.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, external) : null;

    private async UniTask<IReadOnlyList<MenuAction>> ResolveChildrenAsync(MenuAction action, MenuContext context, CancellationToken token)
    {
      token.ThrowIfCancellationRequested();
      IReadOnlyList<MenuAction> children;
      bool returnToUnity = unityOwner;
      try
      {
        children = action.ChildrenProvider == null ? action.Children
          : await action.ChildrenProvider.ResolveAsync(context, token).AttachExternalCancellation(token);
      }
      finally { await ReturnToOwnerAsync(returnToUnity); }
      token.ThrowIfCancellationRequested();
      return SnapshotAndValidate(children);
    }

    private static IReadOnlyList<MenuAction> SnapshotAndValidate(IReadOnlyList<MenuAction> source)
    {
      if (source == null || source.Count == 0) return Array.Empty<MenuAction>();
      var ids = new HashSet<MenuActionId>();
      var snapshot = new MenuAction[source.Count];
      for (int i = 0; i < snapshot.Length; i++)
      {
        var action = source[i];
        if (action == null) throw new ArgumentException("A menu hierarchy contains a null action at index " + i + ".", nameof(source));
        if (!action.Id.IsValid || !ids.Add(action.Id)) throw new ArgumentException("Duplicate or invalid menu action identity '" + action.Id + "'.", nameof(source));
        snapshot[i] = action;
      }
      return Array.AsReadOnly(snapshot);
    }

    private static Frame CopyFrame(Frame old, IReadOnlyList<MenuAction> source, MenuAction parent = null)
    {
      var frame = new Frame(source, parent ?? old.Parent, old.SecondaryCollection)
        { Page = old.Page, SelectionCleared = old.SelectionCleared };
      if (old.Selection != null) frame.Selection = FindById(source, old.Selection.Id) ?? old.Selection;
      return frame;
    }

    private async UniTask RefreshExpandedAsync(IReadOnlyList<MenuAction> source, IReadOnlyList<MenuAction> oldSource,
      Dictionary<MenuAction, IReadOnlyList<MenuAction>> oldExpanded,
      Dictionary<MenuAction, IReadOnlyList<MenuAction>> refreshedExpanded, HashSet<MenuAction> visited,
      long opened, long request, CancellationToken token)
    {
      for (int i = 0; i < source.Count; i++)
      {
        if (!IsCurrent(opened) || navigationRequest != request) return;
        var action = source[i];
        if (!visited.Add(action)) continue;
        var oldAction = FindById(oldSource, action.Id);
        if (oldAction == null) continue;
        if (oldExpanded.TryGetValue(oldAction, out var oldChildren))
        {
          if (!CanInvoke(action) || !action.HasChildren) continue;
          var children = await ResolveChildrenAsync(action, Context, token);
          if (!IsCurrent(opened) || navigationRequest != request) return;
          refreshedExpanded[action] = children;
          await RefreshExpandedAsync(children, oldChildren, oldExpanded, refreshedExpanded,
            visited, opened, request, token);
        }
        else if (action.Children.Count > 0)
          await RefreshExpandedAsync(action.Children, oldAction.Children, oldExpanded, refreshedExpanded,
            visited, opened, request, token);
      }
    }

    private static MenuAction FindById(IReadOnlyList<MenuAction> source, MenuActionId id)
    {
      for (int i = 0; i < source.Count; i++) if (source[i].Id == id) return source[i];
      return null;
    }

    private int IndexOf(MenuActionId id)
    {
      for (int i = 0; i < allRows.Count; i++) if (allRows[i].Action.Id == id) return i;
      return -1;
    }
    private int IndexOfReference(MenuAction action)
    {
      for (int i = 0; i < allRows.Count; i++) if (ReferenceEquals(allRows[i].Action, action)) return i;
      return -1;
    }

    private List<MenuAction> AncestorsOf(MenuAction action)
    {
      var ancestors = new List<MenuAction>();
      for (int i = 0; i < allRows.Count; i++)
      {
        var row = allRows[i];
        while (ancestors.Count > row.Depth) ancestors.RemoveAt(ancestors.Count - 1);
        if (ReferenceEquals(row.Action, action)) return ancestors;
        if (row.Expanded) ancestors.Add(row.Action);
      }
      ancestors.Clear();
      return ancestors;
    }

    private void DropExpansion(MenuAction action)
    {
      if (!expanded.TryGetValue(action, out var children)) return;
      expanded.Remove(action);
      expansionRequests.Remove(action);
      for (int i = 0; i < children.Count; i++) DropExpansion(children[i]);
    }

    private void Rebuild(bool notify = true)
    {
      if (!EnsureValid() || rebuilding) return;
      rebuilding = true;
      try
      {
        for (int i = 0; i < frames.Count; i++)
        {
          if (i > 0 && (!(frames[i].SecondaryCollection ? IsVisible(frames[i].Parent) : CanInvoke(frames[i].Parent))
            || FindById(frames[i - 1].Source, frames[i].Parent.Id) == null))
          {
            frames.RemoveRange(i, frames.Count - i);
            path.RemoveRange(i - 1, path.Count - (i - 1));
            break;
          }
          var frame = frames[i];
          frame.Visible.Clear();
          for (int j = 0; j < frame.Source.Count; j++)
            if (frame.Source[j].Evaluate(Context).Visible) frame.Visible.Add(frame.Source[j]);
        }
        allRows.Clear();
        AddRows(Current.Visible, 0, new HashSet<MenuAction>());
        Current.Page = Math.Min(Current.Page, PageCount - 1);
        if (Current.Selection != null)
        {
          int selected = IndexOfReference(Current.Selection);
          if (selected < 0) selected = IndexOf(Current.Selection.Id);
          if (selected >= 0) { Current.Selection = allRows[selected].Action; Current.Page = selected / pageSize; }
          else Current.Selection = null;
        }
        RebuildPage();
        ReconcileSubscriptions();
      }
      finally { rebuilding = false; }
      if (notify && IsOpen) Changed?.Invoke();
    }

    private void AddRows(IReadOnlyList<MenuAction> source, int depth, HashSet<MenuAction> ancestry)
    {
      for (int i = 0; i < source.Count; i++)
      {
        var action = source[i];
        if (!action.Evaluate(Context).Visible) continue;
        bool isExpanded = expanded.TryGetValue(action, out var children) && CanInvoke(action);
        allRows.Add(new MenuRow(action, depth, isExpanded));
        if (isExpanded && ancestry.Add(action))
        {
          AddRows(children, depth + 1, ancestry);
          ancestry.Remove(action);
        }
      }
    }

    private void RebuildPage()
    {
      rows.Clear();
      if (!IsOpen || frames.Count == 0) { Selected = null; return; }
      int start = Current.Page * pageSize;
      int count = Math.Min(pageSize, allRows.Count - start);
      for (int i = 0; i < count; i++) rows.Add(allRows[start + i]);
      Selected = null;
      if (Current.SelectionCleared) return;
      for (int i = 0; i < rows.Count; i++)
        if (rows[i].Action == Current.Selection && rows[i].Action.Behavior.IsInteractive) Selected = rows[i].Action;
      if (Selected == null)
      {
        for (int i = 0; i < rows.Count; i++)
          if (rows[i].Action.Behavior.IsInteractive && CanInvoke(rows[i].Action)) { Selected = rows[i].Action; break; }
        if (Selected == null)
          for (int i = 0; i < rows.Count; i++)
            if (rows[i].Action.Behavior.IsInteractive) { Selected = rows[i].Action; break; }
      }
      Current.Selection = Selected;
    }

    private void ReconcileSubscriptions()
    {
      var reachable = new HashSet<MenuAction>();
      for (int i = 0; i < frames.Count; i++) Collect(frames[i].Source, reachable);
      var stale = new List<MenuAction>();
      foreach (var pair in expanded) if (!reachable.Contains(pair.Key)) stale.Add(pair.Key);
      for (int i = 0; i < stale.Count; i++) { expanded.Remove(stale[i]); expansionRequests.Remove(stale[i]); }
      var removed = new List<MenuAction>();
      foreach (var action in subscribed)
        if (!reachable.Contains(action)) { action.Changed -= OnActionChanged; removed.Add(action); }
      for (int i = 0; i < removed.Count; i++) subscribed.Remove(removed[i]);
      foreach (var action in reachable) if (subscribed.Add(action)) action.Changed += OnActionChanged;
    }

    private void Collect(IReadOnlyList<MenuAction> source, HashSet<MenuAction> reachable)
    {
      for (int i = 0; i < source.Count; i++)
      {
        var action = source[i];
        if (action == null || !reachable.Add(action)) continue;
        Collect(action.Children, reachable);
        Collect(action.SecondaryActions, reachable);
        if (expanded.TryGetValue(action, out var children)) Collect(children, reachable);
      }
    }

    private void OnActionChanged()
    {
      if (DispatchToOwner(OnActionChanged)) return;
      if (IsOpen) RefreshAsync().Forget(error => Serilog.Log.ForContext<MenuSession>()
        .ForContext("Feature", "ContextMenus")
        .Error(error, "Failed to refresh action menu {Generation}", generation));
    }

    private bool DispatchToOwner(Action callback)
    {
      if (!unityOwner || PlayerLoopHelper.IsMainThread) return false;
      long opened = generation;
      UniTask.Post(() => { if (IsOpen && generation == opened) callback(); });
      return true;
    }
    private static async UniTask ReturnToOwnerAsync(bool returnToUnity)
    {
      // Cancellation must not prevent cleanup from reaching the captured owner.
      // Managed-only consumers never capture Unity ownership and remain synchronous.
      if (returnToUnity && !PlayerLoopHelper.IsMainThread) await UniTask.SwitchToMainThread();
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      try { Close(); }
      finally { Changed = null; Closed = null; }
    }
  }
}
