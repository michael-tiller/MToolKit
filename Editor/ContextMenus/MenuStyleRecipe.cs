using System;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Input;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [Serializable]
  public abstract class MenuStyleRecipe
  {
    public string Id = Guid.NewGuid().ToString("N");
    public string AssetName = "MenuStyle";
    public MenuProfile Output;
    [Tooltip("Optional replacement. Empty preserves the existing presenter or creates a native default.")]
    public GameObject PresenterPrefab;
    [Tooltip("Optional per-style composition. Empty references retain the shared or existing output value.")]
    public GameObject ItemPrefab;
    public GameObject CenterPrefab;
    public GameObject SecondaryPrefab;
    [Tooltip("Sparse MTK theme tokens for this authoring layer. Null inherits the next layer.")]
    public MenuSkin ThemeOverrides;
    public MenuSkin Skin;
    public bool OverrideShowCenter;
    public bool ShowCenter;
    public bool OverrideInputBindings;
    public MenuInputBindings InputBindings = new MenuInputBindings();
    public int MaximumVisibleItems = 8;
    public Vector2 WidgetSize = new Vector2(96, 64);
    public abstract string DisplayName { get; }
    public virtual bool UsesRadialLayout => false;
    internal virtual Type DefaultPresenterType => null;
    internal abstract void ApplySpecialized(MenuProfile profile);
  }

  [Serializable]
  public sealed class ListMenuStyleRecipe : MenuStyleRecipe
  {
    public float ListWidth = 380;
    public float RowHeight = 48;
    public MenuNavigationPolicy Navigation = MenuNavigationPolicy.Inline;
    public override string DisplayName => "ListContextMenu";
    internal override Type DefaultPresenterType => typeof(MToolKit.ContextMenus.Presentation.ListMenuPresenter);
    internal override void ApplySpecialized(MenuProfile profile)
    {
      profile.ListWidth = ListWidth; profile.RowHeight = RowHeight;
      profile.Navigation = Navigation; profile.ShowCenter = false;
    }
  }

  [Serializable]
  public class SingleRingMenuStyleRecipe : MenuStyleRecipe
  {
    public float InnerRadius = 166;
    public float RingWidth = 90;
    public float StartAngle = 90;
    public bool Clockwise = true;
    public float AngularGap = 3;
    public bool DrawSegments = true;
    public override string DisplayName => "WheelContextMenu";
    public override bool UsesRadialLayout => true;
    internal override Type DefaultPresenterType => typeof(MToolKit.ContextMenus.Presentation.RadialMenuPresenter);
    internal override void ApplySpecialized(MenuProfile profile)
    {
      profile.InnerRadius = InnerRadius; profile.RingWidth = RingWidth;
      profile.StartAngle = StartAngle; profile.Clockwise = Clockwise;
      profile.AngularGap = AngularGap; profile.DrawSegments = DrawSegments;
      profile.Navigation = MenuNavigationPolicy.Replacement;
    }
  }

  [Serializable]
  public sealed class ConcentricMenuStyleRecipe : SingleRingMenuStyleRecipe
  {
    public float RingGap = 12;
    public float ChildRingWidth = 72;
    public float ChildArcDegrees = 120;
    public override string DisplayName => "NestedWheelContextMenu";
    internal override void ApplySpecialized(MenuProfile profile)
    {
      base.ApplySpecialized(profile);
      profile.Navigation = MenuNavigationPolicy.Concentric;
      profile.RingGap = RingGap; profile.ChildRingWidth = ChildRingWidth;
      profile.ChildArcDegrees = ChildArcDegrees;
    }
  }

  [Serializable]
  public sealed class ReplacementMenuStyleRecipe : SingleRingMenuStyleRecipe
  {
    public ReplacementMenuStyleRecipe()
    {
      DrawSegments = false;
      WidgetSize = new Vector2(144, 56);
      OverrideShowCenter = true;
      ShowCenter = false;
    }
    public override string DisplayName => "PieContextMenu";
    internal override void ApplySpecialized(MenuProfile profile) => base.ApplySpecialized(profile);
  }

  [Serializable]
  public sealed class CustomMenuStyleRecipe : MenuStyleRecipe
  {
    public MenuNavigationPolicy Navigation = MenuNavigationPolicy.Replacement;
    public override string DisplayName => "Custom presenter";
    internal override void ApplySpecialized(MenuProfile profile) => profile.Navigation = Navigation;
  }
}
