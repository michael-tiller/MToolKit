using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuAuthoringTests
  {
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
    private T Asset<T>(string id = null) where T : ScriptableObject
    {
      var asset = ScriptableObject.CreateInstance<T>();
      created.Add(asset);
      if (asset is MenuActionDefinitionSO definition) { definition.Id = id; definition.Label = id; }
      return asset;
    }
    [TearDown] public void Cleanup()
    {
      foreach (var item in created) if (item != null) UnityEngine.Object.DestroyImmediate(item);
      created.Clear();
    }

    [Test] public void Action_set_materializes_authored_runtime_and_localized_descriptors()
    {
      var command = Asset<CommandDefinition>("inspect");
      command.Label = "Inspect"; command.LabelKey = "inspect_key";
      command.Description = "Details"; command.DescriptionKey = "details_key"; command.TableName = "Menus";
      var set = Asset<MenuActionSetSO>(); set.Actions = new MenuActionDefinitionSO[] { command };
      var actions = set.Create(new MenuContext());
      Assert.That(actions.Count, Is.EqualTo(1));
      Assert.That(actions[0].Id.Value, Is.EqualTo("inspect"));
      Assert.That(actions[0].Label.Key, Is.EqualTo("inspect_key"));
      Assert.That(actions[0].Description.Table, Is.EqualTo("Menus"));
    }

    [Test] public void Reused_child_definitions_have_distinct_stable_placement_ids()
    {
      var child = Asset<CommandDefinition>("inspect");
      var left = Asset<MenuSubmenuDefinitionSO>("left"); left.Children = new[] { child };
      var right = Asset<MenuSubmenuDefinitionSO>("right"); right.Children = new[] { child };
      var set = Asset<MenuActionSetSO>(); set.Actions = new MenuActionDefinitionSO[] { left, right };
      var actions = set.Create(new MenuContext());
      Assert.That(actions[0].Children[0].Id.Value, Is.EqualTo("left/inspect"));
      Assert.That(actions[1].Children[0].Id.Value, Is.EqualTo("right/inspect"));
      Assert.That(set.Create(new MenuContext())[0].Children[0].Id, Is.EqualTo(actions[0].Children[0].Id));
    }

    [Test] public void Nested_paths_and_dynamic_children_keep_their_placement_scope()
    {
      var provider = Asset<DynamicDefinition>();
      var middle = Asset<MenuSubmenuDefinitionSO>("middle"); middle.Provider = provider;
      var root = Asset<MenuSubmenuDefinitionSO>("root"); root.Children = new[] { middle };
      var action = root.Create(new MenuContext());
      Assert.That(action.Children[0].Id.Value, Is.EqualTo("root/middle"));
      var children = action.Children[0].ChildrenProvider.ResolveAsync(new MenuContext(), default).GetAwaiter().GetResult();
      Assert.That(children[0].Id.Value, Is.EqualTo("root/middle/generated"));
      Assert.That(provider.Calls, Is.EqualTo(1));
    }

    [Test] public void Invalid_context_does_not_invoke_authored_dynamic_provider()
    {
      var provider = Asset<DynamicDefinition>();
      var submenu = Asset<MenuSubmenuDefinitionSO>("root"); submenu.Provider = provider;
      var context = new MenuContext();
      var action = submenu.Create(context); context.Invalidate();
      var children = action.ChildrenProvider.ResolveAsync(context, default).GetAwaiter().GetResult();
      Assert.That(children, Is.Empty);
      Assert.That(provider.Calls, Is.Zero);
    }

    [Test] public void Secondary_and_parent_primary_are_explicit_and_navigation_never_executes_them()
    {
      var state = new TargetState();
      var primary = Asset<CommandDefinition>("primary");
      var secondary = Asset<CommandDefinition>("auxiliary");
      var child = Asset<CommandDefinition>("child");
      var submenu = Asset<MenuSubmenuDefinitionSO>("root");
      submenu.Primary = primary; submenu.Children = new[] { child }; submenu.SecondaryActions = new[] { secondary };
      var context = new MenuContext(state);
      using var session = new MenuSession();
      session.Open(new[] { submenu.Create(context) }, context);
      session.Select("root");
      session.ActivateAsync().GetAwaiter().GetResult();
      Assert.That(state.Calls, Is.Zero);
      Assert.That(session.Actions[0].Id.Value, Is.EqualTo("root/child"));
      session.GoBack(); session.Select("root");
      session.InvokeSecondaryAsync(0).GetAwaiter().GetResult();
      Assert.That(state.Calls, Is.EqualTo(1));
      Assert.That(session.IsOpen, Is.True);
      session.InvokePrimaryAsync().GetAwaiter().GetResult();
      Assert.That(state.Calls, Is.EqualTo(2));
    }

    [Test] public void Authored_descriptors_keep_context_state_out_of_shared_assets()
    {
      var definition = Asset<CommandDefinition>("toggle");
      var one = new TargetState(); var two = new TargetState();
      var first = definition.Create(new MenuContext(one));
      var second = definition.Create(new MenuContext(two));
      first.Behavior.ActivateAsync(first, new MenuContext(one), default).GetAwaiter().GetResult();
      Assert.That(first.Evaluate(new MenuContext(one)).IsChecked, Is.True);
      Assert.That(second.Evaluate(new MenuContext(two)).IsChecked, Is.False);
      Assert.That(two.Calls, Is.Zero);
    }

    [Test] public void Scoped_placements_forward_changes_without_retaining_subscriptions_after_close()
    {
      var definition = Asset<CommandDefinition>("child");
      var submenu = Asset<MenuSubmenuDefinitionSO>("root"); submenu.Children = new[] { definition };
      var context = new MenuContext(new TargetState());
      var action = submenu.Create(context);
      using var session = new MenuSession();
      session.Open(new[] { action }, context);
      session.ExpandAsync("root").GetAwaiter().GetResult();
      int events = 0; session.Changed += () => events++;
      int descriptorSignals = 0;
      Action observed = () => descriptorSignals++;
      action.Children[0].Changed += observed;
      definition.LastCreated.NotifyChanged();
      Assert.That(descriptorSignals, Is.EqualTo(1));
      // Refresh publishes immediate availability before awaiting providers, then the resolved tree.
      Assert.That(events, Is.GreaterThanOrEqualTo(1));
      action.Children[0].Changed -= observed;
      session.Close(); events = 0;
      definition.LastCreated.NotifyChanged();
      Assert.That(events, Is.Zero);
    }

    [Test] public void Duplicate_sibling_ids_fail_with_useful_diagnostics()
    {
      var one = Asset<CommandDefinition>("duplicate");
      var two = Asset<CommandDefinition>("duplicate");
      var set = Asset<MenuActionSetSO>(); set.Actions = new MenuActionDefinitionSO[] { one, two };
      var error = Assert.Throws<InvalidOperationException>(() => set.Create(new MenuContext()));
      Assert.That(error.Message, Does.Contain("duplicate"));
    }

    [Test] public void Missing_action_id_and_null_definition_fail_with_useful_diagnostics()
    {
      var missing = Asset<CommandDefinition>();
      Assert.Throws<InvalidOperationException>(() => missing.Create(new MenuContext()));
      var set = Asset<MenuActionSetSO>(); set.Actions = new MenuActionDefinitionSO[] { null };
      var error = Assert.Throws<InvalidOperationException>(() => set.Create(new MenuContext()));
      Assert.That(error.Message, Does.Contain("0"));
    }

    [Test] public void Authored_cycles_are_rejected_before_recursing_or_executing_commands()
    {
      var first = Asset<MenuSubmenuDefinitionSO>("first");
      var second = Asset<MenuSubmenuDefinitionSO>("second");
      first.Children = new[] { second }; second.Children = new[] { first };
      var error = Assert.Throws<InvalidOperationException>(() => first.Create(new MenuContext()));
      Assert.That(error.Message, Does.Contain("cycle"));
    }

    [Test] public void Dynamic_results_are_discarded_when_context_becomes_invalid_during_resolution()
    {
      var provider = Asset<PendingDefinition>();
      var submenu = Asset<MenuSubmenuDefinitionSO>("root"); submenu.Provider = provider;
      var context = new MenuContext();
      var pending = submenu.Create(context).ChildrenProvider.ResolveAsync(context, default);
      context.Invalidate();
      provider.Result.TrySetResult(new[] { new MenuAction("late", new MenuLabel("Late")) });
      Assert.That(pending.GetAwaiter().GetResult(), Is.Empty);
    }

    [Test] public void Header_definition_stays_noninteractive_and_empty_secondary_fields_are_optional()
    {
      var header = Asset<MenuHeaderDefinitionSO>("section");
      header.SecondaryActions = null;
      var set = Asset<MenuActionSetSO>(); set.Actions = new[] { header };
      var actions = set.Create(new MenuContext());
      Assert.That(actions.Count, Is.EqualTo(1));
      Assert.That(actions[0].Behavior.IsInteractive, Is.False);
      Assert.That(actions[0].SecondaryActions, Is.Empty);
    }

    public sealed class TargetState { public int Calls; }
    public sealed class CommandDefinition : MenuActionDefinitionSO
    {
      public MenuAction LastCreated;
      protected override MenuAction CreateAction(MenuContext context)
      {
        LastCreated = new MenuAction(Id, LabelDescriptor,
          new MenuToggleBehavior(c => ((TargetState)c.Target).Calls++),
          state: c => new MenuActionState(isChecked: c.Target is TargetState state && state.Calls > 0),
          description: DescriptionDescriptor);
        return LastCreated;
      }
    }
    public sealed class DynamicDefinition : MenuChildrenProviderSO
    {
      public int Calls;
      public override UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken)
      {
        Calls++;
        return UniTask.FromResult<IReadOnlyList<MenuAction>>(new[] { new MenuAction("generated", new MenuLabel("Generated")) });
      }
    }
    public sealed class PendingDefinition : MenuChildrenProviderSO
    {
      public readonly UniTaskCompletionSource<IReadOnlyList<MenuAction>> Result = new UniTaskCompletionSource<IReadOnlyList<MenuAction>>();
      public override UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken cancellationToken) => Result.Task;
    }
  }
}
