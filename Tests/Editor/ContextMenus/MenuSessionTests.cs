using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuSessionTests
  {
    [Test]
    public void EnteringParentNeverInvokesItsPrimaryCommand()
    {
      int calls = 0;
      using var menu = new MenuSession();
      var child = Command("child", _ => calls++);
      var parent = new MenuAction("parent", new MenuLabel("Parent"),
        children: new[] { child }, primary: new MenuCommandBehavior(_ => calls++));
      menu.Open(new[] { parent }, new MenuContext());
      Run(menu.ActivateAsync());
      Assert.That(calls, Is.Zero);
      Assert.That(menu.Path, Is.EqualTo(new MenuActionId[] { "parent" }));
      Assert.That(menu.Selected, Is.SameAs(child));
      Assert.That(menu.ParentSelected, Is.SameAs(parent));
      Assert.That(menu.ParentActions, Is.EqualTo(new[] { parent }));
      Assert.That(menu.PathActions, Is.EqualTo(new[] { parent }), "Every path identity retains its actual parent descriptor.");
    }

    [Test]
    public void ExplicitPrimaryCommandDoesNotNavigateAndKeepsConfiguredOutcome()
    {
      int calls = 0;
      using var menu = new MenuSession();
      var parent = new MenuAction("parent", new MenuLabel("Parent"),
        children: new[] { Command("child", _ => { }) },
        primary: new MenuCommandBehavior(_ => calls++, MenuActionOutcome.Keep));
      menu.Open(new[] { parent }, new MenuContext());
      Run(menu.InvokePrimaryAsync());
      Assert.That(calls, Is.EqualTo(1));
      Assert.That(menu.Path, Is.Empty);
      Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void BackTraversesArbitraryDepthThenCancelsRoot()
    {
      using var menu = new MenuSession();
      MenuAction leaf = Command("leaf", _ => { });
      MenuAction parent = Parent("two", leaf);
      MenuAction root = Parent("one", parent);
      menu.Open(new[] { root }, new MenuContext());
      Run(menu.EnterSubmenuAsync(root.Id));
      Run(menu.EnterSubmenuAsync(parent.Id));
      Assert.That(menu.Path.Count, Is.EqualTo(2));
      Assert.That(menu.PathActions, Is.EqualTo(new[] { root, parent }));
      Assert.That(menu.GoBack(), Is.True);
      Assert.That(menu.Selected, Is.SameAs(parent));
      Assert.That(menu.GoBack(), Is.True);
      Assert.That(menu.Selected, Is.SameAs(root));
      Assert.That(menu.GoBack(), Is.True);
      Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void InlineExpansionProducesDepthRowsWithoutReplacingCurrentLevel()
    {
      using var menu = new MenuSession();
      var leaf = Command("leaf", _ => { });
      var nested = Parent("nested", leaf);
      var parent = Parent("parent", nested);
      menu.Open(new[] { parent }, new MenuContext());
      Run(menu.ExpandAsync(parent.Id));
      Run(menu.ExpandAsync(nested.Id));
      Assert.That(menu.Actions, Is.EqualTo(new[] { parent }));
      Assert.That(menu.Path, Is.Empty);
      Assert.That(menu.Rows.Count, Is.EqualTo(3));
      Assert.That(menu.Rows[0].Expanded, Is.True);
      Assert.That(menu.Rows[1].Depth, Is.EqualTo(1));
      Assert.That(menu.Rows[2].Depth, Is.EqualTo(2));
      Assert.That(menu.Select(leaf.Id), Is.True);
      Assert.That(menu.Collapse(parent.Id), Is.True);
      Assert.That(menu.Rows.Count, Is.EqualTo(1));
      Assert.That(menu.Selected, Is.SameAs(parent));
    }

    [Test]
    public void DynamicChildrenResolveOnEveryEntryAndRefresh()
    {
      int generation = 0;
      var provider = new DelegateMenuChildrenProvider((_, __) =>
        UniTask.FromResult<IReadOnlyList<MenuAction>>(new[] { Command("item" + ++generation, ___ => { }) }));
      var parent = new MenuAction("parent", new MenuLabel("Parent"), childrenProvider: provider);
      using var menu = new MenuSession();
      menu.Open(new[] { parent }, new MenuContext());
      Assert.That(generation, Is.Zero, "Children are lazy.");
      Run(menu.EnterSubmenuAsync(parent.Id));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("item1"));
      Run(menu.RefreshAsync());
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("item2"));
      menu.GoBack();
      Run(menu.EnterSubmenuAsync(parent.Id));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("item3"));

      // A provider may produce fresh descriptor instances with the same stable identities.
      int snapshot = 0;
      var refreshable = new MenuAction("refreshable", new MenuLabel("Refreshable"), childrenProvider:
        new DelegateMenuChildrenProvider((_, __) =>
        {
          snapshot++;
          var currentLeaf = new MenuAction("stable-leaf", new MenuLabel("Version " + snapshot),
            new MenuCommandBehavior(___ => { }, MenuActionOutcome.Keep));
          return UniTask.FromResult<IReadOnlyList<MenuAction>>(new[] { Parent("stable-parent", currentLeaf) });
        }));
      menu.Open(new[] { refreshable }, new MenuContext());
      Run(menu.EnterSubmenuAsync(refreshable.Id));
      Run(menu.ExpandAsync("stable-parent"));
      menu.Select("stable-leaf");
      Run(menu.RefreshAsync());
      Assert.That(menu.Rows.Count, Is.EqualTo(2), "Refresh preserves expanded branches by stable identity.");
      Assert.That(menu.Rows[0].Expanded, Is.True);
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("stable-leaf"));
      Assert.That(menu.Selected.Label.Fallback, Is.EqualTo("Version 2"));
    }

    [Test]
    public void LiveStateHidesRowsAndRetainsStructuredCheckedState()
    {
      bool visible = true, enabled = true, selected = false;
      var changing = new MenuAction("live", new MenuLabel("Live"),
        new MenuCommandBehavior(_ => { }, MenuActionOutcome.Keep),
        state: _ => new MenuActionState(visible, enabled, selected, enabled ? null : "unavailable"));
      using var menu = new MenuSession();
      menu.Open(new[] { changing, Command("other", _ => { }) }, new MenuContext());
      selected = true; enabled = false; changing.NotifyChanged();
      Assert.That(changing.Evaluate(menu.Context).IsChecked, Is.True);
      Assert.That(changing.Evaluate(menu.Context).DisabledReason, Is.EqualTo("unavailable"));
      visible = false; changing.NotifyChanged();
      Assert.That(menu.Rows.Count, Is.EqualTo(1));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("other"));
    }

    [Test]
    public void DisabledOrHiddenCommandsNeverExecute()
    {
      int calls = 0;
      var disabled = new MenuAction("disabled", new MenuLabel("Disabled"),
        new MenuCommandBehavior(_ => calls++), state: _ => new MenuActionState(enabled: false));
      var hidden = new MenuAction("hidden", new MenuLabel("Hidden"),
        new MenuCommandBehavior(_ => calls++), state: _ => new MenuActionState(visible: false));
      using var menu = new MenuSession();
      menu.Open(new[] { disabled, hidden }, new MenuContext());
      menu.Select(disabled.Id);
      Run(menu.ActivateAsync());
      Assert.That(menu.Select(hidden.Id), Is.False);
      Assert.That(calls, Is.Zero);
      Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void AvailabilityIsRecheckedImmediatelyBeforeCommandInvocation()
    {
      bool enabled = true;
      int calls = 0;
      var action = new MenuAction("live", new MenuLabel("Live"),
        new MenuCommandBehavior(_ => calls++), state: _ => new MenuActionState(enabled: enabled));
      using var menu = new MenuSession();
      menu.Open(new[] { action }, new MenuContext());
      enabled = false;
      Run(menu.ActivateAsync());
      Assert.That(calls, Is.Zero);
    }

    [Test]
    public void InvalidContextAndDestroyedUnityTargetCannotExecute()
    {
      int calls = 0;
      var target = new GameObject("menu target");
      using var menu = new MenuSession();
      menu.Open(new[] { Command("command", _ => calls++) }, new MenuContext(target));
      Object.DestroyImmediate(target);
      Run(menu.ActivateAsync());
      Assert.That(calls, Is.Zero);
      Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void ContextInvalidationClosesAndReleasesFocusExactlyOnce()
    {
      using var menu = new MenuSession();
      var context = new MenuContext();
      var lease = new CountingLease();
      int closed = 0;
      menu.Closed += () => closed++;
      menu.Open(new[] { Command("command", _ => { }) }, context, lease);
      context.Invalidate(); menu.Close(); menu.Dispose();
      Assert.That(closed, Is.EqualTo(1));
      Assert.That(lease.Disposals, Is.EqualTo(1));
      Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void ExternalCancellationClosesAnOpenSession()
    {
      using var cancellation = new CancellationTokenSource();
      using var menu = new MenuSession();
      menu.Open(new[] { Command("command", _ => { }) },
        new MenuContext(cancellationToken: cancellation.Token));
      cancellation.Cancel();
      Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void LateDynamicResultCannotReopenAClosedOrReplacedMenu()
    {
      var pending = new UniTaskCompletionSource<IReadOnlyList<MenuAction>>();
      var parent = new MenuAction("parent", new MenuLabel("Parent"), childrenProvider:
        new DelegateMenuChildrenProvider((_, __) => pending.Task));
      using var menu = new MenuSession();
      menu.Open(new[] { parent }, new MenuContext());
      var loading = menu.EnterSubmenuAsync(parent.Id);
      var replacement = Command("replacement", _ => { });
      menu.Open(new[] { replacement }, new MenuContext());
      pending.TrySetResult(new[] { Command("stale", _ => { }) });
      Run(loading);
      Assert.That(menu.Selected, Is.SameAs(replacement));
      Assert.That(menu.Path, Is.Empty);
      Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void NewerDynamicNavigationDiscardsEarlierCompletion()
    {
      var pending = new UniTaskCompletionSource<IReadOnlyList<MenuAction>>();
      var slow = new MenuAction("slow", new MenuLabel("Slow"), childrenProvider:
        new DelegateMenuChildrenProvider((_, __) => pending.Task));
      var fast = Parent("fast", Command("fresh", _ => { }));
      using var menu = new MenuSession();
      menu.Open(new[] { slow, fast }, new MenuContext());
      var loading = menu.EnterSubmenuAsync(slow.Id);
      Run(menu.EnterSubmenuAsync(fast.Id));
      pending.TrySetResult(new[] { Command("stale", _ => { }) });
      Run(loading);
      Assert.That(menu.Path, Is.EqualTo(new MenuActionId[] { "fast" }));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("fresh"));
    }

    [Test]
    public void ReentrantReplacementSurvivesOldCommandsCloseOutcome()
    {
      using var menu = new MenuSession();
      var replacement = Command("replacement", _ => { });
      var action = Command("original", _ => menu.Open(new[] { replacement }, new MenuContext()));
      menu.Open(new[] { action }, new MenuContext());
      Run(menu.ActivateAsync());
      Assert.That(menu.IsOpen, Is.True);
      Assert.That(menu.Selected, Is.SameAs(replacement));
    }

    [Test]
    public void PendingCommandCannotBeInvokedTwice()
    {
      int calls = 0;
      var pending = new UniTaskCompletionSource<MenuActionOutcome>();
      var action = new MenuAction("pending", new MenuLabel("Pending"), new MenuCommandBehavior((_, __) =>
      { calls++; return pending.Task; }));
      using var menu = new MenuSession();
      menu.Open(new[] { action }, new MenuContext());
      var first = menu.ActivateAsync();
      Run(menu.ActivateAsync());
      Assert.That(calls, Is.EqualTo(1));
      pending.TrySetResult(MenuActionOutcome.Keep);
      Run(first);
      Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void SecondaryActionsHaveIndependentAvailabilityAndKeepMenuOpen()
    {
      int primaryCalls = 0, secondaryCalls = 0;
      bool secondaryEnabled = false;
      var auxiliary = new MenuAction("ammo", new MenuLabel("Cycle ammunition"),
        new MenuCommandBehavior(_ => secondaryCalls++, MenuActionOutcome.Refresh),
        state: _ => new MenuActionState(enabled: secondaryEnabled));
      var primary = new MenuAction("weapon", new MenuLabel("Weapon"),
        new MenuCommandBehavior(_ => primaryCalls++), secondaryActions: new[]
        { auxiliary, Parent("variants", Command("variant-choice", _ => { })) });
      using var menu = new MenuSession();
      menu.Open(new[] { primary }, new MenuContext());
      Run(menu.InvokeSecondaryAsync(0));
      secondaryEnabled = true;
      Run(menu.InvokeSecondaryAsync(0));
      Assert.That(primaryCalls, Is.Zero);
      Assert.That(secondaryCalls, Is.EqualTo(1));
      Assert.That(menu.IsOpen, Is.True);
      Run(menu.InvokeSecondaryAsync(1));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("variant-choice"), "Secondary submenu navigation uses the shared session path.");
      Run(menu.RefreshAsync());
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("variant-choice"));
      menu.GoBack();
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("variants"));
      menu.GoBack();
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("weapon"));
    }

    [Test]
    public void EnabledSecondaryRemainsUsableWhileSelectedPrimaryIsDisabled()
    {
      int primaryCalls = 0, secondaryCalls = 0;
      var secondary = new MenuAction("inspect", new MenuLabel("Inspect"),
        new MenuCommandBehavior(_ => secondaryCalls++, MenuActionOutcome.Keep));
      var primary = new MenuAction("unavailable-primary", new MenuLabel("Unavailable primary"),
        new MenuCommandBehavior(_ => primaryCalls++),
        secondaryActions: new[] { secondary, Parent("secondary-submenu", Command("variant", _ => { })) },
        state: _ => new MenuActionState(enabled: false));
      using var menu = new MenuSession();
      menu.Open(new[] { primary }, new MenuContext());
      Assert.That(menu.Select(primary.Id), Is.True, "A disabled command can still expose independently enabled auxiliary controls.");
      Run(menu.ActivateAsync());
      Run(menu.InvokeSecondaryAsync(0));
      Assert.That(primaryCalls, Is.Zero);
      Assert.That(secondaryCalls, Is.EqualTo(1));
      Run(menu.InvokeSecondaryAsync(1));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("variant"));
      Run(menu.RefreshAsync());
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("variant"));
      menu.GoBack(); menu.GoBack();
      Assert.That(menu.Selected, Is.SameAs(primary));
    }

    [Test]
    public void PageOperationsAreBoundedAndMoveSelectionToVisiblePage()
    {
      using var menu = new MenuSession { PageSize = 2 };
      menu.Open(new[] { Command("one", _ => { }), Command("two", _ => { }),
        Command("three", _ => { }), Command("four", _ => { }), Command("five", _ => { }) }, new MenuContext());
      Assert.That(menu.Rows.Count, Is.EqualTo(2));
      Assert.That(menu.PageForward(), Is.True);
      Assert.That(menu.PageIndex, Is.EqualTo(1));
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("three"));
      Assert.That(menu.PageForward(), Is.True);
      Assert.That(menu.Rows.Count, Is.EqualTo(1));
      Assert.That(menu.PageForward(), Is.False);
      Assert.That(menu.PageBackward(), Is.True);
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("three"));
    }

    [Test]
    public void DuplicateSiblingIdentitiesFailWithUsefulDiagnostic()
    {
      using var menu = new MenuSession();
      var error = Assert.Throws<ArgumentException>(() => menu.Open(new[]
      { Command("same", _ => { }), Command("same", _ => { }) }, new MenuContext()));
      Assert.That(error.Message, Does.Contain("same"));
      Assert.That(menu.IsOpen, Is.False);
    }

    [Test]
    public void HeaderCannotBecomeAnActivatableSelection()
    {
      var header = new MenuAction("header", new MenuLabel("Header"), new MenuHeaderBehavior());
      var command = Command("command", _ => { });
      using var menu = new MenuSession();
      menu.Open(new[] { header, command }, new MenuContext());
      Assert.That(menu.Selected, Is.SameAs(command));
      Assert.That(menu.Select(header.Id), Is.False);
    }

    [Test]
    public void RefreshRemovingCurrentParentReturnsToValidAncestor()
    {
      bool visible = true;
      var parent = new MenuAction("parent", new MenuLabel("Parent"), children: new[] { Command("child", _ => { }) },
        state: _ => new MenuActionState(visible: visible));
      using var menu = new MenuSession();
      menu.Open(new[] { parent, Command("other", _ => { }) }, new MenuContext());
      Run(menu.EnterSubmenuAsync(parent.Id));
      visible = false;
      parent.NotifyChanged();
      Assert.That(menu.Path, Is.Empty);
      Assert.That(menu.Selected.Id.Value, Is.EqualTo("other"));
    }

    [Test]
    public void ClosingUnsubscribesActionChangesAndReplacingDisposesOldLease()
    {
      using var menu = new MenuSession();
      var first = Command("first", _ => { });
      var lease = new CountingLease();
      menu.Open(new[] { first }, new MenuContext(), lease);
      var replacement = Command("replacement", _ => { });
      menu.Open(new[] { replacement }, new MenuContext());
      int changes = 0;
      menu.Changed += () => changes++;
      first.NotifyChanged();
      Assert.That(changes, Is.Zero);
      Assert.That(lease.Disposals, Is.EqualTo(1));
      menu.Close();
      int closedChanges = changes;
      replacement.NotifyChanged();
      Assert.That(changes, Is.EqualTo(closedChanges));
    }

    [Test]
    public void ToggleAndChoiceRefreshStructuredStateWithoutClosing()
    {
      bool active = false;
      int choice = 0;
      var toggle = new MenuAction("toggle", new MenuLabel("Toggle"),
        new MenuToggleBehavior(_ => active = !active), state: _ => new MenuActionState(isChecked: active));
      var option = new MenuAction("option", new MenuLabel("Option"),
        new MenuChoiceBehavior(_ => choice = 2), state: _ => new MenuActionState(isChecked: choice == 2));
      using var menu = new MenuSession();
      menu.Open(new[] { toggle, option }, new MenuContext());
      Run(menu.ActivateAsync());
      menu.Select(option.Id);
      Run(menu.ActivateAsync());
      Assert.That(toggle.Evaluate(menu.Context).IsChecked, Is.True);
      Assert.That(option.Evaluate(menu.Context).IsChecked, Is.True);
      Assert.That(menu.IsOpen, Is.True);
    }

    [Test]
    public void CancellationRetiresPendingProviderWithoutReopening()
    {
      var pending = new UniTaskCompletionSource<IReadOnlyList<MenuAction>>();
      CancellationToken observedToken = default;
      var parent = new MenuAction("parent", new MenuLabel("Parent"), childrenProvider:
        new DelegateMenuChildrenProvider((_, token) => { observedToken = token; return pending.Task; }));
      using var menu = new MenuSession();
      menu.Open(new[] { parent }, new MenuContext());
      var loading = menu.EnterSubmenuAsync(parent.Id);
      menu.Close();
      Assert.That(observedToken.IsCancellationRequested, Is.True);
      pending.TrySetResult(new[] { Command("stale", _ => { }) });
      Run(loading);
      Assert.That(menu.IsOpen, Is.False);
      Assert.That(menu.Rows, Is.Empty);
    }

    [Test]
    public void ThrowingCommandDoesNotLeaveTheExecutionGateLocked()
    {
      int calls = 0;
      var broken = Command("broken", _ => throw new InvalidOperationException("expected command error"));
      var valid = new MenuAction("valid", new MenuLabel("Valid"),
        new MenuCommandBehavior(_ => calls++, MenuActionOutcome.Keep));
      using var menu = new MenuSession();
      menu.Open(new[] { broken, valid }, new MenuContext());
      Assert.Throws<InvalidOperationException>(() => Run(menu.ActivateAsync()));
      Assert.That(menu.IsExecuting, Is.False);
      Assert.That(menu.IsOpen, Is.True);
      menu.Select(valid.Id);
      Run(menu.ActivateAsync());
      Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void ClosedCallbackMayOpenReplacementWithoutOldCleanupTouchingIt()
    {
      using var menu = new MenuSession();
      var original = Command("old", _ => { });
      var replacement = Command("new", _ => { });
      menu.Open(new[] { original }, new MenuContext());
      bool once = false;
      menu.Closed += () =>
      {
        if (once) return;
        once = true;
        menu.Open(new[] { replacement }, new MenuContext());
      };
      menu.Close();
      Assert.That(menu.IsOpen, Is.True);
      Assert.That(menu.Selected, Is.SameAs(replacement));
      Assert.That(menu.Rows.Count, Is.EqualTo(1));
    }

    [Test]
    public void DynamicDuplicateIdsAreRejectedWithoutChangingTheCurrentPath()
    {
      var parent = new MenuAction("parent", new MenuLabel("Parent"), childrenProvider:
        new DelegateMenuChildrenProvider((_, __) => UniTask.FromResult<IReadOnlyList<MenuAction>>(new[]
        { Command("duplicate", ___ => { }), Command("duplicate", ___ => { }) })));
      using var menu = new MenuSession();
      menu.Open(new[] { parent }, new MenuContext());
      Assert.Throws<ArgumentException>(() => Run(menu.EnterSubmenuAsync(parent.Id)));
      Assert.That(menu.Path, Is.Empty);
      Assert.That(menu.Selected, Is.SameAs(parent));
    }

    private static MenuAction Command(string id, Action<MenuContext> execute)
      => new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(execute));
    private static MenuAction Parent(string id, params MenuAction[] children)
      => new MenuAction(id, new MenuLabel(id), children: children);
    private static void Run(UniTask task) => task.GetAwaiter().GetResult();
    private sealed class CountingLease : IDisposable
    {
      public int Disposals;
      public void Dispose() => Disposals++;
    }
  }
}
