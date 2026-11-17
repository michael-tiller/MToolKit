using MToolKit.ContextMenus.Authoring;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  [CreateAssetMenu(menuName = "MToolKit/Samples/Action Menus/Choice")]
  public sealed class GalleryChoiceDefinitionSO : MenuActionDefinitionSO
  {
    [field: SerializeField] public string Group { get; set; } = "sample.mode";
    [field: SerializeField] public string Value { get; set; } = "careful";
    protected override MenuAction CreateAction(MenuContext context)
      => new MenuAction(Id, LabelDescriptor,
        new MenuChoiceBehavior(current => (current.Target as MenuGalleryState)?.Choose(Group, Value, Id, Label)),
        state: current => GalleryDefinitionUtility.Availability(current,
          isChecked: (current.Target as MenuGalleryState)?.IsChoice(Group, Value) == true),
        description: DescriptionDescriptor, icon: Icon, bindingHint: BindingHint);
  }
}
