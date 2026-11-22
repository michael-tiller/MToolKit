using MToolKit.ContextMenus.Authoring;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  [CreateAssetMenu(menuName = "MToolKit/Samples/Action Menus/Command")]
  public sealed class GalleryCommandDefinitionSO : MenuActionDefinitionSO
  {
    [field: SerializeField] public MenuActionOutcome Outcome { get; set; } = MenuActionOutcome.Close;
    [field: SerializeField] public string CostText { get; set; }
    [field: SerializeField] public string UnavailableReason { get; set; }
    protected override MenuAction CreateAction(MenuContext context)
      => new MenuAction(Id, LabelDescriptor,
        new MenuCommandBehavior(current => (current.Target as MenuGalleryState)?.Record(Id, Label), Outcome),
        state: current => GalleryDefinitionUtility.Availability(current, UnavailableReason),
        description: DescriptionDescriptor, icon: Icon, bindingHint: BindingHint,
        metadata: context?.Target is MenuGalleryState model ? new MenuGalleryState.Details(model, CostText) : null);
  }

  internal static class GalleryDefinitionUtility
  {
    public static MenuActionState Availability(MenuContext context, string reason = null, bool isChecked = false)
    {
      var model = context?.Target as MenuGalleryState;
      if (model == null || !model.IsValid) return new MenuActionState(true, false, disabledReason: "Gallery target unavailable");
      bool enabled = model.CommandsEnabled && string.IsNullOrEmpty(reason);
      return new MenuActionState(true, enabled, isChecked, enabled ? null : reason ?? "Commands disabled by gallery control");
    }
  }
}
