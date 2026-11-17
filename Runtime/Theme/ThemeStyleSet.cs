using UnityEngine;
using SpacingToken = MToolKit.Theme.Spacing.Spacing;
using SwatchToken = MToolKit.Theme.Swatch.Swatch;
using TypesetToken = MToolKit.Theme.Typeset.Typeset;

namespace MToolKit.Theme
{
  /// <summary>Reusable authored visual tokens shared by concrete UI style assets.</summary>
  public abstract class ThemeStyleSet : ScriptableObject
  {
    [field: SerializeField] public SwatchToken Background { get; set; }
    [field: SerializeField] public SwatchToken Foreground { get; set; }
    [field: SerializeField] public SwatchToken Description { get; set; }
    [field: SerializeField] public SwatchToken Icon { get; set; }
    [field: SerializeField] public SwatchToken Border { get; set; }
    [field: SerializeField] public TypesetToken LabelTypeset { get; set; }
    [field: SerializeField] public TypesetToken DescriptionTypeset { get; set; }
    [field: SerializeField] public SpacingToken Spacing { get; set; }
    [field: SerializeField] public bool OverrideTheme { get; set; }
  }
}
