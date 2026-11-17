using MToolKit.Theme;
using MToolKit.Theme.Swatch;
using UnityEngine;

namespace MToolKit.ContextMenus.Configuration
{
  /// <summary>One reusable menu appearance; its caller chooses the interaction-state slot.</summary>
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Menu Style Set", fileName = "MenuStyleSet")]
  public sealed class MenuStyleSet : ThemeStyleSet
  {
    [field: SerializeField] public Swatch Normal { get; set; }
    [field: SerializeField] public Swatch Hovered { get; set; }
    [field: SerializeField] public Swatch Selected { get; set; }
    [field: SerializeField] public Swatch Disabled { get; set; }
    [field: SerializeField] public Swatch PanelBackground { get; set; }
    [field: SerializeField] public Swatch CenterBackground { get; set; }
    [field: SerializeField] public Swatch AuxiliaryBackground { get; set; }
    [field: SerializeField] public Swatch Backdrop { get; set; }
    [field: SerializeField] public Swatch InnerBorder { get; set; }
    [field: SerializeField] public Swatch OuterBorder { get; set; }
    [field: SerializeField] public Swatch Separator { get; set; }
    [field: SerializeField] public Swatch Checked { get; set; }

    internal Swatch GetSwatch(MenuVisualRole role)
    {
      switch (role)
      {
        case MenuVisualRole.Normal: return First(Normal, Background);
        case MenuVisualRole.Hovered: return First(Hovered, Background);
        case MenuVisualRole.Selected: return First(Selected, Background);
        case MenuVisualRole.Disabled: return First(Disabled, Background);
        case MenuVisualRole.Background: return PanelBackground;
        case MenuVisualRole.Center: return CenterBackground;
        case MenuVisualRole.Auxiliary: return AuxiliaryBackground;
        case MenuVisualRole.Backdrop: return Backdrop;
        case MenuVisualRole.InnerBorder: return First(InnerBorder, Border);
        case MenuVisualRole.OuterBorder: return First(OuterBorder, Border);
        case MenuVisualRole.Separator: return Separator;
        case MenuVisualRole.Checked: return Checked;
        case MenuVisualRole.Label: return Foreground;
        case MenuVisualRole.Description: return Description;
        case MenuVisualRole.Icon: return Icon;
        default: return null;
      }
    }

    private static Swatch First(Swatch preferred, Swatch fallback)
      => preferred != null ? preferred : fallback;
  }
}
