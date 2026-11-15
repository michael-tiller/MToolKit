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
    Separator, Label, Description, Icon, Center, Auxiliary
  }

  [Serializable]
  public sealed class MenuSkinColor
  {
    [field: SerializeField] public MenuVisualRole Role { get; set; }
    [field: SerializeField] public Swatch Swatch { get; set; }
    [field: SerializeField] public bool OverrideColor { get; set; }
    [field: SerializeField] public Color Color { get; set; } = Color.white;
  }

  /// <summary>
  /// Menu composition consumes existing semantic theme assets. Explicit local colors win, followed by
  /// the active theme's matching token, the authored token, and finally complete readable defaults.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Menu Skin", fileName = "MenuSkin")]
  public sealed class MenuSkin : ScriptableObject
  {
    [field: SerializeField] public MenuSkinColor[] Colors { get; set; } = Array.Empty<MenuSkinColor>();
    [field: SerializeField] public bool OverrideTheme { get; set; }
    [field: SerializeField] public Typeset LabelTypeset { get; set; }
    [field: SerializeField] public Typeset DescriptionTypeset { get; set; }
    [field: SerializeField] public Spacing Spacing { get; set; }

    public Color GetColor(MenuVisualRole role, IThemeService themeService = null)
    {
      var entry = Find(role);
      if (entry == null) return DefaultColor(role);
      if (entry.OverrideColor) return entry.Color;
      var token = entry.Swatch;
      if (token == null) return DefaultColor(role);
      var theme = Current(themeService);
      if (!OverrideTheme && theme != null && theme.SwatchRegistry != null && theme.SwatchRegistry.Swatches != null)
      {
        var resolved = theme.SwatchRegistry.GetSwatch(token.Id);
        if (resolved != null) token = resolved;
      }
      return token;
    }

    public Typeset GetTypeset(bool description = false, IThemeService themeService = null)
    {
      var token = description ? DescriptionTypeset : LabelTypeset;
      var theme = Current(themeService);
      if (token == null || OverrideTheme || theme == null || theme.TypesetStyleRegistry == null ||
          theme.TypesetStyleRegistry.Typesets == null) return token;
      return theme.TypesetStyleRegistry.GetTypeset(token.Id) ?? token;
    }

    public Spacing GetSpacing(IThemeService themeService = null)
    {
      var theme = Current(themeService);
      if (Spacing == null || OverrideTheme || theme == null || theme.SpacingRegistry == null ||
          theme.SpacingRegistry.Spacings == null) return Spacing;
      return theme.SpacingRegistry.GetSpacing(Spacing.Id) ?? Spacing;
    }

    public void SetColor(MenuVisualRole role, Color color)
    {
      var entry = GetOrCreate(role);
      entry.Color = color;
      entry.OverrideColor = true;
    }

    public void SetSwatch(MenuVisualRole role, Swatch swatch)
    {
      var entry = GetOrCreate(role);
      entry.Swatch = swatch;
      entry.OverrideColor = false;
    }

    private MenuSkinColor Find(MenuVisualRole role)
    {
      if (Colors == null) return null;
      for (int i = Colors.Length - 1; i >= 0; i--)
        if (Colors[i] != null && Colors[i].Role == role) return Colors[i];
      return null;
    }

    private MenuSkinColor GetOrCreate(MenuVisualRole role)
    {
      var entry = Find(role);
      if (entry != null) return entry;
      entry = new MenuSkinColor { Role = role };
      var colors = Colors ?? Array.Empty<MenuSkinColor>();
      Array.Resize(ref colors, colors.Length + 1);
      colors[colors.Length - 1] = entry;
      Colors = colors;
      return entry;
    }

    internal static IThemeService Service(IThemeService supplied)
      => supplied ?? (CurrentTheme.HasInstance ? CurrentTheme.Instance : null);

    private static ThemeAsset Current(IThemeService supplied) => Service(supplied)?.Current;

    public static Color DefaultColor(MenuVisualRole role)
    {
      switch (role)
      {
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
