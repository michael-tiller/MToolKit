using System;
using System.Collections.Generic;
using System.Reflection;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ThemeSpacing = MToolKit.Theme.Spacing.Spacing;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuNavigationControlsTests
  {
    private readonly List<Object> owned = new();
    private readonly Vector3[] corners = new Vector3[4];
    private EventSystem previousEvents;
    private Canvas canvas;
    private Camera camera;
    private MenuSession session;
    private MenuPresentationContext context;
    private MenuProfile profile;
    private MenuPresenterBase presenter;
    private int commands;

    [SetUp]
    public void Setup()
    {
      commands = 0;
      previousEvents = EventSystem.current;
      var events = Own(new GameObject("Navigation control events",
        typeof(MenuPresenterTests.MenuTestEventSystem)));
      EventSystem.current = events.GetComponent<EventSystem>();
      var cameraObject = Own(new GameObject("Navigation control camera", typeof(Camera)));
      camera = cameraObject.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 900;
      camera.transform.position = new Vector3(0, 0, -10);
      var canvasObject = Own(new GameObject("Navigation control canvas", typeof(RectTransform), typeof(Canvas)));
      canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(2400, 1800);
      profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.Anchor = MenuAnchorPolicy.Centered;
      profile.Navigation = MenuNavigationPolicy.Replacement;
      profile.MaximumVisibleItems = 4;
      profile.InnerRadius = 100; profile.RingWidth = 60;
      profile.RingGap = 16; profile.ChildRingWidth = 64; profile.ChildArcDegrees = 120;
      profile.WidgetSize = new Vector2(96, 56);
      profile.ListWidth = 360; profile.RowHeight = 44;
      profile.ShowCenter = false; profile.Backdrop = false;
      session = new MenuSession();
    }

    [TearDown]
    public void Cleanup()
    {
      if (presenter != null) presenter.Unbind();
      session?.Dispose(); context?.Dispose();
      session = null; context = null;
      for (int i = owned.Count - 1; i >= 0; i--)
        if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      if (previousEvents != null) EventSystem.current = previousEvents;
    }

    [TestCase("Wheel")]
    [TestCase("Pie")]
    [TestCase("List")]
    public void One_page_presentations_do_not_display_unusable_page_controls(string kind)
    {
      Open(kind, Commands(3));
      Assert.That(session.PageCount, Is.EqualTo(1));
      AssertPaging(false);
      Assert.That(Find("Back").gameObject.activeInHierarchy, Is.True,
        "Existing callers retain their default Back control.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Radial_page_controls_sit_at_ten_and_two_with_their_nearest_edges_outside_every_ring(bool concentric)
    {
      const float gap = 19;
      var spacing = Own(ScriptableObject.CreateInstance<ThemeSpacing>());
      typeof(ThemeSpacing).GetProperty(nameof(ThemeSpacing.Gap)).SetValue(spacing, gap);
      var skin = Own(ScriptableObject.CreateInstance<MenuSkin>());
      skin.OverrideTheme = true; skin.Spacing = spacing; profile.Skin = skin;
      if (concentric) profile.Navigation = MenuNavigationPolicy.Concentric;
      var roots = concentric ? Parents(3, 15) : Commands(15);
      Open("Wheel", roots);
      if (concentric) session.EnterSubmenuAsync(roots[0].Id).GetAwaiter().GetResult();
      Layout();
      var radial = (RadialMenuPresenter)presenter;
      Assert.That(session.PageCount, Is.GreaterThan(1));
      AssertPaging(true);
      float extent = profile.InnerRadius + profile.RingWidth;
      if (concentric)
      {
        Assert.That(radial.OuterGeometry, Is.Not.Null);
        extent = radial.OuterGeometry.Segments[0].OuterRadius;
      }
      AssertRadialControl(Find("Previous"), new Vector2(-Mathf.Sqrt(3) * .5f, .5f), extent, gap);
      AssertRadialControl(Find("Next"), new Vector2(Mathf.Sqrt(3) * .5f, .5f), extent, gap);
    }

    [TestCase(3, 15, true, false)]
    [TestCase(11, 2, false, true)]
    public void Concentric_paging_visibility_follows_the_focused_ring(int parentCount, int childCount,
      bool childPages, bool parentPages)
    {
      profile.Navigation = MenuNavigationPolicy.Concentric;
      var roots = Parents(parentCount, childCount);
      Open("Wheel", roots);
      session.EnterSubmenuAsync(roots[0].Id).GetAwaiter().GetResult();
      Layout();
      var radial = (RadialMenuPresenter)presenter;
      Assert.That(radial.OuterGeometry, Is.Not.Null);
      Assert.That(radial.FocusedRing, Is.EqualTo(1));
      AssertPaging(childPages);
      radial.SwitchRing(); Layout();
      Assert.That(radial.FocusedRing, Is.Zero);
      AssertPaging(parentPages);
      radial.SwitchRing(); Layout();
      Assert.That(radial.FocusedRing, Is.EqualTo(1));
      AssertPaging(childPages);
    }

    [Test]
    public void ShowBackControl_defaults_to_true_for_existing_callers()
    {
      var property = BackSetting();
      Assert.That(property.PropertyType, Is.EqualTo(typeof(bool)));
      Assert.That(property.GetValue(profile), Is.True);
      Open("Wheel", Commands(2));
      Assert.That(Find("Back").gameObject.activeInHierarchy, Is.True);
    }

    [TestCase("Wheel")]
    [TestCase("Pie")]
    [TestCase("List")]
    public void Hiding_the_local_Back_control_preserves_recursive_semantic_Back_and_single_root_close(string kind)
    {
      BackSetting().SetValue(profile, false);
      var leaf = Command("leaf");
      var branch = new MenuAction("branch", new MenuLabel("Branch"), children: new[] { leaf });
      var root = new MenuAction("root", new MenuLabel("Root"), children: new[] { branch });
      Open(kind, new[] { root });
      int closed = 0; session.Closed += () => closed++;
      session.EnterSubmenuAsync(root.Id).GetAwaiter().GetResult();
      session.EnterSubmenuAsync(branch.Id).GetAwaiter().GetResult();
      Layout();
      Assert.That(session.Path.Count, Is.EqualTo(2));
      Assert.That(IsVisible("Back"), Is.False,
        "A gallery or host can supply the single global Back control instead.");
      presenter.Back(); Assert.That(session.Path.Count, Is.EqualTo(1));
      presenter.Back(); Assert.That(session.Path, Is.Empty);
      presenter.Back(); Assert.That(session.IsOpen, Is.False);
      presenter.Back();
      Assert.That(closed, Is.EqualTo(1));
      Assert.That(commands, Is.Zero);
    }

    [Test]
    public void Page_buttons_navigate_one_real_page_without_executing_a_gameplay_command()
    {
      Open("Wheel", Commands(15));
      Assert.That(session.PageCount, Is.GreaterThan(1));
      Assert.That(Find("Previous").interactable, Is.False);
      Find("Next").onClick.Invoke();
      Assert.That(session.PageIndex, Is.EqualTo(1));
      Find("Previous").onClick.Invoke();
      Assert.That(session.PageIndex, Is.Zero);
      Assert.That(commands, Is.Zero);
      Assert.That(session.IsOpen, Is.True);
    }

    [Test]
    public void Navigation_controls_request_glyphs_using_owned_bindings_and_fit_the_complete_measured_content()
    {
      using var prompts = new RecordingPrompts();
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      var map = asset.AddActionMap("Menu navigation");
      var back = map.AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
      var previous = map.AddAction("Previous", InputActionType.Button, "<Keyboard>/pageUp");
      var next = map.AddAction("Next", InputActionType.Button, "<Keyboard>/pageDown");
      var runtime = Own(Object.Instantiate(asset));
      var runtimePrevious = runtime.FindAction(previous.id);
      runtimePrevious.ApplyBindingOverride(0, "<Keyboard>/home");
      profile.InputBindings.Configure(back: Reference(back), pageBackward: Reference(previous), pageForward: Reference(next));
      Open("Wheel", Commands(15), prompts, action => runtime.FindAction(action.id));
      Assert.That(prompts.Resolved, Does.Contain(runtimePrevious));
      Assert.That(prompts.Resolved, Has.No.Member(previous));
      foreach (var style in prompts.Styles) Assert.That(style, Is.EqualTo(InputPromptStyle.Glyph));
      foreach (string name in new[] { "Back", "Previous", "Next" }) AssertMeasured(Find(name));
      prompts.Revision++;
      prompts.Notify(); Layout();
      foreach (string name in new[] { "Back", "Previous", "Next" }) AssertMeasured(Find(name));
      Assert.That(prompts.Resolved, Does.Contain(runtime.FindAction(next.id)));
      Assert.That(commands, Is.Zero);
    }

    [Test]
    public void Navigation_controls_and_prompt_writers_are_retired_before_rebinding()
    {
      using var prompts = new RecordingPrompts();
      var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
      var map = asset.AddActionMap("Navigation cleanup");
      var action = map.AddAction("Page", InputActionType.Button, "<Keyboard>/pageDown");
      profile.InputBindings.Configure(back: Reference(action), pageBackward: Reference(action), pageForward: Reference(action));
      Open("Wheel", Commands(15), prompts);
      var retired = Find("Next");
      Assert.That(prompts.ActiveBindings, Is.GreaterThan(0));
      presenter.Unbind();
      Assert.That(prompts.ActiveBindings, Is.Zero);
      Assert.That(retired == null, Is.True, "The old native navigation root is owned by its binding.");
      prompts.Notify();
      presenter.Bind(session, profile, context); Layout();
      Assert.That(Find("Next"), Is.Not.SameAs(retired));
      Find("Next").onClick.Invoke();
      Assert.That(session.PageIndex, Is.EqualTo(1));
      Assert.That(commands, Is.Zero);
      presenter.Unbind();
      Assert.That(prompts.ActiveBindings, Is.Zero);
    }

    [Test]
    public void Shared_authoring_back_setting_bakes_without_changing_navigation_bindings()
    {
      var commonType = EditorType("MenuCommonSettings");
      var recipeType = EditorType("ListMenuStyleRecipe");
      var authoring = EditorType("MenuStyleAuthoring");
      var common = Own(ScriptableObject.CreateInstance(commonType));
      var setting = commonType.GetField("ShowBackControl", BindingFlags.Instance | BindingFlags.Public);
      Assert.That(setting, Is.Not.Null, "Shared authoring must carry the explicit global-Back opt-out.");
      Assert.That(setting.GetValue(common), Is.True);
      setting.SetValue(common, false);
      var candidate = authoring.GetMethod("CreateCandidate", BindingFlags.Static | BindingFlags.NonPublic);
      Assert.That(candidate, Is.Not.Null);
      var output = Own((MenuProfile)candidate.Invoke(null, new[] { common, Activator.CreateInstance(recipeType) }));
      Assert.That(BackSetting().GetValue(output), Is.False);
      Assert.That(output.InputBindings, Is.Not.Null);
      Assert.That(output.Navigation, Is.EqualTo(MenuNavigationPolicy.Inline));
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private InputActionReference Reference(InputAction action) => Own(InputActionReference.Create(action));
    private MenuAction Command(string id)
      => new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(_ => commands++, MenuActionOutcome.Keep));
    private MenuAction[] Commands(int count, string prefix = "action")
    {
      var result = new MenuAction[count];
      for (int i = 0; i < count; i++) result[i] = Command(prefix + i);
      return result;
    }
    private MenuAction[] Parents(int count, int children)
    {
      var result = new MenuAction[count];
      for (int i = 0; i < count; i++)
        result[i] = new MenuAction("parent" + i, new MenuLabel("Parent " + i), children: Commands(children, "child" + i + "."));
      return result;
    }
    private void Open(string kind, IReadOnlyList<MenuAction> actions, IInputPromptService prompts = null,
      Func<InputAction, InputAction> resolve = null)
    {
      profile.DrawSegments = kind != "Pie";
      var root = new GameObject("Navigation " + kind, typeof(RectTransform));
      root.transform.SetParent(canvas.transform, false);
      presenter = kind == "List" ? root.AddComponent<ListMenuPresenter>() : root.AddComponent<RadialMenuPresenter>();
      session.Open(actions, new MenuContext());
      context = new MenuPresentationContext(canvas, RectTransformUtility.WorldToScreenPoint(camera, canvas.transform.position),
        prompts: prompts, labelOverride: label => label.Fallback, resolveInput: resolve);
      presenter.Bind(session, profile, context); Layout();
    }
    private void Layout()
    {
      Canvas.ForceUpdateCanvases();
      if (presenter != null)
        foreach (var button in presenter.GetComponentsInChildren<Button>(true))
          if (button.name == "Previous" || button.name == "Next" || button.name == "Back")
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)button.transform);
      Canvas.ForceUpdateCanvases();
    }
    private Button Find(string name)
    {
      foreach (var button in presenter.GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
      Assert.Fail("Missing navigation control " + name); return null;
    }
    private bool IsVisible(string name)
    {
      foreach (var button in presenter.GetComponentsInChildren<Button>(true))
        if (button.name == name) return button.gameObject.activeInHierarchy;
      return false;
    }
    private void AssertPaging(bool visible)
    {
      Assert.That(IsVisible("Previous"), Is.EqualTo(visible), "Previous must reflect actual focused-ring pagination.");
      Assert.That(IsVisible("Next"), Is.EqualTo(visible), "Next must reflect actual focused-ring pagination.");
    }
    private void AssertRadialControl(Button button, Vector2 direction, float radius, float gap)
    {
      var rect = BoundsInRoot((RectTransform)button.transform);
      Assert.That(Vector2.Dot(rect.center.normalized, direction), Is.GreaterThan(.995f),
        button.name + " belongs at ten/two o'clock rather than under the menu.");
      var closest = new Vector2(Mathf.Clamp(0, rect.xMin, rect.xMax), Mathf.Clamp(0, rect.yMin, rect.yMax));
      Assert.That(closest.magnitude, Is.GreaterThanOrEqualTo(radius + gap - .1f),
        "The nearest control edge, not its center, must clear the maximum visible ring plus MTK spacing.");
      Assert.That(rect.yMin, Is.GreaterThan(0), "A radial pager must not form a bottom footer.");
    }
    private Rect BoundsInRoot(RectTransform rect)
    {
      rect.GetWorldCorners(corners);
      Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
      Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
      for (int i = 0; i < corners.Length; i++)
      {
        Vector2 point = presenter.Root.InverseTransformPoint(corners[i]);
        minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
      }
      return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }
    private void AssertMeasured(Button button)
    {
      var widget = button.GetComponent<MenuActionWidget>();
      Assert.That(widget, Is.Not.Null);
      Assert.That(widget.BindingHint, Is.Not.Null);
      Assert.That(widget.BindingHint.text, Is.Not.Empty);
      var rect = (RectTransform)button.transform;
      Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(LayoutUtility.GetPreferredWidth(rect) - .1f),
        "A complete label and binding legend must fit without forcing the horizontal layout to shrink either.");
      Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(profile.MinimumTargetSize));
      Assert.That(widget.Label.overflowMode, Is.Not.EqualTo(TextOverflowModes.Ellipsis));
      Assert.That(widget.BindingHint.overflowMode, Is.Not.EqualTo(TextOverflowModes.Ellipsis));
    }
    private static PropertyInfo BackSetting()
    {
      var property = typeof(MenuProfile).GetProperty("ShowBackControl", BindingFlags.Instance | BindingFlags.Public);
      Assert.That(property, Is.Not.Null, "MenuProfile needs the authorable ShowBackControl opt-out, default true.");
      return property;
    }
    private static Type EditorType(string name)
    {
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        var type = assembly.GetType("MToolKit.Editor.ContextMenus." + name);
        if (type != null) return type;
      }
      Assert.Fail("Missing existing menu authoring type " + name); return null;
    }

    private sealed class RecordingPrompts : IInputPromptService, IDisposable
    {
      private readonly Subject<Unit> changed = new();
      public readonly List<InputAction> Resolved = new();
      public readonly List<InputPromptStyle> Styles = new();
      public int ActiveBindings, Revision;
      public Observable<Unit> Changed => changed;
      public InputPromptContext Context => default;
      public void Notify() => changed.OnNext(Unit.Default);
      public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
      {
        Resolved.Add(action); Styles.Add(style);
        // Glyph providers retain readable text when an icon is unavailable. Exercise that widest valid legend too.
        return "[" + new string('W', 10 + Revision * 4) + "]";
      }
      public string ResolveReference(InputActionReference action, InputPromptStyle style = InputPromptStyle.Auto,
        InputPromptContext? context = null) => Resolve(action?.action, style, context);
      public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null) => path;
      public string Localize(string key, string fallback, params object[] arguments) => fallback;
      public string Compose(string key, string fallback, params InputPromptArgument[] arguments) => fallback;
      public IDisposable Bind(TMP_Text text, Func<string> render)
      {
        text.text = render(); ActiveBindings++;
        var subscription = changed.Subscribe(_ => { if (text != null) text.text = render(); });
        bool disposed = false;
        return Disposable.Create(() =>
        {
          if (disposed) return;
          disposed = true; subscription.Dispose(); ActiveBindings--;
        });
      }
      public void Dispose() => changed.Dispose();
    }
  }
}
