using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Rendering;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.Input.Prompts;
using NUnit.Framework;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MToolKit.ContextMenus.Input;
using System.Reflection;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuPresenterTests
  {
    private readonly List<Object> cleanup = new List<Object>();
    private MenuSession session;
    private MenuPresentationContext context;
    private EventSystem previousEventSystem;
    private bool capturedEventSystem;

    [TearDown]
    public void Cleanup()
    {
      for (int i = cleanup.Count - 1; i >= 0; i--)
        if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
      cleanup.Clear();
      context?.Dispose(); context = null;
      session?.Dispose(); session = null;
      if (capturedEventSystem && previousEventSystem != null) EventSystem.current = previousEventSystem;
      capturedEventSystem = false; previousEventSystem = null;
    }

    private T Presenter<T>(IReadOnlyList<MenuAction> actions, out MenuProfile profile, MenuContext openContext = null) where T : Component
    {
      var canvasObject = new GameObject("Menu presenter test canvas", typeof(RectTransform), typeof(Canvas));
      cleanup.Add(canvasObject);
      var canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.WorldSpace;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(1200, 900);
      var root = new GameObject("Presenter", typeof(RectTransform));
      root.transform.SetParent(canvas.transform, false);
      profile = ScriptableObject.CreateInstance<MenuProfile>();
      cleanup.Add(profile);
      session = new MenuSession();
      session.Open(actions, openContext ?? new MenuContext());
      context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback);
      var component = root.AddComponent<T>();
      ((IContextMenuPresenter)component).Bind(session, profile, context);
      return component;
    }

    private static MenuAction Command(string id, Action callback = null)
      => new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(_ => callback?.Invoke(), MenuActionOutcome.Keep));

    [Test]
    public void ListDefault_ExpandsInlineAndBackCollapsesWithoutExecutingParent()
    {
      int calls = 0;
      var parent = new MenuAction("parent", new MenuLabel("Parent"), children: new[] { Command("child") },
        primary: new MenuCommandBehavior(_ => calls++));
      var presenter = Presenter<ListMenuPresenter>(new[] { parent, Command("other") }, out _);
      presenter.Confirm();
      Assert.That(session.Rows.Count, Is.EqualTo(3));
      Assert.That(session.Path.Count, Is.Zero);
      Assert.That(presenter.VisibleWidgetCount, Is.EqualTo(3));
      Assert.That(calls, Is.Zero);
      presenter.Back();
      Assert.That(session.Rows.Count, Is.EqualTo(2));
      Assert.That(session.IsOpen, Is.True);
    }

    [Test]
    public void ListKeyboardNavigation_ExecutesOnlySelectedLeaf()
    {
      int first = 0, second = 0;
      var presenter = Presenter<ListMenuPresenter>(new[] { Command("first", () => first++), Command("second", () => second++) }, out _);
      presenter.Navigate(Vector2.down, false);
      presenter.Confirm();
      Assert.That(first, Is.Zero);
      Assert.That(second, Is.EqualTo(1));
    }

    [Test]
    public void ListPagination_UsesSessionStateAndBackDoesNotPopFrameworkNavigation()
    {
      var actions = new List<MenuAction>();
      for (int i = 0; i < 20; i++) actions.Add(Command("action" + i));
      var presenter = Presenter<ListMenuPresenter>(actions, out _);
      Assert.That(session.PageCount, Is.GreaterThan(1));
      presenter.Page(1);
      Assert.That(session.PageIndex, Is.EqualTo(1));
      presenter.Back();
      Assert.That(session.IsOpen, Is.False);
    }

    [Test]
    public void ReplacementRadial_TraversesMoreThanTwoHierarchyLevels()
    {
      var third = new MenuAction("third", new MenuLabel("Third"), children: new[] { Command("leaf") });
      var second = new MenuAction("second", new MenuLabel("Second"), children: new[] { third });
      var first = new MenuAction("first", new MenuLabel("First"), children: new[] { second });
      var presenter = Presenter<RadialMenuPresenter>(new[] { first }, out var profile);
      profile.Navigation = MenuNavigationPolicy.Replacement;
      presenter.Confirm(); presenter.Confirm(); presenter.Confirm();
      Assert.That(session.Path.Count, Is.EqualTo(3));
      Assert.That(session.Selected.Id.Value, Is.EqualTo("leaf"));
      presenter.Back(); presenter.Back(); presenter.Back();
      Assert.That(session.Path.Count, Is.Zero);
      Assert.That(session.IsOpen, Is.True);
    }

    [Test]
    public void ConcentricRadial_ShowsParentAndPartialChildGeometry()
    {
      var parent = new MenuAction("parent", new MenuLabel("Parent"), children: new[] { Command("child1"), Command("child2") });
      var presenter = Presenter<RadialMenuPresenter>(new[] { parent }, out var profile);
      profile.Navigation = MenuNavigationPolicy.Concentric;
      presenter.Confirm();
      Assert.That(presenter.InnerGeometry, Is.Not.Null);
      Assert.That(presenter.OuterGeometry, Is.Not.Null);
      Assert.That(presenter.OuterGeometry.Segments.Count, Is.EqualTo(2));
      float sweep = 0;
      foreach (var segment in presenter.OuterGeometry.Segments) sweep += Math.Abs(segment.SweepAngle);
      Assert.That(sweep, Is.LessThan(360));
      Assert.That(session.ParentSelected, Is.SameAs(parent));
    }

    [Test]
    public void RadialDeadZone_PreventsHoldReleaseExecutingPreviousSelection()
    {
      int calls = 0;
      var presenter = Presenter<RadialMenuPresenter>(new[] { Command("one", () => calls++) }, out _);
      presenter.BeginHold();
      presenter.Navigate(Vector2.right, true);
      presenter.Navigate(Vector2.zero, true);
      presenter.EndHold();
      Assert.That(calls, Is.Zero);
    }

    [Test]
    public void SelectionChanges_ReuseCachedRadialGeometry()
    {
      var presenter = Presenter<RadialMenuPresenter>(new[] { Command("one"), Command("two") }, out _);
      var geometry = presenter.InnerGeometry;
      presenter.Navigate(Vector2.right, true);
      presenter.Navigate(Vector2.left, true);
      Assert.That(presenter.InnerGeometry, Is.SameAs(geometry));
    }

    [Test]
    public void RadialWholeVisibleWedge_IsARaycastTargetBeyondItsContentWidget()
    {
      var presenter = Presenter<RadialMenuPresenter>(new[] { Command("one"), Command("two") }, out _);
      var wedges = presenter.GetComponentsInChildren<NativeRadialGraphic>();
      Assert.That(wedges.Length, Is.EqualTo(2));
      Assert.That(wedges[0].raycastTarget, Is.True);
      Assert.That(wedges[0].GetComponent<MenuSectorPointer>(), Is.Not.Null);
    }

    [Test]
    public void HoldWithoutDirectionalSelection_CancelsRatherThanInvokingDefaultLeaf()
    {
      int calls = 0;
      var presenter = Presenter<RadialMenuPresenter>(new[] { Command("one", () => calls++) }, out _);
      presenter.BeginHold();
      presenter.EndHold();
      Assert.That(calls, Is.Zero);
      Assert.That(session.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator WorkerCancellation_DoesNotTouchUnityOrUnbindANewerSession()
    {
      using var cancellation = new CancellationTokenSource();
      var presenter = Presenter<ListMenuPresenter>(new[] { Command("old") }, out var profile,
        new MenuContext(cancellationToken: cancellation.Token));
      Assert.DoesNotThrow(() => Task.Run(() => cancellation.Cancel()).GetAwaiter().GetResult(),
        "External cancellation must not access Unity objects on its worker thread.");
      using var replacement = new MenuSession();
      replacement.Open(new[] { Command("replacement") }, new MenuContext());
      presenter.gameObject.SetActive(true);
      presenter.Bind(replacement, profile, context);
      yield return null;
      yield return null;
      Assert.That(replacement.IsOpen, Is.True);
      Assert.That(presenter.VisibleWidgetCount, Is.EqualTo(1));
    }

    [Test]
    public void CustomItemAndCenter_AreBoundThroughContracts()
    {
      var presenter = Presenter<ListMenuPresenter>(new[] { Command("one") }, out var profile);
      var customItem = new GameObject("Custom menu item", typeof(RectTransform));
      cleanup.Add(customItem);
      customItem.AddComponent<RecordingMenuWidget>();
      profile.ItemPrefab = customItem;
      presenter.Unbind();
      presenter.Bind(session, profile, context);
      var widgets = presenter.GetComponentsInChildren<RecordingMenuWidget>(true);
      Assert.That(widgets.Length, Is.GreaterThanOrEqualTo(1));
      Assert.That(widgets[0].State.Action.Id.Value, Is.EqualTo("one"));
      Assert.That(widgets[0].GetComponent<MenuActionWidget>(), Is.Null);
    }

    [Test]
    public void PresenterHint_UsesOwnedRuntimeBindingIdentity()
    {
      var presenter = Presenter<ListMenuPresenter>(new[] { Command("one") }, out var profile);
      var authoredAsset = ScriptableObject.CreateInstance<InputActionAsset>();
      cleanup.Add(authoredAsset);
      var authoredMap = new InputActionMap("Menu");
      authoredAsset.AddActionMap(authoredMap);
      var authored = authoredMap.AddAction("authored submit", InputActionType.Button, "<Keyboard>/enter");
      using var runtime = authored.Clone();
      runtime.ApplyBindingOverride(0, "<Keyboard>/space");
      var reference = InputActionReference.Create(authored);
      cleanup.Add(reference);
      profile.InputBindings.Configure(submit: reference);
      var prompts = new RecordingPromptService();
      var canvas = presenter.GetComponentInParent<Canvas>();
      presenter.Unbind();
      context.Dispose();
      context = new MenuPresentationContext(canvas, Vector2.zero, prompts: prompts,
        resolveInput: action => ReferenceEquals(action, authored) ? runtime : action);
      presenter.Bind(session, profile, context);
      Assert.That(prompts.LastAction, Is.SameAs(runtime));
      Assert.That(prompts.LastAction.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SemanticPointerClickOnOverlappingSecondaryExecutesOnlyAuxiliary(bool primaryEnabled)
    {
      int primary = 0, auxiliary = 0;
      var events = CreateEventSystem();
      var owner = new MenuAction("owner", new MenuLabel("Owner"),
        new MenuCommandBehavior(_ => primary++, MenuActionOutcome.Keep),
        secondaryActions: new[] { Command("auxiliary", () => auxiliary++) },
        state: _ => new MenuActionState(true, primaryEnabled));
      var presenter = Presenter<RadialMenuPresenter>(new[] { owner }, out var profile);
      var control = ConfigureOverlappingCenter(presenter, profile);
      using var input = new PointerInputScope();
      profile.InputBindings.Configure(point: input.Reference(input.Point), click: input.Reference(input.Click), routePointerClick: true);
      presenter.Unbind(); presenter.Bind(session, profile, context);
      control = presenter.GetComponentInChildren<AuxiliaryCenterWidget>().Control;
      ConfigureKnownControlRaycast(presenter.GetComponentInParent<Canvas>(), control.gameObject);
      Canvas.ForceUpdateCanvases();
      var screen = RectTransformUtility.WorldToScreenPoint(presenter.GetComponentInParent<Canvas>().worldCamera, control.transform.position);
      var local = (Vector2)presenter.Root.InverseTransformPoint(control.transform.position);
      Assert.That(presenter.InnerGeometry.HitTest(local), Is.EqualTo(0), "The auxiliary intentionally overlaps the visible primary wedge.");
      var hits = new List<RaycastResult>();
      events.RaycastAll(new PointerEventData(events) { position = screen }, hits);
      TestContext.Out.WriteLine("EditMode auxiliary Graphic depth=" + control.GetComponent<Image>().depth +
        ", cull=" + control.GetComponent<Image>().canvasRenderer.cull + "; rendered overlap proof belongs to the PlayMode gallery.");
      Assert.That(hits.Count, Is.GreaterThan(0), "The ownership unit fixture must publish its known control hit.");
      TestContext.Out.WriteLine("Ownership unit UI endpoint: " + string.Join(", ", hits.ConvertAll(hit => hit.gameObject.name)));
      Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(control.gameObject),
        "The known overlapping auxiliary must own the routed UI endpoint.");
      using var router = new MenuInputRouter(presenter, profile.InputBindings);
      input.Send(screen, false);
      input.Send(screen, true); // Router Point/Confirm runs during the Input System callback, before uGUI processes release.
      input.Send(screen, false);
      ExecuteEvents.Execute(control.gameObject, new PointerEventData(events) { position = screen, button = PointerEventData.InputButton.Left },
        ExecuteEvents.pointerClickHandler);
      Assert.That(primary, Is.Zero, "A click owned by the center must never execute the wedge behind it.");
      Assert.That(auxiliary, Is.EqualTo(1));
    }

    [Test]
    public void SemanticFocusedSecondarySubmitHasOneOwner()
    {
      int primary = 0, auxiliary = 0;
      var events = CreateEventSystem();
      var owner = new MenuAction("owner", new MenuLabel("Owner"), new MenuCommandBehavior(_ => primary++, MenuActionOutcome.Keep),
        secondaryActions: new[] { Command("auxiliary", () => auxiliary++) });
      var presenter = Presenter<RadialMenuPresenter>(new[] { owner }, out var profile);
      var control = ConfigureOverlappingCenter(presenter, profile);
      events.SetSelectedGameObject(control.gameObject);
      presenter.Confirm();
      // InputSystemUIInputModule.ProcessNavigation returns before Submit/Cancel when this flag is false.
      if (events.sendNavigationEvents) ExecuteEvents.Execute(control.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
      Assert.That(primary, Is.Zero);
      Assert.That(auxiliary, Is.EqualTo(1));
    }

    [Test]
    public void ButtonOnlyRadialAcceptsVisibleItemCornerOutsideTheAnnulus()
    {
      int first = 0, second = 0;
      var events = CreateEventSystem();
      var presenter = Presenter<RadialMenuPresenter>(new[] { Command("first", () => first++), Command("second", () => second++) }, out var profile);
      ConfigureOverlappingCenter(presenter, profile);
      profile.DrawSegments = false;
      profile.ShowCenter = false;
      using var input = new PointerInputScope();
      profile.InputBindings.Configure(point: input.Reference(input.Point), click: input.Reference(input.Click), routePointerClick: true);
      presenter.Unbind(); presenter.Bind(session, profile, context);
      MenuActionWidget secondWidget = null;
      foreach (var widget in presenter.GetComponentsInChildren<MenuActionWidget>())
        if (widget.Label != null && widget.Label.text == "second") { secondWidget = widget; break; }
      Assert.That(secondWidget, Is.Not.Null);
      ConfigureKnownControlRaycast(presenter.GetComponentInParent<Canvas>(), secondWidget.gameObject);
      var rect = (RectTransform)secondWidget.transform;
      var world = rect.TransformPoint(new Vector3(rect.rect.xMin + 1, rect.rect.yMax - 1));
      var screen = RectTransformUtility.WorldToScreenPoint(presenter.GetComponentInParent<Canvas>().worldCamera, world);
      Assert.That(presenter.InnerGeometry.HitTest((Vector2)presenter.Root.InverseTransformPoint(world)), Is.EqualTo(-1),
        "The visible button corner intentionally lies outside annular selection.");
      Assert.That(presenter.Root.rect.Contains((Vector2)presenter.Root.InverseTransformPoint(world)), Is.True,
        "The unchanged menu bounds must contain the whole button target without reducing its dimensions.");
      Canvas.ForceUpdateCanvases();
      var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = screen }, hits);
      Assert.That(hits.Count, Is.GreaterThan(0));
      Assert.That(hits[0].gameObject.transform.IsChildOf(secondWidget.transform), Is.True,
        "The actual visible item must own the corner hit before routing input.");
      using var router = new MenuInputRouter(presenter, profile.InputBindings);
      input.Send(screen, false); input.Send(screen, true); input.Send(screen, false);
      ExecuteEvents.Execute(secondWidget.gameObject, new PointerEventData(events) { position = screen, button = PointerEventData.InputButton.Left },
        ExecuteEvents.pointerClickHandler);
      Assert.That(session.IsOpen, Is.True, "An item corner must not fall through to outside dismissal.");
      Assert.That(first, Is.Zero);
      Assert.That(second, Is.EqualTo(1));
      Assert.That(session.Selected.Id.Value, Is.EqualTo("second"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeCancelIsConsumedOnlyByTheProfileOwningNativeNavigation(bool native)
    {
      var events = CreateEventSystem();
      var parent = new MenuAction("parent", new MenuLabel("Parent"), children: new[] { Command("child") });
      var presenter = Presenter<ListMenuPresenter>(new[] { parent }, out var profile);
      profile.InputBindings.Configure(useNativeUguiNavigation: native);
      presenter.Unbind(); presenter.Bind(session, profile, context);
      session.EnterSubmenuAsync(parent.Id).GetAwaiter().GetResult();
      var data = new BaseEventData(events);
      presenter.OnCancel(data);
      Assert.That(session.Path.Count, Is.EqualTo(native ? 0 : 1));
      Assert.That(data.used, Is.EqualTo(native));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NavigationOwnershipRestoresTheOriginalFlagAcrossOverlappingHosts(bool original)
    {
      var events = CreateEventSystem();
      events.sendNavigationEvents = original;
      var first = Presenter<ListMenuPresenter>(new[] { Command("first") }, out _);
      Assert.That(events.sendNavigationEvents, Is.False);
      var secondRoot = new GameObject("Native second host", typeof(RectTransform));
      secondRoot.transform.SetParent(first.Root.parent, false);
      var second = secondRoot.AddComponent<ListMenuPresenter>();
      var secondProfile = ScriptableObject.CreateInstance<MenuProfile>(); cleanup.Add(secondProfile);
      secondProfile.InputBindings.Configure(useNativeUguiNavigation: true);
      using var secondSession = new MenuSession();
      secondSession.Open(new[] { Command("second") }, new MenuContext());
      second.Bind(secondSession, secondProfile, context);
      Assert.That(events.sendNavigationEvents, Is.True);
      first.Unbind();
      Assert.That(events.sendNavigationEvents, Is.True, "Closing an older host must retain the active host's ownership.");
      second.Unbind();
      Assert.That(events.sendNavigationEvents, Is.EqualTo(original));
    }

    private EventSystem CreateEventSystem()
    {
      if (!capturedEventSystem) { previousEventSystem = EventSystem.current; capturedEventSystem = true; }
      var root = new GameObject("Menu test EventSystem", typeof(MenuTestEventSystem)); cleanup.Add(root);
      var events = root.GetComponent<EventSystem>(); EventSystem.current = events; return events;
    }

    private static void ConfigureKnownControlRaycast(Canvas canvas, GameObject control)
    {
      var raycaster = canvas.GetComponent<MenuTestControlRaycaster>();
      if (raycaster == null) raycaster = canvas.gameObject.AddComponent<MenuTestControlRaycaster>();
      raycaster.Target = control; raycaster.Camera = canvas.worldCamera;
    }

    private Button ConfigureOverlappingCenter(RadialMenuPresenter presenter, MenuProfile profile)
    {
      var canvas = presenter.GetComponentInParent<Canvas>();
      presenter.Unbind(); context.Dispose();
      canvas.renderMode = RenderMode.WorldSpace;
      var cameraObject = new GameObject("Menu pointer test camera", typeof(Camera)); cleanup.Add(cameraObject);
      var camera = cameraObject.GetComponent<Camera>();
      camera.orthographic = true; camera.orthographicSize = 450;
      camera.transform.position = new Vector3(0, 0, -10);
      canvas.worldCamera = camera;
      if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<MenuTestGraphicRaycaster>();
      Canvas.ForceUpdateCanvases();
      context = new MenuPresentationContext(canvas, new Vector2(Screen.width * .5f, Screen.height * .5f),
        labelOverride: label => label.Fallback);
      var prefab = new GameObject("Custom auxiliary center", typeof(RectTransform)); cleanup.Add(prefab);
      ((RectTransform)prefab.transform).sizeDelta = new Vector2(170, 150);
      var widget = prefab.AddComponent<AuxiliaryCenterWidget>();
      var child = new GameObject("Overlapping auxiliary", typeof(RectTransform), typeof(Image), typeof(Button));
      child.transform.SetParent(prefab.transform, false);
      var rect = (RectTransform)child.transform; rect.anchoredPosition = new Vector2(0, -100); rect.sizeDelta = new Vector2(160, 64);
      widget.Control = child.GetComponent<Button>();
      profile.CenterPrefab = prefab; profile.RingWidth = 60;
      presenter.Bind(session, profile, context);
      return presenter.GetComponentInChildren<AuxiliaryCenterWidget>().Control;
    }

    [TestCase(-500, -300, -450, -250)]
    [TestCase(500, 300, 450, 250)]
    [TestCase(0, 0, 0, 0)]
    public void ScreenPlacement_ClampsCompleteBoundsWithoutScaling(float x, float y, float expectedX, float expectedY)
    {
      var viewport = new Rect(-500, -300, 1000, 600);
      var bounds = new Rect(-50, -50, 100, 100);
      Assert.That(MenuScreenPlacement.TryClamp(viewport, bounds, new Vector2(x, y), out var point), Is.True);
      Assert.That(point.x, Is.EqualTo(expectedX));
      Assert.That(point.y, Is.EqualTo(expectedY));
    }

    [Test]
    public void ScreenPlacement_ReportsInsufficientCapacityRatherThanShrinkingTargets()
      => Assert.That(MenuScreenPlacement.TryClamp(new Rect(0, 0, 50, 50), new Rect(-50, -50, 100, 100),
        Vector2.zero, out _), Is.False);

    public sealed class RecordingMenuWidget : MonoBehaviour, IMenuWidget
    {
      public MenuWidgetState State { get; private set; }
      public void Bind(MenuWidgetState state) => State = state;
      public void Unbind() => State = null;
    }

    public sealed class AuxiliaryCenterWidget : MonoBehaviour, IMenuWidget
    {
      public Button Control;
      private MenuWidgetState state;
      public void Bind(MenuWidgetState value) { Unbind(); state = value; Control.onClick.AddListener(Activate); }
      public void Unbind() { if (Control != null) Control.onClick.RemoveListener(Activate); state = null; }
      private void Activate()
      { if (state != null && state.SecondaryStates.Count > 0 && state.SecondaryStates[0].Availability.Enabled) state.SecondaryStates[0].Activate?.Invoke(); }
    }

    [ExecuteAlways]
    public sealed class MenuTestEventSystem : EventSystem { }

    [ExecuteAlways]
    public sealed class MenuTestGraphicRaycaster : GraphicRaycaster { }

    /// <summary>
    /// Tests semantic ownership against a known uGUI endpoint without claiming EditMode rendered
    /// geometry. Real GraphicRaycaster/depth/hit behavior is verified separately in PlayMode.
    /// </summary>
    [ExecuteAlways]
    public sealed class MenuTestControlRaycaster : BaseRaycaster
    {
      public GameObject Target;
      public Camera Camera;
      public override Camera eventCamera => Camera;
      public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
      {
        if (Target == null || !Target.activeInHierarchy || !RectTransformUtility.RectangleContainsScreenPoint(
            Target.GetComponent<RectTransform>(), eventData.position, Camera)) return;
        results.Add(new RaycastResult { gameObject = Target, module = this, index = results.Count,
          distance = 0, depth = int.MaxValue, sortingOrder = int.MaxValue, screenPosition = eventData.position });
      }
    }

    private sealed class PointerInputScope : IDisposable
    {
      private const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      private readonly InputSettings settings = InputSystem.settings;
      private readonly InputSettings.UpdateMode previousMode;
      private readonly bool previousFeature;
      private readonly Mouse previousMouse = Mouse.current;
      private readonly List<InputActionReference> references = new List<InputActionReference>();
      private readonly InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
      private readonly Mouse mouse;
      public InputAction Point { get; }
      public InputAction Click { get; }
      public PointerInputScope()
      {
        previousMode = settings.updateMode;
        previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
          .Invoke(settings, new object[] { PlayerUpdates });
        settings.SetInternalFeatureFlag(PlayerUpdates, true);
        settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        mouse = InputSystem.AddDevice<Mouse>();
        asset.devices = new InputDevice[] { mouse };
        var map = asset.AddActionMap("Menu");
        Point = map.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
        Click = map.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton");
      }
      public InputActionReference Reference(InputAction action)
      { var reference = InputActionReference.Create(action); references.Add(reference); return reference; }
      public void Send(Vector2 position, bool pressed)
      { InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = pressed ? (ushort)1 : (ushort)0 }); InputSystem.Update(); }
      public void Dispose()
      {
        try
        {
          asset.Disable();
          if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
          foreach (var reference in references) Object.DestroyImmediate(reference);
          Object.DestroyImmediate(asset);
        }
        finally
        {
          settings.SetInternalFeatureFlag(PlayerUpdates, previousFeature); settings.updateMode = previousMode;
          if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }
      }
    }

    private sealed class RecordingPromptService : IInputPromptService
    {
      public InputAction LastAction { get; private set; }
      public Observable<Unit> Changed => Observable.Empty<Unit>();
      public InputPromptContext Context => default;
      public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
      { LastAction = action; return "Runtime hint"; }
      public string ResolveReference(InputActionReference action, InputPromptStyle style = InputPromptStyle.Auto,
        InputPromptContext? context = null) => throw new AssertionException("Presenter must resolve the host-owned runtime clone first.");
      public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null) => string.Empty;
      public string Localize(string key, string fallback, params object[] arguments) => fallback;
      public string Compose(string key, string fallback, params InputPromptArgument[] arguments) => fallback;
      public IDisposable Bind(TMP_Text text, Func<string> render) => Disposable.Empty;
    }
  }
}
