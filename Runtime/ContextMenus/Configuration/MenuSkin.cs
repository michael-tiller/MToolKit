using System;
using MToolKit.Theme;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Swatch;
using MToolKit.Theme.Typeset;
using UnityEngine;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.ContextMenus.Configuration
{
  public enum MenuVisualRole
  {
    Background, Normal, Hovered, Selected, Disabled, InnerBorder, OuterBorder,
    Separator, Label, Description, Icon, Center, Auxiliary, Checked, Backdrop
  }

  public enum MenuThemeState { Any = 0, Normal = 1, Hovered = 2, Selected = 3, Disabled = 4, Checked = 5 }

  [Serializable]
  public sealed class MenuSkinColor
  {
    [field: SerializeField] public MenuVisualRole Role { get; set; }
    [field: SerializeField] public MenuThemeState State { get; set; }
    [field: SerializeField] public Swatch Swatch { get; set; }
    [field: SerializeField] public bool OverrideColor { get; set; }
    [field: SerializeField] public Color Color { get; set; } = Color.white;
  }

  [Serializable]
  public sealed class MenuSkinTypography
  {
    [field: SerializeField] public MenuThemeState State { get; set; }
    [field: SerializeField] public bool Description { get; set; }
    [field: SerializeField] public Typeset Typeset { get; set; }
  }

  /// <summary>
  /// Sparse menu tokens use MTK's semantic Swatch, Typeset and Spacing assets. Explicit local
  /// colors win over active-theme resolution. Missing entries can inherit another menu layer.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Menu Skin", fileName = "MenuSkin")]
  public sealed class MenuSkin : ScriptableObject
  {
    [field: SerializeField] public MenuSkinColor[] Colors { get; set; } = Array.Empty<MenuSkinColor>();
    [field: SerializeField] public MenuSkinTypography[] Typography { get; set; } = Array.Empty<MenuSkinTypography>();
    [field: SerializeField] public bool OverrideTheme { get; set; }
    [field: SerializeField] public Typeset LabelTypeset { get; set; }
    [field: SerializeField] public Typeset DescriptionTypeset { get; set; }
    [field: SerializeField] public Spacing Spacing { get; set; }

    public Color GetColor(MenuVisualRole role, IThemeService themeService = null)
      => GetColor(role, MenuThemeState.Any, themeService);

    public Color GetColor(MenuVisualRole role, MenuThemeState state, IThemeService themeService = null)
    {
      if (TryGetColor(role, state, out var color, themeService) ||
          state != MenuThemeState.Any && TryGetColor(role, MenuThemeState.Any, out color, themeService)) return color;
      return role == MenuVisualRole.Checked ? GetColor(MenuVisualRole.Label, state, themeService) : DefaultColor(role);
    }

    /// <summary>Resolves only the requested state. False means this property inherits a lower layer.</summary>
    public bool TryGetColor(MenuVisualRole role, MenuThemeState state, out Color color, IThemeService themeService = null)
    {
      color = default;
      var entry = Find(role, state);
      if (entry == null) return false;
      if (entry.OverrideColor) { color = entry.Color; return true; }
      var token = entry.Swatch;
      if (token == null) return false;
      var theme = Current(themeService);
      if (!OverrideTheme && theme != null && theme.SwatchRegistry != null && theme.SwatchRegistry.Swatches != null)
        token = theme.SwatchRegistry.GetSwatch(token.Id) ?? token;
      color = token; return true;
    }

    public Typeset GetTypeset(bool description = false, IThemeService themeService = null)
      => GetTypeset(description, MenuThemeState.Any, themeService);

    public Typeset GetTypeset(bool description, MenuThemeState state, IThemeService themeService = null)
    {
      if (TryGetTypeset(description, state, out var token, themeService) ||
          state != MenuThemeState.Any && TryGetTypeset(description, MenuThemeState.Any, out token, themeService)) return token;
      return null;
    }

    public bool TryGetTypeset(bool description, MenuThemeState state, out Typeset typeset, IThemeService themeService = null)
    {
      typeset = null;
      if (Typography != null)
        for (int i = Typography.Length - 1; i >= 0; i--)
          if (Typography[i] != null && Typography[i].Description == description && Typography[i].State == state)
          { typeset = Typography[i].Typeset; break; }
      if (typeset == null && state == MenuThemeState.Any) typeset = description ? DescriptionTypeset : LabelTypeset;
      if (typeset == null) return false;
      var theme = Current(themeService);
      if (!OverrideTheme && theme != null && theme.TypesetStyleRegistry != null && theme.TypesetStyleRegistry.Typesets != null)
        typeset = theme.TypesetStyleRegistry.GetTypeset(typeset.Id) ?? typeset;
      return true;
    }

    public Spacing GetSpacing(IThemeService themeService = null)
    {
      var theme = Current(themeService);
      if (Spacing == null || OverrideTheme || theme == null || theme.SpacingRegistry == null || theme.SpacingRegistry.Spacings == null) return Spacing;
      return theme.SpacingRegistry.GetSpacing(Spacing.Id) ?? Spacing;
    }

    public void SetColor(MenuVisualRole role, Color color) => SetColor(role, color, MenuThemeState.Any);
    public void SetColor(MenuVisualRole role, Color color, MenuThemeState state)
    {
      var entry = GetOrCreate(role, state); entry.Color = color; entry.OverrideColor = true;
    }
    public void SetSwatch(MenuVisualRole role, Swatch swatch) => SetSwatch(role, swatch, MenuThemeState.Any);
    public void SetSwatch(MenuVisualRole role, Swatch swatch, MenuThemeState state)
    {
      var entry = GetOrCreate(role, state); entry.Swatch = swatch; entry.OverrideColor = false;
    }

    private MenuSkinColor Find(MenuVisualRole role, MenuThemeState state)
    {
      if (Colors == null) return null;
      for (int i = Colors.Length - 1; i >= 0; i--)
        if (Colors[i] != null && Colors[i].Role == role && Colors[i].State == state) return Colors[i];
      return null;
    }
    private MenuSkinColor GetOrCreate(MenuVisualRole role, MenuThemeState state)
    {
      var entry = Find(role, state);
      if (entry != null) return entry;
      entry = new MenuSkinColor { Role = role, State = state };
      var colors = Colors ?? Array.Empty<MenuSkinColor>(); Array.Resize(ref colors, colors.Length + 1);
      colors[colors.Length - 1] = entry; Colors = colors; return entry;
    }
    internal static IThemeService Service(IThemeService supplied)
      => supplied ?? (CurrentTheme.HasInstance ? CurrentTheme.Instance : null);
    private static ThemeAsset Current(IThemeService supplied) => Service(supplied)?.Current;

    public static Color DefaultColor(MenuVisualRole role)
    {
      switch (role)
      {
        case MenuVisualRole.Backdrop: return new Color(0, 0, 0, .35f);
        case MenuVisualRole.Background: return new Color(.04f, .05f, .07f, .95f);
        case MenuVisualRole.Normal: return new Color(.12f, .14f, .18f, .95f);
        case MenuVisualRole.Hovered: return new Color(.22f, .28f, .36f, 1);
        case MenuVisualRole.Selected: return new Color(.12f, .45f, .72f, 1);
        case MenuVisualRole.Disabled: return new Color(.12f, .12f, .13f, .65f);
        case MenuVisualRole.InnerBorder:
        case MenuVisualRole.OuterBorder:
        case MenuVisualRole.Separator: return new Color(.58f, .65f, .72f, 1);
        case MenuVisualRole.Description: return new Color(.82f, .85f, .9f, 1);
        case MenuVisualRole.Center: return new Color(.08f, .1f, .14f, .95f);
        case MenuVisualRole.Auxiliary: return new Color(.24f, .3f, .38f, 1);
        default: return Color.white;
      }
    }
  }
}
