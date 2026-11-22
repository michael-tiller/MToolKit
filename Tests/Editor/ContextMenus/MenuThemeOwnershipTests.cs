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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuThemeOwnershipTests
  {
    private readonly List<Object> owned = new List<Object>();
    private MenuSession session;
    private MenuPresentationContext context;
    private ListMenuPresenter presenter;
    private MenuThemeLayerTests.FakeThemeService service;
    private EventSystem previousEvents;

    [SetUp] public void SetUp() => previousEvents = EventSystem.current;
    [TearDown] public void Cleanup()
    {
      if (presenter != null) presenter.Unbind();
      session?.Dispose(); context?.Dispose(); service?.Dispose();
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      if (previousEvents != null) EventSystem.current = previousEvents;
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private T Asset<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
    private GameObject Root(string name, Transform parent = null)
    {
      var value = new GameObject(name, typeof(RectTransform));
      if (parent == null) Own(value); else value.transform.SetParent(parent, false);
      return value;
    }
    private Typeset Typography(float size, TMP_FontAsset font)
    {
      var token = Asset<Typeset>(); token.name = "ownership.type." + size;
      typeof(Typeset).GetProperty(nameof(Typeset.Font)).SetValue(token, font);
      typeof(Typeset).GetProperty(nameof(Typeset.FontSize)).SetValue(token, size);
      typeof(Typeset).GetProperty(nameof(Typeset.FontSizeMin)).SetValue(token, size);
      return token;
    }
    private Spacing Space(float gap)
    {
      var token = Asset<Spacing>(); token.name = "ownership.space." + gap;
      typeof(Spacing).GetProperty(nameof(Spacing.Padding)).SetValue(token, new RectOffset(8, 8, 4, 4));
      typeof(Spacing).GetProperty(nameof(Spacing.Gap)).SetValue(token, gap);
      return token;
    }
    private MenuWidgetState State(Typeset type = null, Spacing spacing = null)
    {
      var layer = Asset<MenuSkin>(); layer.OverrideTheme = true;
      layer.LabelTypeset = type; layer.Spacing = spacing;
      return new MenuWidgetState(new MenuAction("owned", new MenuLabel("Owned")), MenuActionState.Available,
        label: "Owned", theme: new MenuThemeContext(overrides: layer));
    }

    [TestCase("apply")]
    [TestCase("restore")]
    public void ReentrantTypographyBindingRetainsTheNewOwnerAndTheCompleteAuthoredBaseline(string phase)
    {
      using var item = MenuWidgetFactory.CreateItem((RectTransform)Root("Typography owner").transform);
      var widget = item.DefaultWidget;
      Object.DestroyImmediate(widget.Label.GetComponent<TypesetPreset>());
      var preset = widget.Label.gameObject.AddComponent<MenuRebindingTypesetPreset>();
      preset.ConfigureTarget(widget.Label);
      var original = Typography(17, widget.Label.font);
      var oldToken = Typography(23, widget.Label.font);
      var newToken = Typography(31, widget.Label.font);
      preset.SetOverrideTheme(false); preset.SetStart(original);
      var oldState = State(oldToken, Space(7));
      var newState = State(newToken, Space(19));
      var layout = widget.SpacingTargets[0].LayoutGroup as HorizontalOrVerticalLayoutGroup;
      Assert.That(layout, Is.Not.Null);
      var spacingPreset = widget.SpacingTargets[0];
      var originalSpacing = Space(3);
      spacingPreset.SetOverrideTheme(false); spacingPreset.SetStart(originalSpacing);
      if (phase == "restore") widget.Bind(oldState);
      bool rebound = false;
      preset.Applied = token =>
      {
        if (rebound || token != (phase == "restore" ? original : oldToken)) return;
        rebound = true; widget.Bind(newState);
      };
      try
      {
        if (phase == "apply") widget.Bind(oldState); else widget.Unbind();
        Assert.That(rebound, Is.True, "The supported preset Apply callback must actually replace the widget binding.");
        Assert.That(preset.ConfiguredAsset, Is.SameAs(newToken));
        Assert.That(preset.OverridesTheme, Is.True, "Retired setters cannot take ownership of the new binding.");
        Assert.That(widget.Label.fontSize, Is.EqualTo(31));
        Assert.That(layout.spacing, Is.EqualTo(19), "The old ApplyVisuals snapshot cannot overwrite the new owner's spacing.");
        preset.Applied = null;
        widget.Unbind();
        Assert.That(preset.ConfiguredAsset, Is.SameAs(original));
        Assert.That(preset.OverridesTheme, Is.False, "The new owner must inherit the authored baseline, not a predecessor's transient flag.");
        Assert.That(widget.Label.fontSize, Is.EqualTo(17));
        Assert.That(spacingPreset.ConfiguredAsset, Is.SameAs(originalSpacing), "A typography callback must transfer the authored spacing baseline too.");
        Assert.That(spacingPreset.OverridesTheme, Is.False);
        Assert.That(layout.spacing, Is.EqualTo(3));
      }
      finally { if (preset != null) preset.Applied = null; }
    }

    [TestCase("apply")]
    [TestCase("restore")]
    public void ReentrantSpacingBindingRetainsTheNewOwnerAndTheCompleteAuthoredBaseline(string phase)
    {
      using var item = MenuWidgetFactory.CreateItem((RectTransform)Root("Spacing owner").transform);
      var widget = item.DefaultWidget;
      var layout = widget.SpacingTargets[0].LayoutGroup as HorizontalOrVerticalLayoutGroup;
      Assert.That(layout, Is.Not.Null);
      Object.DestroyImmediate(widget.SpacingTargets[0]);
      var preset = layout.gameObject.AddComponent<MenuRebindingSpacingPreset>(); preset.ConfigureTarget(layout);
      widget.SpacingTargets = new SpacingPreset[] { preset };
      var original = Space(3); var oldToken = Space(7); var newToken = Space(19);
      preset.SetOverrideTheme(false); preset.SetStart(original);
      var oldState = State(spacing: oldToken); var newState = State(spacing: newToken);
      if (phase == "restore") widget.Bind(oldState);
      bool rebound = false;
      preset.Applied = token =>
      {
        if (rebound || phase == "restore" && token != original || phase == "apply" && token != original && token != oldToken) return;
        rebound = true; widget.Bind(newState);
      };
      try
      {
        if (phase == "apply") widget.Bind(oldState); else widget.Unbind();
        Assert.That(rebound, Is.True);
        Assert.That(preset.ConfiguredAsset, Is.SameAs(newToken));
        Assert.That(preset.OverridesTheme, Is.True);
        Assert.That(layout.spacing, Is.EqualTo(19));
        preset.Applied = null;
        widget.Unbind();
        Assert.That(preset.ConfiguredAsset, Is.SameAs(original));
        Assert.That(preset.OverridesTheme, Is.False, "The original override flag must survive reentrant handoff.");
        Assert.That(layout.spacing, Is.EqualTo(3));
      }
      finally { if (preset != null) preset.Applied = null; }
    }

    [Test]
    public void PresenterBackdropUsesItsOwnLiveThemeRoleAndKeepsOptOutTransparent()
    {
      var events = Root("Backdrop events").AddComponent<MenuPresenterTests.MenuTestEventSystem>(); EventSystem.current = events;
      var canvasRoot = Root("Backdrop canvas"); var canvas = canvasRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(1200, 900);
      presenter = Root("List presenter", canvas.transform).AddComponent<ListMenuPresenter>();
      var fallback = Asset<MenuSkin>(); fallback.SetColor(MenuVisualRole.Background, Color.red);
      var shared = Asset<MenuSkin>();
      var profile = Asset<MenuProfile>(); profile.Skin = fallback; profile.SharedThemeOverrides = shared;
      profile.ShowCenter = false; profile.Backdrop = true;
      service = new MenuThemeLayerTests.FakeThemeService(Asset<ThemeAsset>());
      session = new MenuSession(); session.Open(new[] {
        new MenuAction("leaf", new MenuLabel("Leaf"), new MenuCommandBehavior(_ => { }))
      }, new MenuContext());
      context = new MenuPresentationContext(canvas, Vector2.zero, themes: service, labelOverride: label => label.Fallback);
      presenter.Bind(session, profile, context);
      var backdrop = Array.Find(canvas.GetComponentsInChildren<Image>(true), image => image.name == "Menu outside click");
      Assert.That(backdrop, Is.Not.Null);
      Assert.That(backdrop.color, Is.EqualTo(new Color(0, 0, 0, .35f)), "The legacy opaque panel Background token must never fill the screen.");
      shared.SetColor(MenuVisualRole.Backdrop, Color.green);
      service.SetTheme(Asset<ThemeAsset>());
      Assert.That(backdrop.color, Is.EqualTo(Color.green), "A live theme refresh must update the actual presenter backdrop.");
      profile.Backdrop = false;
      service.SetTheme(Asset<ThemeAsset>());
      Assert.That(backdrop.color, Is.EqualTo(Color.clear));
    }
  }

  public sealed class MenuRebindingTypesetPreset : TypesetPreset
  {
    public Action<Typeset> Applied;
    protected override void Apply(Typeset token) { base.Apply(token); Applied?.Invoke(token); }
  }
  public sealed class MenuRebindingSpacingPreset : SpacingPreset
  {
    public Action<Spacing> Applied;
    protected override void Apply(Spacing token) { base.Apply(token); Applied?.Invoke(token); }
  }
}
