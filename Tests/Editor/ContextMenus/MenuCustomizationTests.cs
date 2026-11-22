using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme;
using MToolKit.Theme.Swatch;
using MToolKit.Theme.Typeset;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuCustomizationTests
  {
    private readonly List<Object> objects = new List<Object>();

    [TearDown]
    public void Cleanup()
    {
      for (int i = objects.Count - 1; i >= 0; i--)
        if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear();
    }

    private T Asset<T>() where T : ScriptableObject
    {
      var asset = ScriptableObject.CreateInstance<T>();
      objects.Add(asset);
      return asset;
    }

    private GameObject GameObject(string name = "Menu customization test")
    {
      var root = new GameObject(name, typeof(RectTransform));
      objects.Add(root);
      return root;
    }

    private Swatch Swatch(string id, Color color)
    {
      var token = Asset<Swatch>();
      token.name = id;
      typeof(Swatch).GetProperty(nameof(MToolKit.Theme.Swatch.Swatch.Color)).SetValue(token, color);
      return token;
    }

    private ThemeAsset Theme(params Swatch[] swatches)
    {
      var registry = Asset<SwatchRegistry>();
      typeof(SwatchRegistry).GetProperty(nameof(SwatchRegistry.Swatches)).SetValue(registry, swatches);
      var theme = Asset<ThemeAsset>();
      typeof(ThemeAsset).GetProperty(nameof(ThemeAsset.SwatchRegistry)).SetValue(theme, registry);
      return theme;
    }

    private static MenuWidgetState State(MenuSkin skin = null, IThemeService theme = null,
      Action activate = null, Action select = null, bool selected = false, bool enabled = true,
      bool isChecked = false, IReadOnlyList<MenuWidgetState> secondary = null)
    {
      var action = new MenuAction("test", new MenuLabel("Test"), description: new MenuLabel("Details"));
      return new MenuWidgetState(action, new MenuActionState(true, enabled, isChecked), selected,
        label: "Localized test", description: "Localized description", skin: skin,
        themeService: theme, activate: activate, select: select, bindingHint: "Confirm",
        secondaryStates: secondary);
    }

    [Test]
    public void DefaultProfile_HasValidAccessibleGeometryBeforePresenterAssignment()
    {
      var profile = Asset<MenuProfile>();
      Assert.That(profile.Validate(out var diagnostic, false), Is.True, diagnostic);
      Assert.That(profile.Navigation, Is.EqualTo(MenuNavigationPolicy.Inline));
      Assert.That(profile.RowHeight, Is.GreaterThanOrEqualTo(profile.MinimumTargetSize));
      Assert.That(profile.WidgetSize.x, Is.GreaterThanOrEqualTo(profile.MinimumTargetSize));
      Assert.That(profile.WidgetSize.y, Is.GreaterThanOrEqualTo(profile.MinimumTargetSize));
      Assert.That(profile.InputBindings, Is.Not.Null);
    }

    [Test]
    public void MissingPresenter_HasActionableConfigurationDiagnostic()
    {
      var profile = Asset<MenuProfile>();
      Assert.That(profile.Validate(out var diagnostic), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.PresenterPrefab)));
    }

    [Test]
    public void TypedAddressablePresenter_DoesNotRequireADirectPrefabBeforeLoading()
    {
      var profile = Asset<MenuProfile>();
      profile.PresenterReference = new ContextMenuPrefabReference("8dcb16e3691b4c0c9055be08a72ab094");
      Assert.That(profile.Validate(out var diagnostic), Is.True, diagnostic);
      profile.PresenterReference = new ContextMenuPrefabReference("not-a-guid");
      Assert.That(profile.Validate(out diagnostic), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.PresenterReference)));
    }

    [Test]
    public void FallbackProfile_CannotReferToItself()
    {
      var profile = Asset<MenuProfile>();
      profile.FallbackProfile = profile;
      Assert.That(profile.Validate(out var diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.FallbackProfile)));
    }

    [Test]
    public void SuppliedPresenterWithoutContract_IsRejectedEvenWhenPresenterIsOptional()
    {
      var profile = Asset<MenuProfile>();
      profile.PresenterPrefab = GameObject();
      Assert.That(profile.Validate(out var diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain("IContextMenuPresenter"));
    }

    [Test]
    public void CustomItemPrefab_UsesItsWidgetContractWithoutDefaultComponents()
    {
      var profile = Asset<MenuProfile>();
      profile.ItemPrefab = GameObject();
      profile.ItemPrefab.AddComponent<RecordingWidget>();
      Assert.That(profile.Validate(out var diagnostic, false), Is.True, diagnostic);
      Assert.That(profile.ItemPrefab.GetComponent<MenuActionWidget>(), Is.Null,
        "Validation must not insert or require a default widget implementation.");
    }

    [Test]
    public void MalformedItemPrefab_IsReportedBeforeInstantiation()
    {
      var profile = Asset<MenuProfile>();
      profile.ItemPrefab = GameObject();
      Assert.That(profile.Validate(out var diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.ItemPrefab)).And.Contain("IMenuWidget"));
    }

    [TestCase(-1)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidGeometry_IsDiagnosedWithoutSilentTargetShrinking(float value)
    {
      var profile = Asset<MenuProfile>();
      profile.RingWidth = value;
      Assert.That(profile.Validate(out var diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.RingWidth)));
    }

    [Test]
    public void TooSmallTargets_AreInvalidInsteadOfOverridingPlayerUiScale()
    {
      var profile = Asset<MenuProfile>();
      profile.WidgetSize = new Vector2(profile.MinimumTargetSize - 1, profile.MinimumTargetSize);
      Assert.That(profile.Validate(out var diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.WidgetSize)));
    }

    [Test]
    public void FullRingGap_DoesNotInheritInactivePartialChildArcLimit()
    {
      var profile = Asset<MenuProfile>();
      profile.Navigation = MenuNavigationPolicy.Replacement;
      profile.MaximumVisibleItems = 20;
      profile.AngularGap = 8;
      Assert.That(profile.Validate(out var diagnostic, false), Is.True, diagnostic);
      profile.Navigation = MenuNavigationPolicy.Concentric;
      Assert.That(profile.Validate(out diagnostic, false), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(MenuProfile.AngularGap)));
    }

    [Test]
    public void Skin_ResolvesMatchingSemanticIdFromCurrentTheme()
    {
      var skin = Asset<MenuSkin>();
      var authored = Swatch("Menu.Selected", Color.red);
      var replacement = Swatch("Menu.Selected", Color.green);
      skin.SetSwatch(MenuVisualRole.Selected, authored);
      using var service = new TestThemeService(Theme(replacement));
      Assert.That(skin.GetColor(MenuVisualRole.Selected, service), Is.EqualTo(Color.green));
      service.SetTheme(Theme());
      Assert.That(skin.GetColor(MenuVisualRole.Selected, service), Is.EqualTo(Color.red),
        "A missing semantic token falls back to the authored token, not a stale prior theme.");
    }

    [Test]
    public void DirectSkinOverride_WinsOverThemeChanges()
    {
      var skin = Asset<MenuSkin>();
      skin.SetSwatch(MenuVisualRole.Selected, Swatch("Menu.Selected", Color.red));
      skin.SetColor(MenuVisualRole.Selected, Color.yellow);
      using var service = new TestThemeService(Theme(Swatch("Menu.Selected", Color.green)));
      Assert.That(skin.GetColor(MenuVisualRole.Selected, service), Is.EqualTo(Color.yellow));
      service.SetTheme(Theme(Swatch("Menu.Selected", Color.blue)));
      Assert.That(skin.GetColor(MenuVisualRole.Selected, service), Is.EqualTo(Color.yellow));
    }

    [Test]
    public void SkinGlobalThemeOptOut_UsesAuthoredToken()
    {
      var skin = Asset<MenuSkin>();
      skin.SetSwatch(MenuVisualRole.Normal, Swatch("Menu.Normal", Color.red));
      skin.OverrideTheme = true;
      using var service = new TestThemeService(Theme(Swatch("Menu.Normal", Color.green)));
      Assert.That(skin.GetColor(MenuVisualRole.Normal, service), Is.EqualTo(Color.red));
    }

    [Test]
    public void MissingOptionalWidgets_StillBindUnbindAndRebindSafely()
    {
      var widget = GameObject().AddComponent<MenuActionWidget>();
      Assert.DoesNotThrow(() => widget.Bind(State()));
      Assert.DoesNotThrow(widget.Unbind);
      Assert.DoesNotThrow(() => widget.Bind(State(selected: true, enabled: false)));
      Assert.DoesNotThrow(widget.Unbind);
      var center = GameObject().AddComponent<MenuCenterWidget>();
      Assert.DoesNotThrow(() => center.Bind(State()));
      Assert.DoesNotThrow(center.Unbind);
    }

    [Test]
    public void RebindingButton_ReplacesOnlyOwnedCallbackAndUnbindRemovesIt()
    {
      var root = GameObject();
      var button = root.AddComponent<Button>();
      var widget = root.AddComponent<MenuActionWidget>();
      widget.Button = button;
      int external = 0, first = 0, second = 0;
      button.onClick.AddListener(() => external++);
      widget.Bind(State(activate: () => first++));
      button.onClick.Invoke();
      widget.Bind(State(activate: () => second++));
      button.onClick.Invoke();
      widget.Unbind();
      button.onClick.Invoke();
      Assert.That(first, Is.EqualTo(1));
      Assert.That(second, Is.EqualTo(1));
      Assert.That(external, Is.EqualTo(3), "Framework cleanup must preserve prefab-author callbacks.");
    }

    [Test]
    public void DisabledSnapshot_CannotInvokeWidgetActivation()
    {
      var root = GameObject();
      var widget = root.AddComponent<MenuActionWidget>();
      widget.Button = root.AddComponent<Button>();
      int calls = 0;
      widget.Bind(State(activate: () => calls++, enabled: false));
      Assert.That(widget.Button.interactable, Is.False);
      widget.Button.onClick.Invoke();
      Assert.That(calls, Is.Zero);
    }

    [Test]
    public void CheckedAndSelected_AreIndependentStructuredVisualState()
    {
      var root = GameObject();
      var widget = root.AddComponent<MenuActionWidget>();
      widget.Highlight = GameObject("Highlight").AddComponent<Image>();
      widget.Check = GameObject("Checked").AddComponent<Image>();
      widget.Bind(State(selected: false, isChecked: true));
      Assert.That(widget.Highlight.enabled, Is.False);
      Assert.That(widget.Check.enabled, Is.True);
      widget.Bind(State(selected: true, isChecked: false));
      Assert.That(widget.Highlight.enabled, Is.True);
      Assert.That(widget.Check.enabled, Is.False);
    }

    [Test]
    public void ThemeChangesWhileBound_UpdateAnyGraphicAndUnbindStopsSubscription()
    {
      var root = GameObject();
      var widget = root.AddComponent<MenuActionWidget>();
      widget.Background = root.AddComponent<Image>();
      var skin = Asset<MenuSkin>();
      skin.SetSwatch(MenuVisualRole.Selected, Swatch("Menu.Selected", Color.red));
      using var service = new TestThemeService(Theme(Swatch("Menu.Selected", Color.green)));
      widget.Bind(State(skin, service, selected: true));
      Assert.That(widget.Background.color, Is.EqualTo(Color.green));
      service.SetTheme(Theme(Swatch("Menu.Selected", Color.blue)));
      Assert.That(widget.Background.color, Is.EqualTo(Color.blue));
      widget.Unbind();
      service.SetTheme(Theme(Swatch("Menu.Selected", Color.yellow)));
      Assert.That(widget.Background.color, Is.EqualTo(Color.blue));
    }

    [Test]
    public void CustomWidgetVisualOptOut_PreservesAuthoredColors()
    {
      var root = GameObject();
      var widget = root.AddComponent<MenuActionWidget>();
      widget.Background = root.AddComponent<Image>();
      widget.Background.color = Color.magenta;
      widget.UseSkin = false;
      var skin = Asset<MenuSkin>();
      skin.SetColor(MenuVisualRole.Selected, Color.yellow);
      widget.Bind(State(skin, selected: true));
      Assert.That(widget.Background.color, Is.EqualTo(Color.magenta));
    }

    [Test]
    public void LocalTypographyOverride_ReusesTypesetPresetAndSurvivesThemeUpdates()
    {
      var root = GameObject();
      var label = root.AddComponent<TextMeshProUGUI>();
      var preset = root.AddComponent<TypesetPreset>();
      var widget = root.AddComponent<MenuActionWidget>();
      widget.Label = label;
      var typeset = Asset<Typeset>();
      typeof(Typeset).GetProperty(nameof(Typeset.FontSize)).SetValue(typeset, 30f);
      var skin = Asset<MenuSkin>();
      skin.OverrideTheme = true;
      skin.LabelTypeset = typeset;
      using var service = new TestThemeService(Theme());
      widget.Bind(State(skin, service));
      Assert.That(label.fontSize, Is.EqualTo(30));
      Assert.That(label.GetComponent<TypesetPreset>(), Is.SameAs(preset), "Existing typography/accessibility binding is reused.");
      service.SetTheme(Theme());
      Assert.That(label.fontSize, Is.EqualTo(30));
    }

    [Test]
    public void CustomCenter_ReceivesTheSameSelectionAndSecondaryState()
    {
      var custom = GameObject().AddComponent<RecordingWidget>();
      var secondary = State(activate: () => { });
      var selected = State(secondary: new[] { secondary });
      ((IMenuWidget)custom).Bind(selected);
      Assert.That(custom.State, Is.SameAs(selected));
      Assert.That(custom.State.SecondaryStates[0], Is.SameAs(secondary));
      custom.Unbind();
      Assert.That(custom.State, Is.Null);
    }

    public sealed class RecordingWidget : MonoBehaviour, IMenuWidget
    {
      public MenuWidgetState State { get; private set; }
      public void Bind(MenuWidgetState state) => State = state;
      public void Unbind() => State = null;
    }

    private sealed class TestThemeService : IThemeService, IDisposable
    {
      private readonly ReactiveProperty<ThemeAsset> changes;
      public TestThemeService(ThemeAsset initial) => changes = new ReactiveProperty<ThemeAsset>(initial);
      public ThemeRegistry ThemeRegistry => null;
      public ThemeAsset DefaultTheme => null;
      public ThemeAsset Current => changes.Value;
      public Observable<ThemeAsset> OnThemeUpdated => changes;
      public Observable<(ThemeAsset Previous, ThemeAsset Current)> OnThemeChanged => changes.Pairwise();
      public void SetTheme(string id, bool isSilent = false) => throw new NotSupportedException();
      public void SetTheme(ThemeAsset theme, bool isSilent = false) => changes.Value = theme;
      public void Dispose() => changes.Dispose();
    }
  }
}
