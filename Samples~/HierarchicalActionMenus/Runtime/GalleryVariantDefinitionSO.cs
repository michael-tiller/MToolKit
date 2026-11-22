using MToolKit.ContextMenus.Authoring;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  [CreateAssetMenu(menuName = "MToolKit/Samples/Action Menus/Secondary Variant")]
  public sealed class GalleryVariantDefinitionSO : MenuActionDefinitionSO
  {
    protected override MenuAction CreateAction(MenuContext context)
      => new MenuAction(Id, LabelDescriptor,
        new MenuCommandBehavior(current => (current.Target as MenuGalleryState)?.CycleVariant(), MenuActionOutcome.Refresh),
        state: current =>
        {
          var model = current.Target as MenuGalleryState;
          return new MenuActionState(model?.IsValid == true, model?.VariantsEnabled == true,
            disabledReason: model?.VariantsEnabled == true ? null : "Secondary actions disabled by gallery control");
        }, description: DescriptionDescriptor, icon: Icon, bindingHint: BindingHint);
  }
}
