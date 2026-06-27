using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Theme.Spacing
{
  [RequireComponent(typeof(LayoutGroup))]
  public class SpacingPreset : ThemePreset<Spacing>
  {
    public enum ELayoutType
    {
      None = 0,
      HorizontalOrVertical = 1,
      Grid = 2
    }
    
    
    
    [field: SerializeField]
    [field: Required]
    public LayoutGroup LayoutGroup { get; private set; }


    [ReadOnly]
    [ShowInInspector]
    [ShowIf("@layoutType", ELayoutType.HorizontalOrVertical)]
    private HorizontalOrVerticalLayoutGroup horizontalOrVerticalLayoutGroup;

    [ReadOnly]
    [ShowInInspector]
    [ShowIf("@layoutType", ELayoutType.Grid)]
    private GridLayoutGroup gridLayoutGroup;

    [ReadOnly]
    [ShowInInspector]
    private ELayoutType layoutType;
    
    protected override void AutoWire()
    {
      LayoutGroup = GetComponent<LayoutGroup>();

      if (LayoutGroup is HorizontalOrVerticalLayoutGroup horizontalOrVerticalLayoutGroup)
      {
        this.horizontalOrVerticalLayoutGroup = horizontalOrVerticalLayoutGroup;
        layoutType = ELayoutType.HorizontalOrVertical;
      }
      else if (LayoutGroup is GridLayoutGroup gridLayoutGroup)
      {
        this.gridLayoutGroup = gridLayoutGroup;
        layoutType = ELayoutType.Grid;
      }
    }

    protected override Spacing Resolve(Theme theme, string id)
    {
      // SpacingRegistry was added after Swatch/Typeset -> tolerate themes that haven't wired one yet.
      return theme.SpacingRegistry != null ? theme.SpacingRegistry.GetSpacing(id) : null;
    }

    protected override void Apply(Spacing spacing)
    {
      if (LayoutGroup == null || spacing == null) return;

      // RectOffset is a reference type. The Spacing SO is a read-only source of truth (changed only by
      // an author), so copy it — assigning spacing.Padding directly would alias every consuming
      // LayoutGroup to the SO's instance, letting an Inspector/runtime padding edit write back and
      // corrupt the asset (and bleed across all other presets bound to it).
      RectOffset p = spacing.Padding;
      LayoutGroup.padding = new RectOffset(p.left, p.right, p.top, p.bottom);

      if (layoutType == ELayoutType.HorizontalOrVertical)
        horizontalOrVerticalLayoutGroup.spacing = spacing.Gap;
      else if (layoutType == ELayoutType.Grid)
        gridLayoutGroup.spacing = new Vector2(spacing.Gap, spacing.Gap);
    }
  }
}