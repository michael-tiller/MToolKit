using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.ContextMenus.Authoring
{
  /// <summary>Reusable authored definition; mutable state belongs to the menu context.</summary>
  public abstract class MenuActionDefinitionSO : ScriptableObject
  {
    [ThreadStatic] private static HashSet<MenuActionDefinitionSO> creating;
    public string Id;
    public string Label;
    public string LabelKey;
    [TextArea] public string Description;
    public string DescriptionKey;
    public string TableName = "Default String Table";
    public Sprite Icon;
    public InputActionReference BindingHint;
    public MenuActionDefinitionSO[] SecondaryActions = Array.Empty<MenuActionDefinitionSO>();
    public MenuLabel LabelDescriptor => new MenuLabel(Label, LabelKey, TableName);
    public MenuLabel DescriptionDescriptor => new MenuLabel(Description, DescriptionKey, TableName);

    /// <summary>Create a fresh hierarchy. Shared child assets receive stable parent-qualified placement IDs.</summary>
    public MenuAction Create(MenuContext context)
    {
      if (context == null) throw new ArgumentNullException(nameof(context));
      if (string.IsNullOrWhiteSpace(Id))
        throw new InvalidOperationException("Menu action definition '" + name + "' requires a stable Id.");
      creating ??= new HashSet<MenuActionDefinitionSO>();
      if (!creating.Add(this))
        throw new InvalidOperationException("Authored menu action cycle detected at '" + Id + "'.");
      try
      {
        var source = CreateAction(context);
        if (source == null) throw new InvalidOperationException("Menu action definition '" + Id + "' returned no descriptor.");
        var secondary = new List<MenuAction>(source.SecondaryActions);
        var authored = MenuDefinitionPlacement.CreateDefinitions(SecondaryActions, context, Id + " secondary actions");
        for (int i = 0; i < authored.Count; i++) secondary.Add(authored[i]);
        return new MenuAction(new MenuActionId(Id), LabelDescriptor, source.Behavior,
          MenuDefinitionPlacement.Prefix(source.Children, Id),
          MenuDefinitionPlacement.Prefix(source.ChildrenProvider, Id),
          source.PrimaryBehavior, MenuDefinitionPlacement.Prefix(secondary, Id + "/@secondary"),
          source.Evaluate, DescriptionDescriptor, Icon != null ? Icon : source.Icon,
          BindingHint != null ? BindingHint : source.BindingHint, source.Metadata, source.PresentationOverride)
          .InheritChanges(source);
      }
      finally { creating.Remove(this); }
    }

    /// <summary>Supply behavior and context state; common authored text, hints and secondary definitions are applied by Create.</summary>
    protected abstract MenuAction CreateAction(MenuContext context);
  }
}
