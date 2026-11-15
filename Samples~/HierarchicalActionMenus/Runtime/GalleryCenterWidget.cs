using MToolKit.ContextMenus.Widgets;
using TMPro;
using UnityEngine;

namespace MToolKit.ContextMenus.Samples
{
  /// <summary>A fully custom center; the framework does not control its children or require theme opt-in.</summary>
  public sealed class GalleryCenterWidget : MonoBehaviour, IMenuWidget
  {
    [field: SerializeField] public TMP_Text Title { get; set; }
    [field: SerializeField] public TMP_Text Details { get; set; }
    [field: SerializeField] public TMP_Text Hint { get; set; }
    public string BoundAction { get; private set; }
    public void Bind(MenuWidgetState state)
    {
      BoundAction = state?.Action?.Id.Value;
      if (Title != null) Title.text = state?.Label ?? string.Empty;
      if (Details != null)
        Details.text = state == null ? string.Empty : state.Description + "\n" + state.Details?.CostText + "\n" + state.Details?.VariantText;
      if (Hint != null) Hint.text = state?.BindingHint ?? string.Empty;
    }
    public void Unbind()
    {
      BoundAction = null;
      if (Title != null) Title.text = string.Empty;
      if (Details != null) Details.text = string.Empty;
      if (Hint != null) Hint.text = string.Empty;
    }
  }
}
