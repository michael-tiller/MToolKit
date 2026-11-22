using System.Linq;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme.Typeset;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuStyleSetNativeTests : MenuStyleSetFixture
  {
    private RectTransform Root()
    {
      var root = Own(new GameObject("Style set native test", typeof(RectTransform)));
      var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(600, 600); return rect;
    }
    private MenuWidgetInstance Item()
    { var result = MenuWidgetFactory.CreateItem(Root()); result.Rect.sizeDelta = new Vector2(240, 64); return result; }
    private static MenuWidgetState State(MenuThemeContext theme, bool selected = false, bool hovered = false,
      bool enabled = true, bool check = false)
      => new(new MenuAction("set-native", new MenuLabel("Reusable style")), new MenuActionState(true, enabled, check),
        selected: selected, label: "Reusable style", description: "Readable description", isHovered: hovered, theme: theme);
    private static HorizontalOrVerticalLayoutGroup Layout(MenuWidgetInstance item)
      => (HorizontalOrVerticalLayoutGroup)item.DefaultWidget.SpacingTargets[0].LayoutGroup;
    private static void ForceContentLayout(MenuWidgetInstance item)
      => LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Layout(item).transform);
    private static float Left(RectTransform child, RectTransform parent)
    {
      var corners = new Vector3[4]; child.GetWorldCorners(corners);
      return parent.InverseTransformPoint(corners[0]).x - parent.rect.xMin;
    }

    [Test]
    public void WholeStateSetChangesNativeTypographySpacingAndColorWithoutChangingHitSize()
    {
      using var item = Item(); var widget = item.DefaultWidget; var hitSize = item.Rect.sizeDelta;
      var normal = Asset<MenuStyleSet>(); normal.OverrideTheme = true;
      normal.Background = Token(Color.blue); normal.LabelTypeset = Type(20, widget.Label.font); normal.Spacing = Space(9, 5);
      var selected = Asset<MenuStyleSet>(); selected.OverrideTheme = true;
      selected.Background = Token(Color.green); selected.LabelTypeset = Type(28, widget.Label.font); selected.Spacing = Space(23, 17);
      var layer = Layer((MenuThemeState.Normal, normal), (MenuThemeState.Selected, selected)); var theme = new MenuThemeContext(layer);
      item.Bind(State(theme));
      Assert.That(widget.Background.color, Is.EqualTo(Color.blue)); Assert.That(widget.Label.fontSize, Is.EqualTo(20));
      Assert.That(Layout(item).padding.left, Is.EqualTo(9)); Assert.That(Layout(item).spacing, Is.EqualTo(5));
      item.Bind(State(theme, selected: true));
      Assert.That(widget.Background.color, Is.EqualTo(Color.green)); Assert.That(widget.Label.fontSize, Is.EqualTo(28));
      Assert.That(Layout(item).padding.left, Is.EqualTo(23)); Assert.That(Layout(item).spacing, Is.EqualTo(17));
      ForceContentLayout(item);
      Assert.That(Left(widget.Icon.rectTransform, item.Rect), Is.EqualTo(23).Within(.01f));
      Assert.That(item.Rect.sizeDelta, Is.EqualTo(hitSize), "Visual content style does not resize the action hit rectangle.");
      Assert.That(Layout(item).padding, Is.Not.SameAs(selected.Spacing.Padding));
    }

    [Test]
    public void OpenSelectedWidgetTracksLiveStyleTokensWithoutAnotherBind()
    {
      using var item = Item(); var widget = item.DefaultWidget;
      var selected = Asset<MenuStyleSet>(); selected.Foreground = Token(Color.red, "set.label");
      selected.LabelTypeset = Type(20, widget.Label.font, "set.type"); selected.Spacing = Space(1, 1, "set.space");
      using var service = new MenuThemeLayerTests.FakeThemeService(Theme(new[] { Token(Color.green, "set.label") },
        new[] { Type(26, widget.Label.font, "set.type") }, new[] { Space(13, 7, "set.space") }));
      item.Bind(State(new MenuThemeContext(Layer((MenuThemeState.Selected, selected)), themeService: service), selected: true));
      Assert.That(widget.Label.color, Is.EqualTo(Color.green)); Assert.That(widget.Label.fontSize, Is.EqualTo(26));
      Assert.That(Layout(item).padding.left, Is.EqualTo(13));
      service.SetTheme(Theme(new[] { Token(Color.cyan, "set.label") },
        new[] { Type(30, widget.Label.font, "set.type") }, new[] { Space(21, 15, "set.space") }));
      Assert.That(widget.Label.color, Is.EqualTo(Color.cyan)); Assert.That(widget.Label.fontSize, Is.EqualTo(30));
      Assert.That(Layout(item).padding.left, Is.EqualTo(21)); Assert.That(Layout(item).spacing, Is.EqualTo(15));
    }

    [Test]
    public void ClearingAndUnbindingStateSetsRestoreTypographyAndSpacingAcrossPooledReuse()
    {
      using var item = Item(); var widget = item.DefaultWidget;
      float baselineType = widget.Label.fontSize; int baselineLeft = Layout(item).padding.left; float baselineGap = Layout(item).spacing;
      var set = Asset<MenuStyleSet>(); set.OverrideTheme = true;
      set.LabelTypeset = Type(30, widget.Label.font); set.Spacing = Space(27, 19);
      var layer = Layer((MenuThemeState.Selected, set)); var theme = new MenuThemeContext(layer);
      item.Bind(State(theme, selected: true));
      Assert.That(widget.Label.fontSize, Is.EqualTo(30)); Assert.That(Layout(item).spacing, Is.EqualTo(19));
      layer.StateStyles = System.Array.Empty<MenuSkinStyle>();
      item.Bind(State(theme, selected: true));
      Assert.That(widget.Label.fontSize, Is.EqualTo(baselineType));
      Assert.That(Layout(item).padding.left, Is.EqualTo(baselineLeft)); Assert.That(Layout(item).spacing, Is.EqualTo(baselineGap));
      layer.StateStyles = new[] { new MenuSkinStyle { State = MenuThemeState.Selected, Style = set } };
      item.Bind(State(theme, selected: true)); item.Unbind();
      Assert.That(widget.Label.fontSize, Is.EqualTo(baselineType));
      Assert.That(Layout(item).padding.left, Is.EqualTo(baselineLeft)); Assert.That(Layout(item).spacing, Is.EqualTo(baselineGap));
    }

    [Test]
    public void OptedOutWidgetIgnoresAllSetStatesAndLiveThemeChanges()
    {
      using var item = Item(); var widget = item.DefaultWidget; widget.UseSkin = false;
      float baselineType = widget.Label.fontSize; int baselineLeft = Layout(item).padding.left; float baselineGap = Layout(item).spacing;
      widget.Background.color = Color.magenta;
      var set = Asset<MenuStyleSet>(); set.Background = Token(Color.green, "set.surface");
      set.LabelTypeset = Type(30, widget.Label.font); set.Spacing = Space(27, 19);
      var layer = Layer((MenuThemeState.Any, set), (MenuThemeState.Selected, set));
      using var service = new MenuThemeLayerTests.FakeThemeService(Theme(new[] { Token(Color.red, "set.surface") }));
      var theme = new MenuThemeContext(layer, themeService: service);
      item.Bind(State(theme)); item.Bind(State(theme, selected: true));
      service.SetTheme(Theme(new[] { Token(Color.yellow, "set.surface") }));
      Assert.That(widget.Background.color, Is.EqualTo(Color.magenta)); Assert.That(widget.Label.fontSize, Is.EqualTo(baselineType));
      Assert.That(Layout(item).padding.left, Is.EqualTo(baselineLeft)); Assert.That(Layout(item).spacing, Is.EqualTo(baselineGap));
    }

    [Test]
    public void DisabledSetWinsHoverAndSelectionWhileCheckedFeedbackRemainsIndependent()
    {
      using var item = Item(); var widget = item.DefaultWidget;
      var disabled = Asset<MenuStyleSet>(); disabled.Background = Token(Color.gray); disabled.Checked = Token(Color.magenta);
      var hovered = Asset<MenuStyleSet>(); hovered.Background = Token(Color.cyan);
      var check = Asset<MenuStyleSet>(); check.Checked = Token(Color.green);
      var theme = new MenuThemeContext(Layer((MenuThemeState.Disabled, disabled), (MenuThemeState.Hovered, hovered), (MenuThemeState.Checked, check)));
      item.Bind(State(theme, selected: true, hovered: true, enabled: false, check: true));
      Assert.That(widget.Background.color, Is.EqualTo(Color.gray)); Assert.That(widget.Check.enabled, Is.True);
      Assert.That(widget.Check.color, Is.EqualTo(Color.magenta));
      item.Bind(State(theme, selected: true, hovered: true, check: true));
      Assert.That(widget.Background.color, Is.EqualTo(Color.cyan)); Assert.That(widget.Check.enabled, Is.True);
      Assert.That(widget.Check.color, Is.EqualTo(Color.green));
    }

    [Test]
    public void TypedSetPreservesAuthoredCustomPresetTargetAndItsOriginalBinding()
    {
      var root = Root(); var label = root.gameObject.AddComponent<TextMeshProUGUI>();
      var child = new GameObject("Authored custom text target", typeof(RectTransform)); child.transform.SetParent(root, false);
      var target = child.AddComponent<TextMeshProUGUI>(); var preset = label.gameObject.AddComponent<TypesetPreset>();
      typeof(TypesetPreset).GetProperty(nameof(TypesetPreset.Text)).SetValue(preset, target);
      var original = Type(22, target.font); preset.SetBinding(original, true);
      var widget = root.gameObject.AddComponent<MenuActionWidget>(); widget.Label = label;
      var set = Asset<MenuStyleSet>(); set.OverrideTheme = true; set.LabelTypeset = Type(30, target.font);
      widget.Bind(State(new MenuThemeContext(Layer((MenuThemeState.Selected, set))), selected: true));
      Assert.That(preset.Text, Is.SameAs(target)); Assert.That(target.fontSize, Is.EqualTo(30));
      widget.Unbind();
      Assert.That(preset.Text, Is.SameAs(target)); Assert.That(preset.ConfiguredAsset, Is.SameAs(original));
      Assert.That(preset.OverridesTheme, Is.True); Assert.That(target.fontSize, Is.EqualTo(22));
    }
  }
}
