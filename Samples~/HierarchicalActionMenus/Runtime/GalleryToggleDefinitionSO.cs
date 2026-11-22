using MToolKit.ContextMenus.Authoring;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  [CreateAssetMenu(menuName = "MToolKit/Samples/Action Menus/Toggle")]
  public sealed class GalleryToggleDefinitionSO : MenuActionDefinitionSO
  {
    protected override MenuAction CreateAction(MenuContext context)
      => new MenuAction(Id, LabelDescriptor,
        new MenuToggleBehavior(current => (current.Target as MenuGalleryState)?.Toggle(Id, Label)),
        state: current => GalleryDefinitionUtility.Availability(current,
          isChecked: (current.Target as MenuGalleryState)?.IsChecked(Id) == true),
        description: DescriptionDescriptor, icon: Icon, bindingHint: BindingHint);
  }
}
