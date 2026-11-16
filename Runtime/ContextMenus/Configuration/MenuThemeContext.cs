using MToolKit.Theme;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Typeset;
using UnityEngine;

namespace MToolKit.ContextMenus.Configuration
{
  /// <summary>Sparse local style, shared style, and legacy fallback bound to the supplied MTK theme service.</summary>
  public readonly struct MenuThemeContext
  {
    public MenuThemeContext(MenuSkin skin = null, MenuSkin sharedOverrides = null,
      MenuSkin overrides = null, IThemeService themeService = null)
    { Skin = skin; SharedOverrides = sharedOverrides; Overrides = overrides; ThemeService = themeService; }
    public MenuSkin Skin { get; }
    public MenuSkin SharedOverrides { get; }
    public MenuSkin Overrides { get; }
    public IThemeService ThemeService { get; }
    public Color GetColor(MenuVisualRole role, MenuThemeState state = MenuThemeState.Any)
    {
      if (TryColor(Overrides, role, state, out var color) || TryColor(SharedOverrides, role, state, out color) ||
          TryColor(Skin, role, state, out color)) return color;
      return role == MenuVisualRole.Checked ? GetColor(MenuVisualRole.Label, state) : MenuSkin.DefaultColor(role);
    }
    public Typeset GetTypeset(bool description = false, MenuThemeState state = MenuThemeState.Any)
    {
      if (TryTypeset(Overrides, description, state, out var token) || TryTypeset(SharedOverrides, description, state, out token) ||
          TryTypeset(Skin, description, state, out token)) return token;
      return null;
    }
    public Spacing GetSpacing()
    {
      if (Overrides != null && Overrides.Spacing != null) return Overrides.GetSpacing(ThemeService);
      if (SharedOverrides != null && SharedOverrides.Spacing != null) return SharedOverrides.GetSpacing(ThemeService);
      return Skin != null ? Skin.GetSpacing(ThemeService) : null;
    }
    private bool TryColor(MenuSkin layer, MenuVisualRole role, MenuThemeState state, out Color color)
    {
      color = default;
      return layer != null && (layer.TryGetColor(role, state, out color, ThemeService) ||
        state != MenuThemeState.Any && layer.TryGetColor(role, MenuThemeState.Any, out color, ThemeService));
    }
    private bool TryTypeset(MenuSkin layer, bool description, MenuThemeState state, out Typeset token)
    {
      token = null;
      return layer != null && (layer.TryGetTypeset(description, state, out token, ThemeService) ||
        state != MenuThemeState.Any && layer.TryGetTypeset(description, MenuThemeState.Any, out token, ThemeService));
    }
    public static MenuThemeState SelectState(MenuActionState availability, bool selected, bool hovered)
      => !availability.Enabled ? MenuThemeState.Disabled : hovered ? MenuThemeState.Hovered :
        selected ? MenuThemeState.Selected : availability.IsChecked ? MenuThemeState.Checked : MenuThemeState.Normal;
  }
}
