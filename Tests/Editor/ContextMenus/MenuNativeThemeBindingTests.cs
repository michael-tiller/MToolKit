using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Typeset;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuNativeThemeBindingTests
  {
    private readonly List<Object> objects = new();
    [TearDown] public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear();
    }
    private T Own<T>(T value) where T : Object { objects.Add(value); return value; }
    private T Asset<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
    private RectTransform Root()
    {
      var root = Own(new GameObject("Native theme test", typeof(RectTransform)));
      var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(600, 600); return rect;
    }
    private MenuWidgetInstance Item()
    {
      var item = MenuWidgetFactory.CreateItem(Root()); item.Rect.sizeDelta = new Vector2(240, 64); return item;
    }
    private Typeset Typography(float size, TMP_FontAsset font = null, string id = null)
    {
      var typeset = Asset<Typeset>(); typeset.name = id ?? "native.typeset." + size;
      typeof(Typeset).GetProperty(nameof(Typeset.FontSize)).SetValue(typeset, size);
      typeof(Typeset).GetProperty(nameof(Typeset.FontSizeMin)).SetValue(typeset, size);
      typeof(Typeset).GetProperty(nameof(Typeset.Font)).SetValue(typeset, font); return typeset;
    }
    private Spacing Space(int left, int right, int top, int bottom, float gap, string id = null)
    {
      var spacing = Asset<Spacing>(); spacing.name = id ?? "native.spacing." + left;
      typeof(Spacing).GetProperty(nameof(Spacing.Padding)).SetValue(spacing, new RectOffset(left, right, top, bottom));
      typeof(Spacing).GetProperty(nameof(Spacing.Gap)).SetValue(spacing, gap); return spacing;
    }
    private static MenuWidgetState State(MenuSkin fallback, MenuThemeContext theme,
      IReadOnlyList<MenuWidgetState> secondary = null)
      => new(new MenuAction("native", new MenuLabel("Native action")), MenuActionState.Available,
        label: "Native action", description: "A readable description", skin: fallback, secondaryStates: secondary,
        bindingHint: "Confirm", theme: theme);
    private static float Left(RectTransform target, RectTransform root)
    {
      var corners = new Vector3[4]; target.GetWorldCorners(corners);
      return root.InverseTransformPoint(corners[0]).x - root.rect.xMin;
    }
    private static float Gap(MenuActionWidget widget)
    {
      var icon = new Vector3[4]; var label = new Vector3[4];
      widget.Icon.rectTransform.GetWorldCorners(icon); widget.Label.rectTransform.GetWorldCorners(label);
      var root = (RectTransform)widget.transform;
      return root.InverseTransformPoint(label[0]).x - root.InverseTransformPoint(icon[2]).x;
    }
    private static HorizontalOrVerticalLayoutGroup ContentLayout(MenuWidgetInstance item)
    {
      var presets = item.GameObject.GetComponentsInChildren<SpacingPreset>(true);
      Assert.That(presets.Length, Is.GreaterThan(0), "Native content exposes an actual MTK spacing target.");
      Assert.That(presets[0].LayoutGroup, Is.InstanceOf<HorizontalOrVerticalLayoutGroup>());
      return (HorizontalOrVerticalLayoutGroup)presets[0].LayoutGroup;
    }
    private static void ForceContentLayout(RectTransform root)
    {
      // uGUI skips a subtree when this root has no ILayoutController: nested layout groups are independent roots.
      var layouts = root.GetComponentsInChildren<LayoutGroup>(true);
      for (int i = 0; i < layouts.Length; i++)
        if (layouts[i].isActiveAndEnabled) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layouts[i].transform);
    }
    private ThemeAsset Theme(Typeset typography = null, Spacing spacing = null)
    {
      var theme = Asset<ThemeAsset>();
      if (typography != null)
      {
        var registry = Asset<TypesetStyleRegistry>();
        typeof(TypesetStyleRegistry).GetProperty(nameof(TypesetStyleRegistry.Typesets)).SetValue(registry, new[] { typography });
        typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.TypesetStyleRegistry)).SetValue(theme, registry);
      }
      if (spacing != null)
      {
        var registry = Asset<SpacingRegistry>();
        typeof(SpacingRegistry).GetProperty(nameof(SpacingRegistry.Spacings)).SetValue(registry, new[] { spacing });
        typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.SpacingRegistry)).SetValue(theme, registry);
      }
      return theme;
    }

    [Test]
    public void NativeItemUsesExistingTypesetPresetForLiveSemanticTypography()
    {
      using var item = Item(); var widget = item.DefaultWidget; var fallback = Asset<MenuSkin>(); var style = Asset<MenuSkin>();
      style.LabelTypeset = Typography(17, widget.Label.font, "menu.label");
      using var service = new MenuThemeLayerTests.FakeThemeService(Theme(Typography(29, widget.Label.font, "menu.label")));
      item.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style, themeService: service)));
      Assert.That(widget.Label.fontSize, Is.EqualTo(29));
      Assert.That(widget.Label.GetComponent<TypesetPreset>(), Is.Not.Null, "The MTK typography/accessibility binding owns native text styling.");
      service.SetTheme(Theme(Typography(31, widget.Label.font, "menu.label")));
      Assert.That(widget.Label.fontSize, Is.EqualTo(31));
    }

    [Test]
    public void NativeCenterAndSecondaryApplyLabelAndDescriptionTypographyToAllDefaultTextSlots()
    {
      var root = Root();
      using var template = MenuWidgetFactory.CreateItem(root); template.Rect.sizeDelta = new Vector2(160, 36);
      var element = template.GameObject.AddComponent<LayoutElement>(); element.preferredHeight = 36;
      template.GameObject.SetActive(false);
      using var center = MenuWidgetFactory.CreateCenter(root, secondaryPrefab: template.GameObject);
      center.Rect.sizeDelta = new Vector2(220, 220); var widget = (MenuCenterWidget)center.DefaultWidget;
      var fallback = Asset<MenuSkin>(); var style = Asset<MenuSkin>(); style.OverrideTheme = true;
      style.LabelTypeset = Typography(27, widget.Label.font); style.DescriptionTypeset = Typography(17, widget.Description.font);
      var theme = new MenuThemeContext(fallback, overrides: style);
      center.Bind(State(fallback, theme, new[] { State(fallback, theme) }));
      Assert.That(widget.Label.fontSize, Is.EqualTo(27)); Assert.That(widget.Description.fontSize, Is.EqualTo(17));
      Assert.That(widget.BindingHint.fontSize, Is.EqualTo(17)); Assert.That(widget.DisabledReason.fontSize, Is.EqualTo(17));
      var secondary = widget.SecondaryContainer.GetComponentInChildren<MenuActionWidget>(true);
      Assert.That(secondary, Is.Not.Null); Assert.That(secondary.Label.fontSize, Is.EqualTo(27));
    }

    [Test]
    public void ClearingTypographyOverridesAndUnbindingRestoreTheNativeBaselineAcrossPooledReuse()
    {
      using var item = Item(); var widget = item.DefaultWidget; float baseline = widget.Label.fontSize;
      var fallback = Asset<MenuSkin>(); var style = Asset<MenuSkin>(); style.OverrideTheme = true;
      style.LabelTypeset = Typography(30, widget.Label.font);
      item.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style)));
      Assert.That(widget.Label.fontSize, Is.EqualTo(30));
      item.Bind(State(fallback, new MenuThemeContext(fallback)));
      Assert.That(widget.Label.fontSize, Is.EqualTo(baseline), "An absent inherited token must not retain a retired style's typography.");
      item.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style)));
      item.Unbind(); Assert.That(widget.Label.fontSize, Is.EqualTo(baseline));
    }

    [Test]
    public void NativeContentConsumesSpacingPaddingAndGapWithoutChangingTheItemHitRectangle()
    {
      using var item = Item(); var widget = item.DefaultWidget; var hitSize = item.Rect.sizeDelta;
      var fallback = Asset<MenuSkin>(); var style = Asset<MenuSkin>(); style.OverrideTheme = true;
      style.Spacing = Space(19, 11, 4, 6, 17);
      item.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style)));
      var layout = ContentLayout(item);
      Assert.That(layout.padding.left, Is.EqualTo(19)); Assert.That(layout.spacing, Is.EqualTo(17));
      ForceContentLayout(item.Rect);
      Assert.That(Left(widget.Icon.rectTransform, item.Rect), Is.EqualTo(19).Within(.01f));
      Assert.That(Gap(widget), Is.EqualTo(17).Within(.01f));
      Assert.That(item.Rect.sizeDelta, Is.EqualTo(hitSize), "Content spacing does not shrink the configured hit target.");
      var presets = item.GameObject.GetComponentsInChildren<SpacingPreset>(true);
      Assert.That(presets.Length, Is.GreaterThan(0));
      Assert.That(presets[0].LayoutGroup.padding, Is.Not.SameAs(style.Spacing.Padding));
    }

    [Test]
    public void NativeSpacingTracksLiveSemanticThemesAndRestoresItsAuthoredBaselineOnClearAndUnbind()
    {
      using var item = Item(); var widget = item.DefaultWidget;
      ForceContentLayout(item.Rect);
      float baselineLeft = Left(widget.Icon.rectTransform, item.Rect), baselineGap = Gap(widget);
      var fallback = Asset<MenuSkin>(); var style = Asset<MenuSkin>(); style.Spacing = Space(1, 1, 1, 1, 1, "menu.content");
      using var service = new MenuThemeLayerTests.FakeThemeService(Theme(spacing: Space(12, 8, 4, 4, 9, "menu.content")));
      item.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style, themeService: service)));
      var layout = ContentLayout(item);
      Assert.That(layout.padding.left, Is.EqualTo(12)); Assert.That(layout.spacing, Is.EqualTo(9));
      ForceContentLayout(item.Rect);
      Assert.That(Gap(widget), Is.EqualTo(9).Within(.01f));
      service.SetTheme(Theme(spacing: Space(27, 8, 4, 4, 21, "menu.content")));
      Assert.That(layout.padding.left, Is.EqualTo(27)); Assert.That(layout.spacing, Is.EqualTo(21));
      ForceContentLayout(item.Rect);
      Assert.That(Left(widget.Icon.rectTransform, item.Rect), Is.EqualTo(27).Within(.01f));
      Assert.That(Gap(widget), Is.EqualTo(21).Within(.01f));
      item.Bind(State(fallback, new MenuThemeContext(fallback)));
      ForceContentLayout(item.Rect);
      Assert.That(Left(widget.Icon.rectTransform, item.Rect), Is.EqualTo(baselineLeft).Within(.01f));
      Assert.That(Gap(widget), Is.EqualTo(baselineGap).Within(.01f));
      item.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style, themeService: service)));
      item.Unbind(); ForceContentLayout(item.Rect);
      Assert.That(Left(widget.Icon.rectTransform, item.Rect), Is.EqualTo(baselineLeft).Within(.01f));
      Assert.That(Gap(widget), Is.EqualTo(baselineGap).Within(.01f));
    }

    [Test]
    public void CustomPresetTargetsArePreservedAndTheirOriginalBindingIsRestored()
    {
      var root = Root(); var label = root.gameObject.AddComponent<TextMeshProUGUI>();
      var other = new GameObject("Custom typography target", typeof(RectTransform)); other.transform.SetParent(root, false);
      var target = other.AddComponent<TextMeshProUGUI>(); var preset = label.gameObject.AddComponent<TypesetPreset>();
      typeof(TypesetPreset).GetProperty(nameof(TypesetPreset.Text)).SetValue(preset, target);
      var baseline = Typography(22, target.font); preset.SetOverrideTheme(true); preset.SetStart(baseline);
      var widget = root.gameObject.AddComponent<MenuActionWidget>(); widget.Label = label;
      var fallback = Asset<MenuSkin>(); var style = Asset<MenuSkin>(); style.OverrideTheme = true;
      style.LabelTypeset = Typography(30, target.font);
      widget.Bind(State(fallback, new MenuThemeContext(fallback, overrides: style)));
      Assert.That(preset.Text, Is.SameAs(target)); Assert.That(target.fontSize, Is.EqualTo(30));
      widget.Unbind(); Assert.That(target.fontSize, Is.EqualTo(22));
      Assert.That(preset.Text, Is.SameAs(target));
    }

    [Test]
    public void OverrideOnlyProfilesCanThemeNativeDefaultsWithoutAFallbackSkin()
    {
      using var item = Item(); var style = Asset<MenuSkin>(); style.SetColor(MenuVisualRole.Label, Color.blue);
      item.Bind(State(null, new MenuThemeContext(overrides: style)));
      Assert.That(item.DefaultWidget.Label.color, Is.EqualTo(Color.blue));
      Assert.That(item.State.Skin, Is.Null, "The legacy fallback remains optional and is not replaced by a runtime clone.");
    }

    [Test]
    public void OptedOutCustomWidgetDoesNotApplyTypographyOrSpacingOverrides()
    {
      using var item = Item(); var widget = item.DefaultWidget; widget.UseSkin = false;
      float baseline = widget.Label.fontSize; ForceContentLayout(item.Rect);
      float left = Left(widget.Icon.rectTransform, item.Rect), gap = Gap(widget);
      var style = Asset<MenuSkin>(); style.OverrideTheme = true;
      style.LabelTypeset = Typography(30, widget.Label.font); style.Spacing = Space(29, 7, 7, 7, 23);
      item.Bind(State(null, new MenuThemeContext(overrides: style)));
      ForceContentLayout(item.Rect);
      Assert.That(widget.Label.fontSize, Is.EqualTo(baseline));
      Assert.That(Left(widget.Icon.rectTransform, item.Rect), Is.EqualTo(left).Within(.01f));
      Assert.That(Gap(widget), Is.EqualTo(gap).Within(.01f));
    }
  }
}
