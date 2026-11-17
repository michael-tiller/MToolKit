using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Swatch;
using MToolKit.Theme.Typeset;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuThemeLayerTests
  {
    private readonly List<Object> objects = new();
    [TearDown] public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear();
    }
    private T Own<T>(T value) where T : Object { objects.Add(value); return value; }
    private T Asset<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
    private MenuSkin Skin(MenuVisualRole role, Color value)
    { var skin = Asset<MenuSkin>(); skin.SetColor(role, value); return skin; }
    private static MenuSkinColor Rule(MenuVisualRole role, MenuThemeState state, Color value)
      => new() { Role = role, State = state, OverrideColor = true, Color = value };
    private MenuActionWidget Widget()
    {
      var root = Own(new GameObject("Theme-layer widget", typeof(RectTransform)));
      var widget = root.AddComponent<MenuActionWidget>(); widget.Background = root.AddComponent<Image>();
      var check = new GameObject("Check", typeof(RectTransform), typeof(Image)); check.transform.SetParent(root.transform, false);
      widget.Check = check.GetComponent<Image>(); return widget;
    }
    private Swatch Token(string id, Color color)
    {
      var token = Asset<Swatch>(); token.name = id;
      typeof(Swatch).GetProperty(nameof(Swatch.Color)).SetValue(token, color); return token;
    }
    private ThemeAsset Theme(params Swatch[] swatches)
    {
      var theme = Asset<ThemeAsset>(); var registry = Asset<SwatchRegistry>();
      typeof(SwatchRegistry).GetProperty(nameof(SwatchRegistry.Swatches)).SetValue(registry, swatches);
      typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.SwatchRegistry)).SetValue(theme, registry); return theme;
    }
    private static MenuWidgetState State(MenuSkin fallback, MenuThemeContext theme,
      bool enabled = true, bool selected = false, bool hovered = false, bool check = false)
      => new(new MenuAction("action", new MenuLabel("Action")), new MenuActionState(true, enabled, check),
        selected: selected, skin: fallback, isHovered: hovered, theme: theme);

    [Test]
    public void SparsePropertyLayersPreferStyleThenSharedThenLegacySkin()
    {
      var fallback = Skin(MenuVisualRole.Label, Color.red); fallback.SetColor(MenuVisualRole.Icon, Color.gray);
      var shared = Skin(MenuVisualRole.Label, Color.blue); shared.SetColor(MenuVisualRole.Description, Color.green);
      var style = Skin(MenuVisualRole.Label, Color.yellow);
      var theme = new MenuThemeContext(fallback, shared, style);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Normal), Is.EqualTo(Color.yellow));
      Assert.That(theme.GetColor(MenuVisualRole.Description, MenuThemeState.Normal), Is.EqualTo(Color.green));
      Assert.That(theme.GetColor(MenuVisualRole.Icon, MenuThemeState.Normal), Is.EqualTo(Color.gray));
      style.Colors = Array.Empty<MenuSkinColor>();
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Normal), Is.EqualTo(Color.blue));
      shared.Colors = Array.Empty<MenuSkinColor>();
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Normal), Is.EqualTo(Color.red));
    }

    [Test]
    public void ExplicitStyleAnyWinsSharedExactAndTransparentRemainsAnExplicitOverride()
    {
      var fallback = Skin(MenuVisualRole.Label, Color.red); var shared = Asset<MenuSkin>(); var style = Asset<MenuSkin>();
      shared.Colors = new[] { Rule(MenuVisualRole.Label, MenuThemeState.Disabled, Color.blue) };
      style.Colors = new[] { Rule(MenuVisualRole.Label, MenuThemeState.Any, Color.clear) };
      var theme = new MenuThemeContext(fallback, shared, style);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Disabled), Is.EqualTo(Color.clear));
      style.Colors[0].OverrideColor = false;
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Disabled), Is.EqualTo(Color.blue),
        "A missing Swatch is inheritance, while an explicit transparent color is a real local override.");
    }

    [TestCase(false, true, true, true, MenuThemeState.Disabled)]
    [TestCase(true, true, true, true, MenuThemeState.Hovered)]
    [TestCase(true, true, false, true, MenuThemeState.Selected)]
    [TestCase(true, false, false, true, MenuThemeState.Checked)]
    [TestCase(true, false, false, false, MenuThemeState.Normal)]
    public void InteractionStateHasDeterministicPriority(bool enabled, bool selected, bool hovered, bool check, MenuThemeState expected)
    {
      var state = State(null, new MenuThemeContext(), enabled, selected, hovered, check);
      Assert.That(state.ThemeState, Is.EqualTo(expected));
    }

    [Test]
    public void CheckIndicatorStaysIndependentWhileDisabledColorsBeatSelectionAndHover()
    {
      var fallback = Skin(MenuVisualRole.Label, Color.white); var style = Asset<MenuSkin>();
      style.Colors = new[]
      {
        Rule(MenuVisualRole.Disabled, MenuThemeState.Disabled, Color.gray),
        Rule(MenuVisualRole.Checked, MenuThemeState.Disabled, Color.magenta),
        Rule(MenuVisualRole.Checked, MenuThemeState.Checked, Color.green)
      };
      var widget = Widget(); var theme = new MenuThemeContext(fallback, overrides: style);
      widget.Bind(State(fallback, theme, enabled: false, selected: true, hovered: true, check: true));
      Assert.That(widget.Background.color, Is.EqualTo(Color.gray));
      Assert.That(widget.Check.enabled, Is.True);
      Assert.That(widget.Check.color, Is.EqualTo(Color.magenta));
      widget.Bind(State(fallback, theme, selected: true, hovered: true, check: true));
      Assert.That(widget.Check.enabled, Is.True);
      Assert.That(widget.Check.color, Is.EqualTo(Color.green), "Selection changes do not discard the independent checked marker.");
    }

    [Test]
    public void SemanticSwatchesFollowTheInjectedThemeWhileLocalColorsAndTokenOverridesWin()
    {
      var fallback = Skin(MenuVisualRole.Label, Color.red); var style = Asset<MenuSkin>();
      var authored = Token("menu.label", Color.blue);
      style.Colors = new[] { new MenuSkinColor { Role = MenuVisualRole.Label, State = MenuThemeState.Normal, Swatch = authored } };
      using var service = new FakeThemeService(Theme(Token("menu.label", Color.green)));
      var theme = new MenuThemeContext(fallback, overrides: style, themeService: service);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Normal), Is.EqualTo(Color.green));
      style.OverrideTheme = true;
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Normal), Is.EqualTo(Color.blue));
      style.Colors[0].OverrideColor = true; style.Colors[0].Color = Color.yellow;
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Normal), Is.EqualTo(Color.yellow));
    }

    [Test]
    public void TypographyAndSpacingInheritPerPropertyRatherThanReplacingTheWholeSkin()
    {
      var fallback = Asset<MenuSkin>(); var shared = Asset<MenuSkin>(); var style = Asset<MenuSkin>();
      fallback.LabelTypeset = Asset<Typeset>(); fallback.Spacing = Asset<Spacing>();
      shared.DescriptionTypeset = Asset<Typeset>(); shared.Spacing = Asset<Spacing>();
      var normal = Asset<Typeset>(); var disabled = Asset<Typeset>();
      style.LabelTypeset = normal;
      style.Typography = new[] { new MenuSkinTypography { State = MenuThemeState.Disabled, Typeset = disabled } };
      var theme = new MenuThemeContext(fallback, shared, style);
      Assert.That(theme.GetTypeset(false, MenuThemeState.Disabled), Is.SameAs(disabled));
      Assert.That(theme.GetTypeset(false, MenuThemeState.Hovered), Is.SameAs(normal));
      Assert.That(theme.GetTypeset(true, MenuThemeState.Disabled), Is.SameAs(shared.DescriptionTypeset));
      Assert.That(theme.GetSpacing(), Is.SameAs(shared.Spacing));
      style.Spacing = Asset<Spacing>();
      Assert.That(theme.GetSpacing(), Is.SameAs(style.Spacing));
      style.Spacing = null; shared.Spacing = null;
      Assert.That(theme.GetSpacing(), Is.SameAs(fallback.Spacing));
    }

    [Test]
    public void OpenWidgetFollowsLiveSemanticThemeChangesWithoutRebinding()
    {
      var fallback = Skin(MenuVisualRole.Normal, Color.red); var style = Asset<MenuSkin>();
      style.Colors = new[] { new MenuSkinColor { Role = MenuVisualRole.Normal, State = MenuThemeState.Normal, Swatch = Token("menu.surface", Color.black) } };
      using var service = new FakeThemeService(Theme(Token("menu.surface", Color.blue)));
      var widget = Widget(); var theme = new MenuThemeContext(fallback, overrides: style, themeService: service);
      widget.Bind(State(fallback, theme));
      Assert.That(widget.Background.color, Is.EqualTo(Color.blue));
      service.SetTheme(Theme(Token("menu.surface", Color.green)));
      Assert.That(widget.Background.color, Is.EqualTo(Color.green));
    }

    [Test]
    public void LabelCopiesPreserveFallbackSkinThemeLayersAndServiceForCustomWidgets()
    {
      var fallback = Asset<MenuSkin>(); var shared = Asset<MenuSkin>(); var style = Skin(MenuVisualRole.Label, Color.blue);
      using var service = new FakeThemeService(Theme());
      var state = State(fallback, new MenuThemeContext(fallback, shared, style, service), selected: true);
      var copy = state.WithLabel("Expanded branch");
      Assert.That(copy.Skin, Is.SameAs(fallback)); Assert.That(copy.ThemeService, Is.SameAs(service));
      Assert.That(copy.Theme.Skin, Is.SameAs(fallback)); Assert.That(copy.Theme.SharedOverrides, Is.SameAs(shared));
      Assert.That(copy.Theme.Overrides, Is.SameAs(style));
      Assert.That(copy.Theme.GetColor(MenuVisualRole.Label, copy.ThemeState), Is.EqualTo(Color.blue));
    }

    [Test]
    public void LegacySkinMethodsIgnoreQualifiedEntriesAndKeepCheckedMarkerFallback()
    {
      var fallback = Skin(MenuVisualRole.Label, Color.red);
      fallback.Colors = new[] { fallback.Colors[0], Rule(MenuVisualRole.Label, MenuThemeState.Disabled, Color.gray) };
      Assert.That(fallback.GetColor(MenuVisualRole.Label), Is.EqualTo(Color.red));
      Assert.That(new MenuThemeContext(fallback).GetColor(MenuVisualRole.Checked, MenuThemeState.Checked), Is.EqualTo(Color.red));
    }

    [Test]
    public void CustomVisualOptOutIgnoresEveryThemeLayerAndLiveThemeChanges()
    {
      var fallback = Skin(MenuVisualRole.Normal, Color.red); var style = Skin(MenuVisualRole.Normal, Color.blue);
      using var service = new FakeThemeService(Theme(Token("menu.surface", Color.green)));
      var widget = Widget(); widget.UseSkin = false; widget.Background.color = Color.magenta;
      widget.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style, themeService: service)));
      service.SetTheme(Theme(Token("menu.surface", Color.yellow)));
      Assert.That(widget.Background.color, Is.EqualTo(Color.magenta));
    }

    internal sealed class FakeThemeService : IThemeService, IDisposable
    {
      private readonly ReactiveProperty<ThemeAsset> current;
      public FakeThemeService(ThemeAsset initial) => current = new ReactiveProperty<ThemeAsset>(initial);
      public ThemeRegistry ThemeRegistry => null;
      public ThemeAsset DefaultTheme => null;
      public ThemeAsset Current => current.Value;
      public Observable<ThemeAsset> OnThemeUpdated => current;
      public Observable<(ThemeAsset Previous, ThemeAsset Current)> OnThemeChanged => current.Pairwise();
      public void SetTheme(string id, bool isSilent = false) => throw new NotSupportedException();
      public void SetTheme(ThemeAsset theme, bool isSilent = false) => current.Value = theme;
      public void Dispose() => current.Dispose();
    }
  }
}
