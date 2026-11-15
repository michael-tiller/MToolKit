using System;
using System.Collections.Generic;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Input.Interfaces;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuRegistrationPresenter : MonoBehaviour, IContextMenuPresenter
  {
    public RectTransform Root => (RectTransform)transform;
    public void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context) { }
    public void Unbind() { }
    public void Navigate(Vector2 direction, bool analog) { }
    public void Point(Vector2 position) { }
    public void Confirm() { }
    public void Back() { }
    public void Cancel() { }
    public void InvokeSecondary(int index) { }
    public void Page(int direction) { }
    public void SwitchRing() { }
    public void BeginHold() { }
    public void EndHold() { }
  }

  public sealed class MenuRebinderOwnershipTests
  {
    private readonly List<Object> owned = new();
    private T Own<T>(T asset) where T : Object { owned.Add(asset); return asset; }
    [TearDown]
    public void Cleanup()
    {
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
    }
    private InputAction Action(string map = "Menu", string name = "Submit")
      => Own(ScriptableObject.CreateInstance<InputActionAsset>()).AddActionMap(map).AddAction(name, InputActionType.Button);
    private MenuHostConfig Config(InputAction action)
    {
      var profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.PresenterPrefab = Own(new GameObject("Registered menu presenter", typeof(RectTransform), typeof(MenuRegistrationPresenter)));
      var reference = Own(InputActionReference.Create(action)); profile.InputBindings.Configure(submit: reference);
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = profile; return config;
    }
    private MenuOpenRequest Request()
    {
      var canvas = Own(new GameObject("Registered menu canvas", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      return new MenuOpenRequest(new[] { new MenuAction("leaf", new MenuLabel("Leaf")) }, new MenuContext(), canvas, Vector2.zero);
    }

    [Test]
    public void Disposing_host_removes_its_foreign_clone_registration_without_removing_application_actions()
    {
      var menuAction = Action(); var applicationAction = Action("Player", "Fire");
      using var rebinder = new InputRebinderService(); rebinder.RegisterAction(applicationAction);
      using var host = new ContextMenuHostService(Config(menuAction), rebinder: rebinder);
      host.OpenAsync(Request()).GetAwaiter().GetResult();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(2));
      host.Dispose();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1), "Shared registry retained a destroyed host-owned input clone");
      foreach (var registered in rebinder.RegisteredActions) Assert.That(ReferenceEquals(registered, applicationAction), Is.True);
    }

    [Test]
    public void Independent_hosts_keep_distinct_same_name_foreign_clones_until_their_own_disposal()
    {
      var config = Config(Action());
      using var rebinder = new InputRebinderService();
      using var first = new ContextMenuHostService(config, rebinder: rebinder);
      using var second = new ContextMenuHostService(config, rebinder: rebinder);
      first.OpenAsync(Request()).GetAwaiter().GetResult(); second.OpenAsync(Request()).GetAwaiter().GetResult();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(2), "The second host collided with the first clone's map/action name");
      first.Dispose();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      second.Dispose();
      Assert.That(rebinder.RegisteredActions, Is.Empty);
    }

    [Test]
    public void Router_scope_does_not_remove_a_permanently_registered_borrowed_action()
    {
      var action = Action();
      using var rebinder = new InputRebinderService(); rebinder.RegisterAction(action);
      var bindings = new MenuInputBindings(); bindings.Configure(submit: Own(InputActionReference.Create(action)));
      using var router = new MenuInputRouter(new Target(), bindings, rebinder: rebinder);
      router.Dispose();
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      foreach (var registered in rebinder.RegisteredActions) Assert.That(ReferenceEquals(registered, action), Is.True);
    }

    [Test]
    public void Host_does_not_register_its_foreign_clones_in_a_legacy_rebinder_without_release_support()
    {
      using var rebinder = new LegacyRebinder();
      var applicationAction = Action("Player", "Fire"); rebinder.RegisterAction(applicationAction); rebinder.RegisterCalls = 0;
      using var host = new ContextMenuHostService(Config(Action()), rebinder: rebinder);
      host.OpenAsync(Request()).GetAwaiter().GetResult();
      host.Dispose();
      Assert.That(rebinder.RegisterCalls, Is.Zero, "An API with no release operation received a host-owned foreign clone");
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
    }

    [Test]
    public void Direct_router_preserves_legacy_registration_for_application_owned_actions()
    {
      using var rebinder = new LegacyRebinder();
      var applicationAction = Action("Application", "Confirm");
      var bindings = new MenuInputBindings(); bindings.Configure(submit: Own(InputActionReference.Create(applicationAction)));
      using var router = new MenuInputRouter(new Target(), bindings, rebinder: rebinder);
      router.Dispose();
      Assert.That(rebinder.RegisterCalls, Is.EqualTo(1));
      Assert.That(rebinder.RegisteredActions.Count, Is.EqualTo(1));
      foreach (var action in rebinder.RegisteredActions) Assert.That(ReferenceEquals(action, applicationAction), Is.True);
    }

    private sealed class Target : IMenuInputTarget
    {
      public void Navigate(Vector2 direction, bool analog) { }
      public void Point(Vector2 position) { }
      public void Confirm() { }
      public void Back() { }
      public void Cancel() { }
      public void InvokeSecondary(int index) { }
      public void Page(int direction) { }
      public void SwitchRing() { }
      public void BeginHold() { }
      public void EndHold() { }
    }
    private sealed class LegacyRebinder : IInputRebinderService, IDisposable
    {
      private readonly List<InputAction> registered = new();
      public int RegisterCalls;
      public Subject<string> OnBindingConflictDetected { get; } = new();
      public Subject<(InputAction action, int bindingIndex)> OnRebindingStarted { get; } = new();
      public Subject<(InputAction action, int bindingIndex, bool completed)> OnRebindingCompleted { get; } = new();
      public Subject<(string conflictingPath, InputAction conflictingAction, int conflictingBindingIndex)> OnBindingConflictFound { get; } = new();
      public IReadOnlyCollection<InputAction> RegisteredActions => registered;
      public bool IsRebinding => false;
      public void RegisterAction(InputAction action) { RegisterCalls++; registered.Add(action); }
      public bool StartInteractiveRebinding(InputAction action, int bindingIndex, string[] excludeControls = null) => false;
      public void CancelRebinding() { }
      public void RemoveBindingOverride(InputAction action, int bindingIndex) { }
      public void ResetActionBindings(InputAction action) { }
      public void ResetAllBindings() { }
      public bool CheckBindingConflict(InputAction action, int bindingIndex, string newBindingPath) => false;
      public bool ResolveBindingConflict(InputAction conflictingAction, int conflictingBindingIndex) => false;
      public List<(InputAction action, int bindingIndex)> GetActionsUsingBindingPath(string path) => new();
      public void Dispose()
      { OnBindingConflictDetected.Dispose(); OnRebindingStarted.Dispose(); OnRebindingCompleted.Dispose(); OnBindingConflictFound.Dispose(); }
    }
  }
}
