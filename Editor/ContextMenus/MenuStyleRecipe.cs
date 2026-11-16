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
    internal virtual MenuPresentationThemeSet PresentationTheme => null;
    internal virtual Type PresentationThemeType => null;
    internal abstract void ApplySpecialized(MenuProfile profile);
  }

  [Serializable]
  public sealed class ListMenuStyleRecipe : MenuStyleRecipe
  {
    public ListMenuThemeSet ThemeSet;
    public float ListWidth = 380;
    public float RowHeight = 48;
    public MenuNavigationPolicy Navigation = MenuNavigationPolicy.Inline;
    public override string DisplayName => "ListContextMenu";
    internal override Type DefaultPresenterType => typeof(MToolKit.ContextMenus.Presentation.ListMenuPresenter);
    internal override MenuPresentationThemeSet PresentationTheme => ThemeSet;
    internal override Type PresentationThemeType => typeof(ListMenuThemeSet);
    internal override void ApplySpecialized(MenuProfile profile)
    {
      if (ThemeSet != null) ThemeSet.ApplyTo(profile);
      else
      {
        new MenuListLayout { ListWidth = ListWidth, RowHeight = RowHeight }.ApplyTo(profile);
        profile.ShowCenter = false;
      }
      profile.Navigation = Navigation;
    }
  }

  [Serializable]
  public abstract class RadialMenuStyleRecipe : MenuStyleRecipe
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
      LegacyLayout.ApplyTo(profile);
      profile.Navigation = MenuNavigationPolicy.Replacement;
    }
    protected MenuWheelLayout LegacyLayout => new MenuWheelLayout {
      InnerRadius = InnerRadius, RingWidth = RingWidth, StartAngle = StartAngle,
      Clockwise = Clockwise, AngularGap = AngularGap, DrawSegments = DrawSegments
    };
  }

  [Serializable]
  public class SingleRingMenuStyleRecipe : RadialMenuStyleRecipe
  {
    public WheelMenuThemeSet ThemeSet;
    internal override MenuPresentationThemeSet PresentationTheme => ThemeSet;
    internal override Type PresentationThemeType => typeof(WheelMenuThemeSet);
    internal override void ApplySpecialized(MenuProfile profile)
    {
      if (ThemeSet != null) ThemeSet.ApplyTo(profile); else base.ApplySpecialized(profile);
      profile.Navigation = MenuNavigationPolicy.Replacement;
    }
  }

  [Serializable]
  public sealed class ConcentricMenuStyleRecipe : RadialMenuStyleRecipe
  {
    public NestedWheelMenuThemeSet ThemeSet;
    internal override MenuPresentationThemeSet PresentationTheme => ThemeSet;
    internal override Type PresentationThemeType => typeof(NestedWheelMenuThemeSet);
    public float RingGap = 12;
    public float ChildRingWidth = 72;
    public float ChildArcDegrees = 120;
    public override string DisplayName => "NestedWheelContextMenu";
    internal override void ApplySpecialized(MenuProfile profile)
    {
      if (ThemeSet != null) ThemeSet.ApplyTo(profile);
      else new MenuNestedWheelLayout { InnerRing = LegacyLayout, RingGap = RingGap,
        ChildRingWidth = ChildRingWidth, ChildArcDegrees = ChildArcDegrees }.ApplyTo(profile);
      profile.Navigation = MenuNavigationPolicy.Concentric;
    }
  }

  [Serializable]
  public sealed class ReplacementMenuStyleRecipe : RadialMenuStyleRecipe
  {
    public PieMenuThemeSet ThemeSet;
    internal override MenuPresentationThemeSet PresentationTheme => ThemeSet;
    internal override Type PresentationThemeType => typeof(PieMenuThemeSet);
    public ReplacementMenuStyleRecipe()
    {
      DrawSegments = false;
      WidgetSize = new Vector2(144, 56);
      OverrideShowCenter = true;
      ShowCenter = false;
    }
    public override string DisplayName => "PieContextMenu";
    internal override void ApplySpecialized(MenuProfile profile)
    {
      if (ThemeSet != null) ThemeSet.ApplyTo(profile); else base.ApplySpecialized(profile);
      profile.Navigation = MenuNavigationPolicy.Replacement;
    }
  }

  [Serializable]
  public sealed class CustomMenuStyleRecipe : MenuStyleRecipe
  {
    public MenuNavigationPolicy Navigation = MenuNavigationPolicy.Replacement;
    public override string DisplayName => "Custom presenter";
    internal override void ApplySpecialized(MenuProfile profile) => profile.Navigation = Navigation;
  }
}
