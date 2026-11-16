using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Theme;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Swatch;
using MToolKit.Theme.Typeset;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public abstract class MenuStyleSetFixture
  {
    private readonly List<Object> objects = new();
    [TearDown] public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear();
    }
    protected T Own<T>(T value) where T : Object { objects.Add(value); return value; }
    protected T Asset<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
    protected Swatch Token(Color color, string id = null)
    {
      var token = Asset<Swatch>(); token.name = id ?? "style.color." + color;
      typeof(Swatch).GetProperty(nameof(Swatch.Color)).SetValue(token, color); return token;
    }
    protected Typeset Type(float size, TMP_FontAsset font = null, string id = null)
    {
      var token = Asset<Typeset>(); token.name = id ?? "style.type." + size;
      typeof(Typeset).GetProperty(nameof(Typeset.FontSize)).SetValue(token, size);
      typeof(Typeset).GetProperty(nameof(Typeset.FontSizeMin)).SetValue(token, size);
      typeof(Typeset).GetProperty(nameof(Typeset.Font)).SetValue(token, font); return token;
    }
    protected Spacing Space(int left, float gap, string id = null)
    {
      var token = Asset<Spacing>(); token.name = id ?? "style.space." + left;
      typeof(Spacing).GetProperty(nameof(Spacing.Padding)).SetValue(token, new RectOffset(left, 4, 3, 3));
      typeof(Spacing).GetProperty(nameof(Spacing.Gap)).SetValue(token, gap); return token;
    }
    protected ThemeAsset Theme(Swatch[] colors = null, Typeset[] types = null, Spacing[] spaces = null)
    {
      var result = Asset<ThemeAsset>();
      var colorsRegistry = Asset<SwatchRegistry>();
      typeof(SwatchRegistry).GetProperty(nameof(SwatchRegistry.Swatches)).SetValue(colorsRegistry, colors ?? Array.Empty<Swatch>());
      typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.SwatchRegistry)).SetValue(result, colorsRegistry);
      var typesRegistry = Asset<TypesetStyleRegistry>();
      typeof(TypesetStyleRegistry).GetProperty(nameof(TypesetStyleRegistry.Typesets)).SetValue(typesRegistry, types ?? Array.Empty<Typeset>());
      typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.TypesetStyleRegistry)).SetValue(result, typesRegistry);
      var spacingRegistry = Asset<SpacingRegistry>();
      typeof(SpacingRegistry).GetProperty(nameof(SpacingRegistry.Spacings)).SetValue(spacingRegistry, spaces ?? Array.Empty<Spacing>());
      typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.SpacingRegistry)).SetValue(result, spacingRegistry);
      return result;
    }
    protected MenuSkin Layer(params (MenuThemeState state, MenuStyleSet style)[] slots)
    {
      var layer = Asset<MenuSkin>();
      layer.StateStyles = slots.Select(slot => new MenuSkinStyle { State = slot.state, Style = slot.style }).ToArray();
      return layer;
    }
  }

  public sealed class MenuStyleSetTests : MenuStyleSetFixture
  {
    [Test]
    public void SixExactSlotsReuseOneAssetAcrossProfilesWithoutCloning()
    {
      var shared = Asset<MenuStyleSet>(); shared.Foreground = Token(Color.green);
      var first = Asset<MenuSkin>(); var second = Asset<MenuSkin>();
      foreach (MenuThemeState state in Enum.GetValues(typeof(MenuThemeState)))
      { first.SetStyle(state, shared); second.SetStyle(state, shared); }
      {
        foreach (MenuThemeState state in Enum.GetValues(typeof(MenuThemeState)))
        {
          Assert.That(first.GetStyle(state), Is.SameAs(shared), state.ToString());
          Assert.That(second.GetStyle(state), Is.SameAs(shared), state.ToString());
        }
        Assert.That(first.StateStyles.Length, Is.EqualTo(6));
        Assert.That(new MenuThemeContext(first).GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.green));
      }
      Assert.That(shared, Is.InstanceOf<ThemeStyleSet>());
    }

    [Test]
    public void EveryVisualRoleUsesItsOwnTypedChannel()
    {
      var set = Asset<MenuStyleSet>();
      var channels = new Dictionary<MenuVisualRole, Swatch>
      {
        [MenuVisualRole.Background] = set.PanelBackground = Token(new Color(.01f, 0, 0)),
        [MenuVisualRole.Normal] = set.Normal = Token(new Color(.02f, 0, 0)),
        [MenuVisualRole.Hovered] = set.Hovered = Token(new Color(.03f, 0, 0)),
        [MenuVisualRole.Selected] = set.Selected = Token(new Color(.04f, 0, 0)),
        [MenuVisualRole.Disabled] = set.Disabled = Token(new Color(.05f, 0, 0)),
        [MenuVisualRole.InnerBorder] = set.InnerBorder = Token(new Color(.06f, 0, 0)),
        [MenuVisualRole.OuterBorder] = set.OuterBorder = Token(new Color(.07f, 0, 0)),
        [MenuVisualRole.Separator] = set.Separator = Token(new Color(.08f, 0, 0)),
        [MenuVisualRole.Label] = set.Foreground = Token(new Color(.09f, 0, 0)),
        [MenuVisualRole.Description] = set.Description = Token(new Color(.10f, 0, 0)),
        [MenuVisualRole.Icon] = set.Icon = Token(new Color(.11f, 0, 0)),
        [MenuVisualRole.Center] = set.CenterBackground = Token(new Color(.12f, 0, 0)),
        [MenuVisualRole.Auxiliary] = set.AuxiliaryBackground = Token(new Color(.13f, 0, 0)),
        [MenuVisualRole.Checked] = set.Checked = Token(new Color(.14f, 0, 0)),
        [MenuVisualRole.Backdrop] = set.Backdrop = Token(new Color(.15f, 0, 0))
      };
      var theme = new MenuThemeContext(Layer((MenuThemeState.Any, set)));
      { foreach (var channel in channels) Assert.That(theme.GetColor(channel.Key), Is.EqualTo(channel.Value.Color), channel.Key.ToString()); }
    }

    [Test]
    public void CommonChannelsFallBackOnlyWithinTheirRelatedRoles()
    {
      var set = Asset<MenuStyleSet>(); set.Background = Token(Color.red); set.Border = Token(Color.blue); set.Foreground = Token(Color.green);
      var fallback = Asset<MenuSkin>();
      fallback.SetColor(MenuVisualRole.Background, Color.black); fallback.SetColor(MenuVisualRole.Center, Color.cyan);
      fallback.SetColor(MenuVisualRole.Auxiliary, Color.yellow); fallback.SetColor(MenuVisualRole.Backdrop, Color.clear);
      var theme = new MenuThemeContext(fallback, overrides: Layer((MenuThemeState.Any, set)));
      {
        foreach (var role in new[] { MenuVisualRole.Normal, MenuVisualRole.Hovered, MenuVisualRole.Selected, MenuVisualRole.Disabled })
          Assert.That(theme.GetColor(role), Is.EqualTo(Color.red), role.ToString());
        Assert.That(theme.GetColor(MenuVisualRole.InnerBorder), Is.EqualTo(Color.blue));
        Assert.That(theme.GetColor(MenuVisualRole.OuterBorder), Is.EqualTo(Color.blue));
        Assert.That(theme.GetColor(MenuVisualRole.Checked), Is.EqualTo(Color.green));
        Assert.That(theme.GetColor(MenuVisualRole.Background), Is.EqualTo(Color.black));
        Assert.That(theme.GetColor(MenuVisualRole.Center), Is.EqualTo(Color.cyan));
        Assert.That(theme.GetColor(MenuVisualRole.Auxiliary), Is.EqualTo(Color.yellow));
        Assert.That(theme.GetColor(MenuVisualRole.Backdrop), Is.EqualTo(Color.clear));
      }
      fallback.SetColor(MenuVisualRole.Checked, Color.blue);
      Assert.That(new MenuThemeContext(fallback, sharedOverrides: Layer((MenuThemeState.Any, set)))
          .GetColor(MenuVisualRole.Checked), Is.EqualTo(Color.blue),
        "A shared foreground-only style must inherit an explicit checked marker from the fallback skin.");
    }

    [Test]
    public void ExactStateStylesAreSparseAndInheritAnyPerProperty()
    {
      var any = Asset<MenuStyleSet>(); any.Foreground = Token(Color.red); any.Icon = Token(Color.blue);
      var selected = Asset<MenuStyleSet>(); selected.Foreground = Token(Color.green);
      var layer = Layer((MenuThemeState.Any, any), (MenuThemeState.Selected, selected));
      var theme = new MenuThemeContext(layer);
      {
        Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.green));
        Assert.That(theme.GetColor(MenuVisualRole.Icon, MenuThemeState.Selected), Is.EqualTo(Color.blue));
        Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Hovered), Is.EqualTo(Color.red));
        Assert.That(layer.GetStyle(MenuThemeState.Hovered), Is.Null, "GetStyle is the exact authoring slot, not inherited resolution.");
      }
    }

    [Test]
    public void PerPropertyLayerPrecedenceRetainsLocalAnyBeforeSharedExact()
    {
      var fallbackSet = Asset<MenuStyleSet>(); fallbackSet.Icon = Token(Color.gray);
      var commonSet = Asset<MenuStyleSet>(); commonSet.Foreground = Token(Color.blue); commonSet.Description = Token(Color.yellow);
      var localSet = Asset<MenuStyleSet>(); localSet.Foreground = Token(Color.green);
      var theme = new MenuThemeContext(Layer((MenuThemeState.Any, fallbackSet)),
        Layer((MenuThemeState.Selected, commonSet)), Layer((MenuThemeState.Any, localSet)));
      {
        Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.green));
        Assert.That(theme.GetColor(MenuVisualRole.Description, MenuThemeState.Selected), Is.EqualTo(Color.yellow));
        Assert.That(theme.GetColor(MenuVisualRole.Icon, MenuThemeState.Selected), Is.EqualTo(Color.gray));
      }
    }

    [Test]
    public void ExactStyleBeatsDirectAnyButExactDirectPropertyWinsOverExactStyle()
    {
      var set = Asset<MenuStyleSet>(); set.Foreground = Token(Color.green); set.Icon = Token(Color.cyan);
      var layer = Layer((MenuThemeState.Selected, set)); layer.SetColor(MenuVisualRole.Label, Color.red);
      var theme = new MenuThemeContext(layer);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.green));
      layer.SetColor(MenuVisualRole.Label, Color.blue, MenuThemeState.Selected);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.blue));
      Assert.That(theme.GetColor(MenuVisualRole.Icon, MenuThemeState.Selected), Is.EqualTo(Color.cyan));
    }

    [Test]
    public void ExplicitTransparentDirectOverrideWinsAndNullTokenInheritsTheSet()
    {
      var set = Asset<MenuStyleSet>(); set.Foreground = Token(Color.green);
      var layer = Layer((MenuThemeState.Disabled, set)); layer.SetColor(MenuVisualRole.Label, Color.clear, MenuThemeState.Disabled);
      var theme = new MenuThemeContext(layer);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Disabled), Is.EqualTo(Color.clear));
      layer.Colors[0].OverrideColor = false;
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Disabled), Is.EqualTo(Color.green));
    }

    [Test]
    public void TypographyUsesExactStyleThenAnyWhileExactDirectTypographyWins()
    {
      var any = Asset<MenuStyleSet>(); any.LabelTypeset = Type(18); any.DescriptionTypeset = Type(16);
      var selected = Asset<MenuStyleSet>(); selected.LabelTypeset = Type(30);
      var layer = Layer((MenuThemeState.Any, any), (MenuThemeState.Selected, selected)); layer.LabelTypeset = Type(20);
      var theme = new MenuThemeContext(layer);
      {
        Assert.That(theme.GetTypeset(false, MenuThemeState.Selected), Is.SameAs(selected.LabelTypeset));
        Assert.That(theme.GetTypeset(true, MenuThemeState.Selected), Is.SameAs(any.DescriptionTypeset));
        Assert.That(theme.GetTypeset(false), Is.SameAs(layer.LabelTypeset));
      }
      var direct = Type(32); layer.Typography = new[] { new MenuSkinTypography { State = MenuThemeState.Selected, Typeset = direct } };
      Assert.That(theme.GetTypeset(false, MenuThemeState.Selected), Is.SameAs(direct));
    }

    [Test]
    public void StateSpacingSelectsTheWholeSetAndPreservesLegacyDefaultMethods()
    {
      var any = Asset<MenuStyleSet>(); any.Spacing = Space(7, 3);
      var selected = Asset<MenuStyleSet>(); selected.Spacing = Space(23, 17);
      var layer = Layer((MenuThemeState.Any, any), (MenuThemeState.Selected, selected)); var legacy = layer.Spacing = Space(9, 5);
      var theme = new MenuThemeContext(layer);
      {
        Assert.That(theme.GetSpacing(MenuThemeState.Selected), Is.SameAs(selected.Spacing));
        Assert.That(layer.GetSpacing(MenuThemeState.Selected), Is.SameAs(selected.Spacing));
        Assert.That(theme.GetSpacing(), Is.SameAs(legacy)); Assert.That(layer.GetSpacing(), Is.SameAs(legacy));
      }
      layer.Spacing = null;
      Assert.That(theme.GetSpacing(), Is.SameAs(any.Spacing));
      selected.Spacing = null;
      Assert.That(theme.GetSpacing(MenuThemeState.Selected), Is.SameAs(any.Spacing));
    }

    [Test]
    public void StyleTokensResolveBySemanticIdAndMissingIdsKeepTheAuthoredToken()
    {
      var set = Asset<MenuStyleSet>(); set.Foreground = Token(Color.red, "style.label"); set.Icon = Token(Color.magenta, "style.missing");
      set.LabelTypeset = Type(20, id: "style.type"); set.Spacing = Space(3, 2, "style.space");
      var resolvedType = Type(31, id: "style.type"); var resolvedSpace = Space(21, 13, "style.space");
      using var service = new MenuThemeLayerTests.FakeThemeService(Theme(new[] { Token(Color.green, "style.label") }, new[] { resolvedType }, new[] { resolvedSpace }));
      var theme = new MenuThemeContext(Layer((MenuThemeState.Any, set)), themeService: service);
      {
        Assert.That(theme.GetColor(MenuVisualRole.Label), Is.EqualTo(Color.green));
        Assert.That(theme.GetColor(MenuVisualRole.Icon), Is.EqualTo(Color.magenta));
        Assert.That(theme.GetTypeset(), Is.SameAs(resolvedType)); Assert.That(theme.GetSpacing(), Is.SameAs(resolvedSpace));
      }
    }

    [Test]
    public void StyleOverrideThemeLocksOnlyStyleTokensWhileLayerOverrideLocksEverything()
    {
      var set = Asset<MenuStyleSet>(); set.Foreground = Token(Color.red, "style.label"); set.OverrideTheme = true;
      set.LabelTypeset = Type(20, id: "style.type"); set.Spacing = Space(3, 2, "style.space");
      var layer = Layer((MenuThemeState.Any, set)); layer.SetSwatch(MenuVisualRole.Icon, Token(Color.blue, "style.icon"));
      using var service = new MenuThemeLayerTests.FakeThemeService(Theme(new[] { Token(Color.green, "style.label"), Token(Color.yellow, "style.icon") },
        new[] { Type(31, id: "style.type") }, new[] { Space(21, 13, "style.space") }));
      var theme = new MenuThemeContext(layer, themeService: service);
      {
        Assert.That(theme.GetColor(MenuVisualRole.Label), Is.EqualTo(Color.red));
        Assert.That(theme.GetColor(MenuVisualRole.Icon), Is.EqualTo(Color.yellow));
        Assert.That(theme.GetTypeset(), Is.SameAs(set.LabelTypeset)); Assert.That(theme.GetSpacing(), Is.SameAs(set.Spacing));
      }
      set.OverrideTheme = false; layer.OverrideTheme = true;
      Assert.That(theme.GetColor(MenuVisualRole.Label), Is.EqualTo(Color.red));
      Assert.That(theme.GetColor(MenuVisualRole.Icon), Is.EqualTo(Color.blue));
    }

    [Test]
    public void NullAndDestroyedStateSetsInheritWithoutReplacingOtherChannels()
    {
      var set = Asset<MenuStyleSet>(); set.Foreground = Token(Color.green);
      var destroyed = Asset<MenuStyleSet>(); destroyed.Foreground = Token(Color.red);
      var layer = Layer((MenuThemeState.Any, set), (MenuThemeState.Selected, destroyed));
      Object.DestroyImmediate(destroyed);
      var theme = new MenuThemeContext(layer);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.green));
      layer.StateStyles = new[] { (MenuSkinStyle)null, new MenuSkinStyle { State = MenuThemeState.Any, Style = set },
        new MenuSkinStyle { State = MenuThemeState.Selected, Style = null } };
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Selected), Is.EqualTo(Color.green));
    }

    [Test]
    public void DuplicateExactSlotsUseTheLastEntryAndClearingDoesNotMutateSharedAssets()
    {
      var first = Asset<MenuStyleSet>(); first.Foreground = Token(Color.red);
      var last = Asset<MenuStyleSet>(); last.Foreground = Token(Color.green);
      var layer = Layer((MenuThemeState.Selected, first), (MenuThemeState.Selected, last));
      var other = Layer((MenuThemeState.Selected, last));
      Assert.That(layer.GetStyle(MenuThemeState.Selected), Is.SameAs(last));
      layer.SetStyle(MenuThemeState.Selected, null);
      {
        Assert.That(layer.GetStyle(MenuThemeState.Selected), Is.Null);
        Assert.That(other.GetStyle(MenuThemeState.Selected), Is.SameAs(last));
        Assert.That(last.Foreground.Color, Is.EqualTo(Color.green));
      }
    }

    [Test]
    public void LegacyDirectColorsTypographyAndSpacingRemainUnchangedWithNoAssignedSets()
    {
      var layer = Asset<MenuSkin>(); layer.SetColor(MenuVisualRole.Label, Color.clear);
      layer.LabelTypeset = Type(24); layer.Spacing = Space(11, 7); var theme = new MenuThemeContext(layer);
      Assert.That(theme.GetColor(MenuVisualRole.Label, MenuThemeState.Disabled), Is.EqualTo(Color.clear));
      Assert.That(theme.GetTypeset(), Is.SameAs(layer.LabelTypeset)); Assert.That(theme.GetSpacing(), Is.SameAs(layer.Spacing));
    }
  }
}
