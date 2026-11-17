using System;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using UnityEngine;

namespace MToolKit.ContextMenus.Configuration
{
  public enum MenuNavigationPolicy { Inline, Replacement, Concentric, Flyout }
  public enum MenuListOverflowPolicy { Pagination = 0, Scrolling = 1 }
  public enum MenuAnchorPolicy
  {
    CursorTopLeft = 0, TopLeft = CursorTopLeft, Centered = 1, BottomCenter = 2,
    TopRight = 3, BottomLeft = 4, BottomRight = 5, TopCenter = 6
  }

  /// <summary>Composition and interaction policy. The presenter component, not a presentation enum, owns layout.</summary>
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Menu Profile", fileName = "MenuProfile")]
  public sealed class MenuProfile : ScriptableObject
  {
    [field: SerializeField] public GameObject PresenterPrefab { get; set; }
    [field: SerializeField] public ContextMenuPrefabReference PresenterReference { get; set; }
    [field: SerializeField] public MenuProfile FallbackProfile { get; set; }
    [field: SerializeField] public GameObject ItemPrefab { get; set; }
    [field: SerializeField] public GameObject CenterPrefab { get; set; }
    [field: SerializeField] public GameObject SecondaryPrefab { get; set; }
    [field: SerializeField] public MenuSkin Skin { get; set; }
    [field: SerializeField] public MenuSkin SharedThemeOverrides { get; set; }
    [field: SerializeField] public MenuSkin ThemeOverrides { get; set; }
    [field: SerializeField] public MenuNavigationPolicy Navigation { get; set; } = MenuNavigationPolicy.Inline;
    [field: SerializeField] public MenuAnchorPolicy Anchor { get; set; } = MenuAnchorPolicy.CursorTopLeft;
    [field: SerializeField] public float InnerRadius { get; set; } = 90;
    [field: SerializeField] public float RingWidth { get; set; } = 90;
    [field: SerializeField] public float RingGap { get; set; } = 12;
    [field: SerializeField] public float ChildRingWidth { get; set; } = 72;
    [field: SerializeField] public float StartAngle { get; set; } = 90;
    [field: SerializeField] public bool Clockwise { get; set; } = true;
    [field: SerializeField] public float AngularGap { get; set; } = 3;
    [field: SerializeField] public float ChildArcDegrees { get; set; } = 120;
    [field: SerializeField] public int MaximumVisibleItems { get; set; } = 8;
    [field: SerializeField] public float ListWidth { get; set; } = 320;
    [field: SerializeField] public float RowHeight { get; set; } = 40;
    [field: SerializeField] public MenuListOverflowPolicy ListOverflow { get; set; } = MenuListOverflowPolicy.Pagination;
    [field: SerializeField] public Vector2 WidgetSize { get; set; } = new Vector2(96, 64);
    [field: SerializeField] public bool ShowCenter { get; set; } = true;
    [field: SerializeField, Tooltip("Show this presenter's Back control. Disable when a host supplies one shared Back control.")]
    public bool ShowBackControl { get; set; } = true;
    [field: SerializeField] public bool Backdrop { get; set; } = true;
    [field: SerializeField] public bool DrawSegments { get; set; } = true;
    [field: SerializeField] public bool ReducedMotion { get; set; }
    [field: SerializeField] public MenuTransitionAsset Transition { get; set; }
    [field: SerializeField] public float MinimumTargetSize { get; set; } = 32;
    [field: SerializeField] public MenuInputBindings InputBindings { get; set; } = new MenuInputBindings();
    [field: SerializeField] public float AnalogDeadZone { get; set; } = .25f;
    [field: SerializeField] public float AnalogHysteresis { get; set; } = 5;
    [field: SerializeField, Tooltip("Unscaled seconds at stick neutral before clearing selection. Zero clears immediately.")]
    public float AnalogSelectionResetDelay { get; set; } = .35f;

    public MenuThemeContext GetTheme(MToolKit.Theme.IThemeService themeService = null)
      => new MenuThemeContext(Skin, SharedThemeOverrides, ThemeOverrides, themeService);

    /// <summary>Authoring diagnostics never silently clamp hit targets or replace a caller's custom component.</summary>
    public bool Validate(out string diagnostic, bool requirePresenter = true)
    {
      diagnostic = null;
      if (Transition != null && !Transition.Validate(out diagnostic)) return false;
      if (!Enum.IsDefined(typeof(MenuNavigationPolicy), Navigation)) return Invalid(nameof(Navigation), out diagnostic);
      if (!Enum.IsDefined(typeof(MenuListOverflowPolicy), ListOverflow)) return Invalid(nameof(ListOverflow), out diagnostic);
      if (!Enum.IsDefined(typeof(MenuAnchorPolicy), Anchor)) return Invalid(nameof(Anchor), out diagnostic);
      if (FallbackProfile == this) return Invalid(nameof(FallbackProfile), out diagnostic);
      if (!Finite(InnerRadius) || InnerRadius < 0) return Invalid(nameof(InnerRadius), out diagnostic);
      if (!Positive(RingWidth)) return Invalid(nameof(RingWidth), out diagnostic);
      if (!Finite(RingGap) || RingGap < 0) return Invalid(nameof(RingGap), out diagnostic);
      if (!Positive(ChildRingWidth)) return Invalid(nameof(ChildRingWidth), out diagnostic);
      if (!Finite(StartAngle)) return Invalid(nameof(StartAngle), out diagnostic);
      if (!Positive(ChildArcDegrees) || ChildArcDegrees > 360) return Invalid(nameof(ChildArcDegrees), out diagnostic);
      if (MaximumVisibleItems <= 0) return Invalid(nameof(MaximumVisibleItems), out diagnostic);
      float smallestArc = Navigation == MenuNavigationPolicy.Concentric ? ChildArcDegrees : 360;
      if (!Finite(AngularGap) || AngularGap < 0 || AngularGap >= smallestArc / MaximumVisibleItems)
        return Invalid(nameof(AngularGap), out diagnostic);
      if (!Positive(MinimumTargetSize)) return Invalid(nameof(MinimumTargetSize), out diagnostic);
      if (!Positive(ListWidth) || ListWidth < MinimumTargetSize) return Invalid(nameof(ListWidth), out diagnostic);
      if (!Positive(RowHeight) || RowHeight < MinimumTargetSize) return Invalid(nameof(RowHeight), out diagnostic);
      if (!Positive(WidgetSize.x) || !Positive(WidgetSize.y) ||
          WidgetSize.x < MinimumTargetSize || WidgetSize.y < MinimumTargetSize)
        return Invalid(nameof(WidgetSize), out diagnostic);
      if (!Finite(AnalogDeadZone) || AnalogDeadZone < 0 || AnalogDeadZone >= 1)
        return Invalid(nameof(AnalogDeadZone), out diagnostic);
      if (!Finite(AnalogHysteresis) || AnalogHysteresis < 0) return Invalid(nameof(AnalogHysteresis), out diagnostic);
      if (!Finite(AnalogSelectionResetDelay) || AnalogSelectionResetDelay < 0)
        return Invalid(nameof(AnalogSelectionResetDelay), out diagnostic);
      if (InputBindings == null) return Invalid(nameof(InputBindings), out diagnostic);
      if (InputBindings.UseNativeUguiNavigation && InputBindings.RoutePointerClick)
      {
        diagnostic = "Native uGUI navigation and semantic pointer-click routing must use separate profiles to avoid duplicate activation.";
        return false;
      }
      bool hasAddressable = PresenterReference != null && Guid.TryParseExact(PresenterReference.AssetGUID, "N", out _);
      if (PresenterPrefab == null && PresenterReference != null && !hasAddressable)
        return Invalid(nameof(PresenterReference), out diagnostic);
      if (PresenterPrefab == null && !hasAddressable && requirePresenter)
      {
        diagnostic = "PresenterPrefab must reference a prefab with an IContextMenuPresenter on its root.";
        return false;
      }
      if (PresenterPrefab != null && !HasComponent<IContextMenuPresenter>(PresenterPrefab))
      {
        diagnostic = "PresenterPrefab root must implement IContextMenuPresenter.";
        return false;
      }
      if (!ValidateWidget(ItemPrefab, nameof(ItemPrefab), out diagnostic) ||
          !ValidateWidget(CenterPrefab, nameof(CenterPrefab), out diagnostic) ||
          !ValidateWidget(SecondaryPrefab, nameof(SecondaryPrefab), out diagnostic)) return false;
      return true;
    }

    private static bool ValidateWidget(GameObject prefab, string field, out string diagnostic)
    {
      diagnostic = null;
      if (prefab == null || HasComponent<IMenuWidget>(prefab)) return true;
      diagnostic = field + " root must implement IMenuWidget; label, icon and other child components are optional.";
      return false;
    }

    private static bool HasComponent<T>(GameObject prefab)
    {
      var components = prefab.GetComponents<MonoBehaviour>();
      for (int i = 0; i < components.Length; i++) if (components[i] is T) return true;
      return false;
    }

    private static bool Invalid(string field, out string diagnostic)
    {
      diagnostic = "MenuProfile." + field + " is invalid. Check finite dimensions, visible-item capacity and minimum target size.";
      return false;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Positive(float value) => Finite(value) && value > 0;
  }
}
