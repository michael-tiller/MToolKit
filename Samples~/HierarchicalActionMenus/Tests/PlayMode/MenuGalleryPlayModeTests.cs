#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Geometry;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Presentation;
using MToolKit.ContextMenus.Rendering;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Theme;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Samples.Tests
{
  /// <summary>
  /// Runs the saved gallery and its real input asset. Generate the sample with
  /// Tools/MToolKit/Samples/Create Action Menu Gallery before running this assembly.
  /// A 1600 x 1000 viewport accommodates the authored two-ring composition.
  /// </summary>
  public sealed class MenuGalleryPlayModeTests
  {
    private const float Timeout = 8;
    private readonly List<EventSystem> disabledEventSystems = new();
    private readonly List<MenuGalleryController> suspendedGalleries = new();
    private readonly HashSet<GamepadButton> heldButtons = new();
    private MenuGalleryController gallery;
    private Scene galleryScene, priorActiveScene;
    private Gamepad gamepad;
    private Mouse mouse;
    private readonly List<Object> ownedObjects = new();
    private Vector2 stick;
    private InputSettings.UpdateMode priorUpdateMode;
    private InputSettings.EditorInputBehaviorInPlayMode priorEditorInputBehavior;
    private InputSettings.BackgroundBehavior priorBackgroundBehavior;
    private bool priorRunInBackground;
    private GameObject priorSelection;
    private EventSystem priorEvents;
    private bool capturedInputSettings;
    private CurrentTheme previousGlobalTheme;
    private string pointerTrace;

    [UnitySetUp]
    public IEnumerator LoadSavedGallery()
    {
      previousGlobalTheme = CurrentTheme.HasInstance ? CurrentTheme.Instance : null;
      string[] scenes = AssetDatabase.FindAssets("ActionMenuGallery t:Scene", new[] { "Assets/Samples" });
      Assert.That(scenes.Length, Is.EqualTo(1),
        "Generate exactly one persisted ActionMenuGallery scene with the sample builder before running PlayMode tests.");
      string scenePath = AssetDatabase.GUIDToAssetPath(scenes[0]);
      priorActiveScene = SceneManager.GetActiveScene();
      priorEvents = EventSystem.current;
      priorSelection = priorEvents != null ? priorEvents.currentSelectedGameObject : null;
      // The runner may enter PlayMode from this same saved gallery. Suspend that
      // pre-existing controller's owned input before loading the test instance.
      foreach (var existing in Object.FindObjectsByType<MenuGalleryController>(FindObjectsSortMode.None))
        if (existing.enabled) { suspendedGalleries.Add(existing); existing.enabled = false; }
      foreach (var events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        if (events.enabled) { disabledEventSystems.Add(events); events.enabled = false; }
      priorUpdateMode = InputSystem.settings.updateMode;
      priorEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
      priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
      priorRunInBackground = Application.runInBackground;
      capturedInputSettings = true;
      Application.runInBackground = true;
      InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
      InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
      // Tests inject only the devices assigned to their owned asset. Route those
      // events to PlayMode even when automation leaves an Inspector tab focused.
      InputSystem.settings.editorInputBehaviorInPlayMode =
        InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
      gamepad = InputSystem.AddDevice<Gamepad>();
      var load = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
      yield return WaitFor(() => load.isDone, "Saved gallery scene did not load");
      galleryScene = SceneManager.GetSceneByPath(scenePath);
      Assert.That(galleryScene.IsValid() && galleryScene.isLoaded, Is.True);
      SceneManager.SetActiveScene(galleryScene);
      foreach (var root in galleryScene.GetRootGameObjects())
      {
        gallery = root.GetComponentInChildren<MenuGalleryController>(true);
        if (gallery != null) break;
      }
      Assert.That(gallery, Is.Not.Null, "Saved scene has no gallery controller");
      int activeGalleries = 0;
      foreach (var candidate in Object.FindObjectsByType<MenuGalleryController>(FindObjectsSortMode.None))
        if (candidate.isActiveAndEnabled) activeGalleries++;
      Assert.That(activeGalleries, Is.EqualTo(1), "The test must own exactly one enabled gallery/input source");
      yield return WaitFor(() => gallery.MenuService?.IsOpen == true, "Gallery did not open its initial menu");
      Assert.That(CurrentTheme.HasInstance, Is.EqualTo(previousGlobalTheme != null), "The gallery changed global theme ownership");
      if (previousGlobalTheme != null) Assert.That(CurrentTheme.Instance, Is.SameAs(previousGlobalTheme));
      Assert.That(gallery.InputModule.actionsAsset, Is.Not.SameAs(gallery.InputActions),
        "The sample must route the InputService runtime clone rather than mutate the authored asset");
      gallery.InputModule.actionsAsset.devices = new InputDevice[] { gamepad };
      yield return Neutral();
    }

    [UnityTearDown]
    public IEnumerator RestoreOwnedState()
    {
      if (gallery != null) gallery.Host.Close();
      if (gamepad != null && gamepad.added)
      {
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return null;
      }
      if (priorActiveScene.IsValid() && priorActiveScene.isLoaded) SceneManager.SetActiveScene(priorActiveScene);
      if (galleryScene.IsValid() && galleryScene.isLoaded)
      {
        var unload = SceneManager.UnloadSceneAsync(galleryScene);
        if (unload != null) yield return WaitFor(() => unload.isDone, "Gallery scene did not unload");
      }
      // The controller owns the gallery's DontDestroyOnLoad theme and cloned input asset.
      yield return null;
      if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
      if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
      mouse = null;
      foreach (var owned in ownedObjects) if (owned != null) Object.Destroy(owned);
      ownedObjects.Clear();
      gamepad = null; heldButtons.Clear(); stick = Vector2.zero;
      if (capturedInputSettings)
      {
        InputSystem.settings.updateMode = priorUpdateMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorInputBehavior;
        InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
        Application.runInBackground = priorRunInBackground;
      }
      capturedInputSettings = false;
      foreach (var existing in suspendedGalleries) if (existing != null) existing.enabled = true;
      suspendedGalleries.Clear();
      foreach (var events in disabledEventSystems) if (events != null) events.enabled = true;
      disabledEventSystems.Clear();
      if (priorEvents != null && priorEvents.isActiveAndEnabled)
      {
        EventSystem.current = priorEvents;
        priorEvents.SetSelectedGameObject(priorSelection);
      }
      gallery = null; galleryScene = default;
    }

    [UnityTest]
    public IEnumerator Four_saved_profiles_and_a_custom_center_configuration_create_native_presentations()
    {
      string[] styles = { MenuGalleryController.WheelContextMenu, MenuGalleryController.PieContextMenu,
        MenuGalleryController.ListContextMenu, MenuGalleryController.NestedWheelContextMenu };
      foreach (string style in styles)
      {
        yield return Open(style);
        var profile = gallery.HostConfig.Resolve(style);
        Assert.That(AssetDatabase.GetAssetPath(profile), Does.EndWith(".asset"));
        Assert.That(AssetDatabase.GetAssetPath(profile.PresenterPrefab), Does.EndWith(".prefab"));
        Assert.That(AssetDatabase.GetAssetPath(profile.ItemPrefab), Does.EndWith(".prefab"));
        var presenter = gallery.MenuService.Presenter;
        if (style == MenuGalleryController.ListContextMenu) Assert.That(presenter, Is.TypeOf<ListMenuPresenter>());
        else Assert.That(presenter, Is.TypeOf<RadialMenuPresenter>(),
          style + " fell back to a list; run with enough viewport space for the authored radial profile");
        AssertContained();
        var widgets = presenter.Root.GetComponentsInChildren<MenuActionWidget>();
        int actionWidgetCount = 0;
        bool anyLabel = false, anyIcon = false;
        foreach (var widget in widgets)
        {
          if (widget is MenuCenterWidget || widget.BackgroundRole == MenuVisualRole.Auxiliary) continue;
          actionWidgetCount++;
          anyLabel |= widget.Label != null && !string.IsNullOrEmpty(widget.Label.text);
          anyIcon |= widget.Icon != null && widget.Icon.enabled && widget.Icon.sprite != null;
        }
        Assert.That(actionWidgetCount, Is.GreaterThanOrEqualTo(5), style + " has no authored action widgets");
        Assert.That(anyIcon, Is.True, style);
        if (style == MenuGalleryController.WheelContextMenu) Assert.That(anyLabel, Is.False, "WheelContextMenu item prefab is deliberately icon-only");
        else Assert.That(anyLabel, Is.True, style);
        var graphics = presenter.Root.GetComponentsInChildren<NativeRadialGraphic>();
        if (style == MenuGalleryController.PieContextMenu || style == MenuGalleryController.ListContextMenu)
          Assert.That(graphics.Length, Is.Zero, style + " should not render background wedges");
        else
        {
          Assert.That(graphics.Length, Is.EqualTo(5), style);
          var mesh = graphics[0].canvasRenderer.GetMesh();
          Assert.That(mesh, Is.Not.Null, style + " produced no uGUI sector mesh");
          Assert.That(mesh.vertexCount, Is.GreaterThan(0), style + " produced an empty uGUI sector mesh");
        }
      }
      // Richer center information is a configuration of the canonical wheel, not another root style.
      var canonicalWheel = gallery.HostConfig.Resolve(MenuGalleryController.WheelContextMenu);
      string profilePath = AssetDatabase.GetAssetPath(canonicalWheel);
      int profileFolder = profilePath.LastIndexOf("/Profiles/", StringComparison.Ordinal);
      Assert.That(profileFolder, Is.GreaterThanOrEqualTo(0));
      var customCenter = AssetDatabase.LoadAssetAtPath<GameObject>(profilePath.Substring(0, profileFolder) + "/Prefabs/CustomCenter.prefab");
      Assert.That(customCenter, Is.Not.Null);
      var configuredWheel = Object.Instantiate(canonicalWheel);
      ownedObjects.Add(configuredWheel);
      configuredWheel.CenterPrefab = customCenter;
      var context = new MenuContext(gallery.State);
      yield return gallery.MenuService.OpenAsync(new MenuOpenRequest(gallery.ActionSet.Create(context), context,
        gallery.Canvas, new Vector2(Screen.width * .5f, Screen.height * .5f), profile: configuredWheel)).ToCoroutine();
      yield return gallery.MenuService.Session.EnterSubmenuAsync("gallery.build").ToCoroutine();
      Assert.That(gallery.MenuService.Session.Select("gallery.build/gallery.wall"), Is.True);
      yield return null; yield return null;
      var boundCenter = gallery.MenuService.Presenter.Root.GetComponentInChildren<GalleryCenterWidget>();
      Assert.That(boundCenter, Is.Not.Null);
      Assert.That(boundCenter.BoundAction, Is.EqualTo("gallery.build/gallery.wall"));
      Assert.That(boundCenter.Title.text, Is.EqualTo("Wall"));
      Assert.That(boundCenter.Details.text, Does.Contain("Cost: 2 stone"));
      AssertContained();
    }

    [UnityTest]
    public IEnumerator NestedWheelContextMenu_partial_arc_slides_through_three_authored_submenu_levels()
    {
      yield return Open(MenuGalleryController.NestedWheelContextMenu);
      string[] path = { "gallery.build", "gallery.build/gallery.doors", "gallery.build/gallery.doors/gallery.metal-doors" };
      for (int depth = 0; depth < path.Length; depth++)
      {
        Assert.That(gallery.MenuService.Session.Select(path[depth]), Is.True);
        yield return Tap(GamepadButton.South);
        int expected = depth + 1;
        yield return WaitFor(() => gallery.MenuService.Session.Path.Count == expected, "Concentric submenu did not enter");
        var radial = gallery.MenuService.Presenter as RadialMenuPresenter;
        Assert.That(radial, Is.Not.Null, "The two-ring sample unexpectedly fell back");
        Assert.That(radial.InnerGeometry, Is.Not.Null);
        Assert.That(radial.OuterGeometry, Is.Not.Null);
        Assert.That(gallery.MenuService.Session.ParentSelected.Id.ToString(), Is.EqualTo(path[depth]));
        float arc = 0;
        foreach (var segment in radial.OuterGeometry.Segments) arc += Math.Abs(segment.SweepAngle);
        Assert.That(arc, Is.LessThan(180), "Outer children must occupy a partial parent-associated arc");
        var child = radial.OuterGeometry.Segments[0];
        Assert.That(child.InnerRadius, Is.GreaterThan(radial.InnerGeometry.Segments[0].OuterRadius));
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, radial.Root.TransformPoint(child.ContentAnchor));
        NativeRadialGraphic actual = null;
        foreach (var graphic in radial.Root.GetComponentsInChildren<NativeRadialGraphic>())
          if (graphic.Geometry.RingIndex == 1 && graphic.Geometry.SegmentIndex == 0) { actual = graphic; break; }
        Assert.That(actual, Is.Not.Null);
        Assert.That(actual.IsRaycastLocationValid(screenPoint, null), Is.True, "Visible child wedge must accept its actual canvas-space hit");
        AssertContained();
      }
      Assert.That(gallery.State.CommandsExecuted, Is.Zero, "Entering a submenu must not execute its descendants");
      for (int depth = 2; depth >= 0; depth--)
      {
        yield return Tap(GamepadButton.East);
        int expected = depth;
        yield return WaitFor(() => gallery.MenuService.Session.Path.Count == expected, "Back did not restore the parent frame");
      }
      yield return Tap(GamepadButton.East);
      Assert.That(gallery.MenuService.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator PieContextMenu_branch_replaces_the_whole_presenter_and_restores_it_after_recursive_Back()
    {
      yield return Open(MenuGalleryController.PieContextMenu);
      Assert.That(gallery.MenuService.Session.Select("gallery.interactions"), Is.True);
      yield return Tap(GamepadButton.South);
      yield return WaitFor(() => gallery.MenuService.Presenter is GalleryCustomPresenter, "Authored branch did not replace the entire presenter");
      Assert.That(gallery.MenuService.Session.Path.Count, Is.EqualTo(1));
      Assert.That(gallery.MenuService.Presenter.Root.GetComponentsInChildren<NativeRadialGraphic>().Length, Is.Zero);
      yield return Tap(GamepadButton.DpadDown);
      yield return Tap(GamepadButton.DpadDown);
      Assert.That(gallery.MenuService.Session.Selected.Id.ToString(), Is.EqualTo("gallery.interactions/gallery.conversation"));
      yield return Tap(GamepadButton.South);
      Assert.That(gallery.MenuService.Session.Path.Count, Is.EqualTo(2));
      Assert.That(gallery.MenuService.Presenter, Is.TypeOf<GalleryCustomPresenter>(), "Ancestor presentation override must survive deeper navigation");
      yield return Tap(GamepadButton.East);
      Assert.That(gallery.MenuService.Session.Path.Count, Is.EqualTo(1));
      Assert.That(gallery.MenuService.Presenter, Is.TypeOf<GalleryCustomPresenter>());
      yield return Tap(GamepadButton.East);
      yield return WaitFor(() => gallery.MenuService.Presenter is RadialMenuPresenter, "Back did not restore the original PieContextMenu presenter");
      Assert.That(gallery.MenuService.Session.Path.Count, Is.Zero);
      Assert.That(gallery.State.CommandsExecuted, Is.Zero);
      AssertContained();
    }

    [UnityTest]
    public IEnumerator List_gamepad_foldout_renders_live_structured_check_state()
    {
      yield return Open(MenuGalleryController.ListContextMenu);
      Assert.That(gallery.MenuService.Session.Select("gallery.settings"), Is.True);
      yield return Tap(GamepadButton.South);
      Assert.That(gallery.MenuService.Session.Path.Count, Is.Zero, "Default list navigation must expand inline");
      Assert.That(HasRow("gallery.settings/gallery.snap", 1), Is.True);
      Assert.That(gallery.MenuService.Session.Select("gallery.settings/gallery.snap"), Is.True);
      yield return Tap(GamepadButton.South);
      Assert.That(gallery.State.IsChecked("gallery.snap"), Is.True);
      Assert.That(gallery.MenuService.IsOpen, Is.True);
      MenuActionWidget snap = FindWidget("Snap to grid");
      Assert.That(snap, Is.Not.Null);
      Assert.That(snap.Check, Is.Not.Null);
      Assert.That(snap.Check.enabled, Is.True, "The prefab's structured check graphic was not updated");
      yield return Tap(GamepadButton.DpadDown);
      Assert.That(gallery.MenuService.Session.Selected.Id.ToString(), Is.EqualTo("gallery.settings/gallery.careful"), "Real D-pad input did not navigate the expanded list");
      Assert.That(gallery.MenuService.Session.Select("gallery.settings"), Is.True);
      yield return Tap(GamepadButton.South);
      Assert.That(HasRow("gallery.settings/gallery.snap", 1), Is.False);
    }

    [UnityTest]
    public IEnumerator Gamepad_analog_ring_focus_and_secondary_use_the_real_rebindable_sample_actions()
    {
      yield return Open(MenuGalleryController.NestedWheelContextMenu);
      var radial = (RadialMenuPresenter)gallery.MenuService.Presenter;
      yield return Aim(radial.InnerGeometry.Segments[1]);
      Assert.That(gallery.MenuService.Session.Selected.Id.ToString(), Is.EqualTo("gallery.equipment"));
      yield return Tap(GamepadButton.South);
      yield return WaitFor(() => gallery.MenuService.Session.Path.Count == 1, "Gamepad confirmation did not enter equipment");
      radial = (RadialMenuPresenter)gallery.MenuService.Presenter;
      Assert.That(radial.FocusedRing, Is.EqualTo(1));
      yield return Aim(radial.OuterGeometry.Segments[0]);
      Assert.That(gallery.MenuService.Session.Selected.Id.ToString(), Is.EqualTo("gallery.equipment/gallery.equip-revolver"));
      yield return Tap(GamepadButton.West);
      Assert.That(gallery.State.VariantChanges, Is.EqualTo(1));
      Assert.That(gallery.State.CommandsExecuted, Is.Zero);
      Assert.That(gallery.MenuService.IsOpen, Is.True);
      gallery.ToggleCommands();
      yield return null;
      Assert.That(gallery.MenuService.Session.Selected.Evaluate(gallery.MenuService.Session.Context).Enabled, Is.False);
      yield return Tap(GamepadButton.West);
      Assert.That(gallery.State.VariantChanges, Is.EqualTo(2), "Secondary availability must be independent of primary availability");
      yield return Tap(GamepadButton.RightShoulder);
      Assert.That(radial.FocusedRing, Is.Zero);
      yield return Tap(GamepadButton.RightShoulder);
      Assert.That(radial.FocusedRing, Is.EqualTo(1));
      gallery.ToggleCommands();
      yield return null;
      Assert.That(gallery.MenuService.Session.Selected.Evaluate(gallery.MenuService.Session.Context).Enabled, Is.True);
      yield return Neutral();
      yield return Tap(GamepadButton.South);
      Assert.That(gallery.State.CommandsExecuted, Is.Zero, "Neutral stick must not confirm a stale angular target");
      Assert.That(gallery.State.GameplayFireEvents, Is.Zero);
      Assert.That(gallery.State.GameplayMoveEvents, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Hold_release_executes_once_and_gameplay_controls_restore_after_the_gesture()
    {
      gallery.Host.Close();
      yield return Neutral();
      Button priorControl = null;
      foreach (var control in gallery.Canvas.GetComponentsInChildren<Button>())
        if (control.onClick.GetPersistentEventCount() > 0) { priorControl = control; break; }
      Assert.That(priorControl, Is.Not.Null, "Saved scene has no authored gallery control");
      EventSystem.current.SetSelectedGameObject(priorControl.gameObject);
      bool priorNativeNavigation = EventSystem.current.sendNavigationEvents;
      yield return Hold(GamepadButton.LeftShoulder, true);
      yield return WaitFor(() => gallery.MenuService.IsOpen, "Bound gamepad hold did not open the wheel");
      string focusAtClose = null;
      gallery.MenuService.Session.Closed += () => focusAtClose = EventSystem.current.currentSelectedGameObject?.name;
      Assert.That(gallery.MenuService.Presenter, Is.TypeOf<RadialMenuPresenter>());
      var radial = (RadialMenuPresenter)gallery.MenuService.Presenter;
      yield return Aim(radial.InnerGeometry.Segments[1]);
      yield return Tap(GamepadButton.South);
      Assert.That(gallery.MenuService.Session.Path.Count, Is.EqualTo(1));
      radial = (RadialMenuPresenter)gallery.MenuService.Presenter;
      yield return Aim(radial.InnerGeometry.Segments[0]);
      Assert.That(gallery.MenuService.Session.Selected.Id.ToString(), Is.EqualTo("gallery.equipment/gallery.equip-revolver"));
      Assert.That(gallery.State.GameplayMoveEvents, Is.Zero, "Menu analog input also moved the player while the wheel was open");
      yield return Hold(GamepadButton.LeftShoulder, false);
      yield return WaitFor(() => !gallery.MenuService.IsOpen, "Release did not confirm the held wheel");
      Assert.That(gallery.State.CommandsExecuted, Is.EqualTo(1));
      Assert.That(gallery.State.LastCommand, Does.Contain("Revolver"));
      Assert.That(gallery.State.GameplayFireEvents, Is.Zero, "Menu confirmation also fired the gameplay action");
      Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(priorControl.gameObject),
        "Closing the menu did not restore previous focus; Closed callback=" + focusAtClose
        + ", native move=" + gallery.InputModule.move.action.ReadValue<Vector2>());
      Assert.That(EventSystem.current.sendNavigationEvents, Is.EqualTo(priorNativeNavigation), "Closing the menu retained native-navigation ownership");
      EventSystem.current.SetSelectedGameObject(null); // Return focus to gameplay before testing its restored Submit binding.
      yield return Neutral();
      yield return Tap(GamepadButton.South);
      Assert.That(gallery.State.GameplayFireEvents, Is.EqualTo(1), "Prior gameplay Fire state was not restored");
      yield return Stick(Vector2.up);
      Assert.That(gallery.State.GameplayMoveEvents, Is.GreaterThan(0), "Prior gameplay Move state was not restored");
      Assert.That(gallery.State.CommandsExecuted, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Hold_release_without_an_angular_target_cancels_without_executing()
    {
      gallery.Host.Close(); yield return Neutral();
      yield return Hold(GamepadButton.LeftShoulder, true);
      yield return WaitFor(() => gallery.MenuService.IsOpen, "Hold did not open the authored WheelContextMenu profile");
      yield return Hold(GamepadButton.LeftShoulder, false);
      yield return WaitFor(() => !gallery.MenuService.IsOpen, "Neutral hold release did not cancel");
      Assert.That(gallery.State.CommandsExecuted, Is.Zero);
      Assert.That(gallery.State.GameplayFireEvents, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Rendered_center_secondary_owns_an_overlapping_wedge_click_and_executes_once()
    {
      var profile = Object.Instantiate(gallery.HostConfig.Resolve(MenuGalleryController.WheelContextMenu));
      ownedObjects.Add(profile);
      // This supported composition deliberately overlaps its standard center with the ring.
      profile.InnerRadius = 40; profile.RingWidth = 160;
      var context = new MenuContext(gallery.State);
      yield return gallery.MenuService.OpenAsync(new MenuOpenRequest(gallery.ActionSet.Create(context), context,
        gallery.Canvas, new Vector2(Screen.width * .5f, Screen.height * .5f), profile: profile)).ToCoroutine();
      yield return gallery.MenuService.Session.EnterSubmenuAsync("gallery.equipment").ToCoroutine();
      Assert.That(gallery.MenuService.Session.Select("gallery.equipment/gallery.equip-bow"), Is.True);
      yield return null; yield return null;
      var radial = gallery.MenuService.Presenter as RadialMenuPresenter;
      Assert.That(radial, Is.Not.Null);
      var center = radial.Root.GetComponentInChildren<MenuCenterWidget>();
      Assert.That(center, Is.Not.Null);
      var auxiliaries = center.SecondaryContainer.GetComponentsInChildren<Button>();
      Assert.That(auxiliaries.Length, Is.EqualTo(2));
      var button = auxiliaries[0];
      Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
        button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
      Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(radial.Root, point, null, out var local), Is.True);
      Assert.That(radial.InnerGeometry.HitTest(local), Is.GreaterThanOrEqualTo(0),
        "The authored secondary layout must actually overlap a visible native sector for this regression");
      AssertTopRaycastIsInside(point, button.transform);
      yield return MouseClick(point);
      Assert.That(gallery.State.VariantChanges, Is.EqualTo(1), "Semantic and native pointer callbacks duplicated or lost the auxiliary action: " + pointerTrace);
      Assert.That(gallery.State.CommandsExecuted, Is.Zero, "The primary wedge also executed through the overlapping center control");
      Assert.That(gallery.MenuService.IsOpen, Is.True);
    }

    [UnityTest]
    public IEnumerator Rendered_PieContextMenu_button_accepts_its_visible_corner_outside_the_annulus()
    {
      var profile = Object.Instantiate(gallery.HostConfig.Resolve(MenuGalleryController.PieContextMenu));
      ownedObjects.Add(profile);
      // At the authored 90..240 band a 144x56 first button has no out-of-band
      // corner. A supported size override provides a clear native hit beyond it.
      profile.WidgetSize = new Vector2(216, 88);
      Assert.That(profile.Validate(out string diagnostic), Is.True, diagnostic);
      var context = new MenuContext(gallery.State);
      yield return gallery.MenuService.OpenAsync(new MenuOpenRequest(gallery.ActionSet.Create(context), context,
        gallery.Canvas, new Vector2(Screen.width * .5f, Screen.height * .5f), profile: profile)).ToCoroutine();
      yield return null; yield return null;
      var radial = (RadialMenuPresenter)gallery.MenuService.Presenter;
      var band = radial.InnerGeometry.Segments[0];
      var build = FindWidget("Build");
      Assert.That(build, Is.Not.Null);
      var rect = (RectTransform)build.transform;
      Vector2 point = default;
      bool found = false;
      foreach (var corner in new[] { new Vector2(rect.rect.xMin + 3, rect.rect.yMin + 3),
        new Vector2(rect.rect.xMax - 3, rect.rect.yMin + 3), new Vector2(rect.rect.xMin + 3, rect.rect.yMax - 3),
        new Vector2(rect.rect.xMax - 3, rect.rect.yMax - 3) })
      {
        Vector2 candidate = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(corner));
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(radial.Root, candidate, null, out var local)
            && (local.sqrMagnitude < band.InnerRadius * band.InnerRadius || local.sqrMagnitude >= band.OuterRadius * band.OuterRadius)
            && radial.InnerGeometry.HitTest(local) == -1) { point = candidate; found = true; break; }
      }
      Assert.That(found, Is.True, "The authored PieContextMenu button needs a visible corner outside the circular hit bands");
      AssertTopRaycastIsInside(point, build.transform);
      yield return MouseClick(point);
      Assert.That(gallery.MenuService.Session.Path.Count, Is.EqualTo(1), pointerTrace);
      Assert.That(gallery.MenuService.Session.ParentSelected.Id.Value, Is.EqualTo("gallery.build"));
      Assert.That(gallery.State.CommandsExecuted, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Closing_semantic_submit_does_not_activate_the_restored_sidebar()
    {
      gallery.Host.Close(); yield return Neutral();
      Button priorControl = null;
      foreach (var control in gallery.Canvas.GetComponentsInChildren<Button>())
        if (control.onClick.GetPersistentEventCount() > 0) { priorControl = control; break; }
      Assert.That(priorControl, Is.Not.Null);
      int sidebarSubmits = 0;
      UnityEngine.Events.UnityAction recordSubmit = () => sidebarSubmits++;
      priorControl.onClick.AddListener(recordSubmit);
      var failures = new List<string>();
      try
      {
        foreach (string style in new[] { MenuGalleryController.ListContextMenu, MenuGalleryController.WheelContextMenu })
        {
          gallery.Host.Close(); yield return Neutral();
          EventSystem.current.SetSelectedGameObject(priorControl.gameObject);
          sidebarSubmits = 0;
          int commands = 0;
          var menuContext = new MenuContext(gallery.State);
          var leaf = new MenuAction("submit.handoff", new MenuLabel("Close once"),
            new MenuCommandBehavior(_ => commands++, MenuActionOutcome.Close));
          var gameplay = new List<InputAction>();
          var player = gallery.InputModule.actionsAsset.FindActionMap("Player");
          if (player != null) foreach (var action in player.actions) gameplay.Add(action);
          yield return gallery.MenuService.OpenAsync(new MenuOpenRequest(new[] { leaf }, menuContext, gallery.Canvas,
            new Vector2(Screen.width * .5f, Screen.height * .5f), profile: gallery.HostConfig.Resolve(style),
            gameplayActions: gameplay)).ToCoroutine();
          if (gallery.MenuService.Presenter is RadialMenuPresenter radial)
            yield return Aim(radial.InnerGeometry.Segments[0]);
          yield return Tap(GamepadButton.South);
          if (commands != 1) failures.Add(style + ": command calls=" + commands);
          if (sidebarSubmits != 0) failures.Add(style + ": restored sidebar submits=" + sidebarSubmits);
          if (gallery.MenuService.IsOpen) failures.Add(style + ": menu reopened or failed to close");
        }
      }
      finally
      {
        priorControl.onClick.RemoveListener(recordSubmit);
        gallery.Host.Close();
      }
      Assert.That(failures, Is.Empty, string.Join("; ", failures));
    }
    [UnityTest]
    public IEnumerator Live_theme_and_UI_scale_changes_preserve_viewport_containment_at_the_screen_edge()
    {
      yield return Open(MenuGalleryController.WheelContextMenu);
      var graphics = gallery.MenuService.Presenter.Root.GetComponentsInChildren<NativeRadialGraphic>();
      NativeRadialGraphic normal = null;
      foreach (var graphic in graphics) if (graphic.Geometry.SegmentIndex == 1) { normal = graphic; break; }
      Assert.That(normal, Is.Not.Null);
      Color before = normal.color;
      var theme = gallery.StandaloneTheme.Current;
      gallery.ToggleTheme();
      yield return null;
      Assert.That(gallery.StandaloneTheme.Current, Is.Not.SameAs(theme));
      Assert.That(normal.color, Is.Not.EqualTo(before), "An open native menu did not consume the semantic theme change");
      gallery.IncreaseScale(); gallery.IncreaseScale();
      yield return null; yield return null;
      Assert.That(gallery.ScaleCanvas.RequestedUserScale, Is.GreaterThan(1));
      Assert.That(gallery.MenuService.IsOpen, Is.True);
      AssertContained();
      gallery.OpenAtEdge();
      yield return WaitFor(() => gallery.MenuService.IsOpen && gallery.MenuService.Presenter != null, "Scaled edge menu did not open");
      yield return null;
      AssertContained();
      Assert.That(((IMenuViewportAwarePresenter)gallery.MenuService.Presenter).FitsViewport, Is.True);
    }

    private IEnumerator Open(string style)
    {
      yield return gallery.OpenAsync(style, new Vector2(Screen.width * .5f, Screen.height * .5f)).ToCoroutine();
      yield return null; yield return null;
      Assert.That(gallery.MenuService.IsOpen, Is.True, style);
      Assert.That(gallery.MenuService.Presenter, Is.Not.Null, style);
    }
    private IEnumerator Aim(RadialSegmentGeometry geometry) => Stick(geometry.ContentAnchor.normalized);
    private IEnumerator Stick(Vector2 direction) { stick = direction; yield return Send(); }
    private IEnumerator Neutral() { heldButtons.Clear(); stick = Vector2.zero; yield return Send(); }
    private IEnumerator Hold(GamepadButton button, bool value)
    { if (value) heldButtons.Add(button); else heldButtons.Remove(button); yield return Send(); }
    private IEnumerator Tap(GamepadButton button)
    { yield return Hold(button, true); yield return Hold(button, false); }
    private IEnumerator Send()
    {
      var state = new GamepadState { leftStick = stick };
      foreach (var button in heldButtons) state = state.WithButton(button);
      InputSystem.QueueStateEvent(gamepad, state);
      yield return null; yield return null;
    }
    private IEnumerator MouseClick(Vector2 point)
    {
      pointerTrace = string.Empty;
      mouse ??= InputSystem.AddDevice<Mouse>();
      gallery.InputModule.actionsAsset.devices = new InputDevice[] { gamepad, mouse };
      InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
      yield return null; yield return null;
      TracePointer("hover");
      InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
      yield return null; yield return null;
      TracePointer("pressed");
      InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
      yield return null; yield return null;
      TracePointer("released");
    }
    private void TracePointer(string stage)
    {
      var module = gallery.InputModule;
      var hit = module.GetLastRaycastResult(mouse.deviceId).gameObject;
      pointerTrace += stage + " point=" + module.point.action.ReadValue<Vector2>()
        + " click=" + module.leftClick.action.ReadValue<float>() + " device=" + mouse.deviceId
        + " moduleActive=" + module.isActiveAndEnabled + "/" + (EventSystem.current.currentInputModule == module)
        + " focus=" + Application.isFocused + "/" + EventSystem.current.isFocused
        + " nativeHit=" + (hit != null ? hit.name : "none")
        + " selected=" + gallery.MenuService.Session?.Selected?.Id.Value + "; ";
    }
    private static void AssertTopRaycastIsInside(Vector2 point, Transform expected)
    {
      var hits = new List<RaycastResult>();
      EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
      Assert.That(hits.Count, Is.GreaterThan(0), "The real rendered GraphicRaycaster produced no UI hit");
      Assert.That(hits[0].module, Is.TypeOf<GraphicRaycaster>());
      Assert.That(hits[0].gameObject.transform.IsChildOf(expected), Is.True,
        "The actual topmost raycast belongs to a different visual than the expected authored widget");
    }
    private static IEnumerator WaitFor(Func<bool> condition, string diagnostic)
    {
      float deadline = Time.realtimeSinceStartup + Timeout;
      while (!condition())
      {
        if (Time.realtimeSinceStartup >= deadline) Assert.Fail(diagnostic + " (" + Timeout + " second timeout)");
        yield return null;
      }
    }
    private bool HasRow(string id, int depth)
    {
      foreach (var row in gallery.MenuService.Session.Rows)
        if (row.Action.Id.ToString() == id && row.Depth == depth) return true;
      return false;
    }
    private MenuActionWidget FindWidget(string label)
    {
      foreach (var widget in gallery.MenuService.Presenter.Root.GetComponentsInChildren<MenuActionWidget>())
        if (widget.Label != null && widget.Label.text == label) return widget;
      return null;
    }
    private void AssertContained()
    {
      var presenter = gallery.MenuService.Presenter;
      var aware = presenter as IMenuViewportAwarePresenter;
      Assert.That(aware, Is.Not.Null);
      Assert.That(aware.FitsViewport, Is.True, aware.PlacementDiagnostic);
      var corners = new Vector3[4]; presenter.Root.GetWorldCorners(corners);
      Rect viewport = gallery.Canvas.pixelRect;
      var camera = gallery.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : gallery.Canvas.worldCamera;
      foreach (var corner in corners)
      {
        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
        Assert.That(point.x, Is.InRange(viewport.xMin - 2, viewport.xMax + 2), "Menu extends beyond the horizontal viewport");
        Assert.That(point.y, Is.InRange(viewport.yMin - 2, viewport.yMax + 2), "Menu extends beyond the vertical viewport");
      }
    }
  }
}
#endif
