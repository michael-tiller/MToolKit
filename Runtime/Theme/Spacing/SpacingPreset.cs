using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Theme.Spacing
{
  [RequireComponent(typeof(HorizontalOrVerticalLayoutGroup))]
  public class SpacingPreset : ThemePreset<Spacing>
  {
    [field: SerializeField]
    [field: Required]
    public HorizontalOrVerticalLayoutGroup LayoutGroup { get; private set; }

    protected override void AutoWire()
    {
      LayoutGroup = GetComponent<HorizontalOrVerticalLayoutGroup>();
    }

    protected override Spacing Resolve(Theme theme, string id)
    {
      // SpacingRegistry was added after Swatch/Typeset -> tolerate themes that haven't wired one yet.
      return theme.SpacingRegistry != null ? theme.SpacingRegistry.GetSpacing(id) : null;
    }

    protected override void Apply(Spacing spacing)
    {
      if (LayoutGroup == null || spacing == null) return;

      LayoutGroup.padding = spacing.Padding;
      LayoutGroup.spacing = spacing.Gap;
    }
  }
}