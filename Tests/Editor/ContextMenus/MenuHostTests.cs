using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using MToolKit.Runtime.AssetLoader.Interfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuHostTestPresenter : MonoBehaviour, IContextMenuPresenter
  {
    public RectTransform Root => (RectTransform)transform;
    public static int Bound, Unbound;
    public void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context)
    { Bound++; Root.anchoredPosition = new Vector2(31, 47); }
    public void Unbind() { Unbound++; }
    public void Navigate(Vector2 direction, bool analog) { }
    public void Point(Vector2 screenPosition) { }
    public void Confirm() { }
    public void Back() { }
    public void Cancel() { }
    public void InvokeSecondary(int index) { }
    public void Page(int direction) { }
    public void SwitchRing() { }
    public void BeginHold() { }
    public void EndHold() { }
  }

  [ExecuteAlways]
  public sealed class MenuHostLifecyclePresenter : MonoBehaviour, IContextMenuPresenter, IMenuContainmentEvents
  {
    public static Action<MenuHostLifecyclePresenter> Binding, Unbinding, Enabled;
    public MenuPresentationContext Context;
    public MenuProfile Profile;
    public int ContainmentSubscribers, BackCalls;
    private Action containment;
    public event Action ContainmentChanged
    {
      add { containment += value; ContainmentSubscribers++; }
      remove { containment -= value; ContainmentSubscribers--; }
    }
    public RectTransform Root => (RectTransform)transform;
    public void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context)
    { Profile = profile; Context = context; Binding?.Invoke(this); }
    public void Unbind() => Unbinding?.Invoke(this);
    private void OnEnable() => Enabled?.Invoke(this);
    public void Navigate(Vector2 direction, bool analog) { }
    public void Point(Vector2 screenPosition) { }
    public void Confirm() { }
    public void Back() { BackCalls++; }
    public void Cancel() { }
    public void InvokeSecondary(int index) { }
    public void Page(int direction) { }
    public void SwitchRing() { }
    public void BeginHold() { }
    public void EndHold() { }
  }

  public sealed class MenuHostTests
  {
    private GameObject canvasOwner, prefab;
    private MenuProfile profile;
    private MenuHostConfig config;
    private readonly List<Object> extras = new();
    [SetUp] public void Setup()
    {
      canvasOwner = new GameObject("Owned host canvas", typeof(RectTransform), typeof(Canvas));
      canvasOwner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      prefab = new GameObject("Owned custom presenter", typeof(RectTransform), typeof(MenuHostTestPresenter));
      profile = ScriptableObject.CreateInstance<MenuProfile>(); profile.PresenterPrefab = prefab;
      config = ScriptableObject.CreateInstance<MenuHostConfig>(); config.DefaultProfile = profile;
      MenuHostTestPresenter.Bound = MenuHostTestPresenter.Unbound = 0;
    }
    [TearDown] public void Cleanup()
    {
      MenuHostLifecyclePresenter.Binding = MenuHostLifecyclePresenter.Unbinding = MenuHostLifecyclePresenter.Enabled = null;
      Object.DestroyImmediate(canvasOwner); Object.DestroyImmediate(prefab); Object.DestroyImmediate(profile); Object.DestroyImmediate(config);
      foreach (var item in extras) if (item != null) Object.DestroyImmediate(item);
      extras.Clear();
    }
    private MenuOpenRequest Request(MenuProfile selected = null)
      => new MenuOpenRequest(new[] { new MenuAction("leaf", new MenuLabel("Leaf")) },
        new MenuContext(), canvasOwner.GetComponent<Canvas>(), Vector2.zero, profile: selected);

    [Test] public void Replacement_retires_the_old_session_and_keeps_one_presenter()
    {
      using var host = new ContextMenuHostService(config);
      host.OpenAsync(Request()).GetAwaiter().GetResult(); var previous = host.Session;
      host.OpenAsync(Request()).GetAwaiter().GetResult();
      Assert.That(previous.IsOpen, Is.False); Assert.That(host.IsOpen, Is.True);
      Assert.That(MenuHostTestPresenter.Bound, Is.EqualTo(2));
      Assert.That(MenuHostTestPresenter.Unbound, Is.EqualTo(1));
      host.Close(); Assert.That(host.IsOpen, Is.False);
    }

    [Test] public void Custom_presenter_owns_its_transform_after_binding()
    {
      using var host = new ContextMenuHostService(config);
      host.OpenAsync(Request()).GetAwaiter().GetResult();
      Assert.That(host.Presenter.Root.anchoredPosition, Is.EqualTo(new Vector2(31, 47)));
    }

    [Test] public void Missing_presenter_fails_with_a_useful_diagnostic_and_no_live_session()
    {
      profile.PresenterPrefab = null;
      using var host = new ContextMenuHostService(config);
      Assert.Throws<ArgumentException>(() => host.OpenAsync(Request()).GetAwaiter().GetResult());
      Assert.That(host.IsOpen, Is.False);
    }

    [Test] public void Closing_a_pending_load_cannot_reopen_the_dismissed_menu()
    {
      var loader = new DeferredLoader();
      using var cache = new MenuPrefabCache(loader);
      var cancellation = new CancellationTokenSource();
      var pending = cache.LoadAsync("test-prefab", cancellation.Token).Preserve();
      cancellation.Cancel();
      Assert.Throws<OperationCanceledException>(() => pending.GetAwaiter().GetResult());
      loader.Source.TrySetResult(prefab);
      cache.Dispose();
      Assert.That(loader.Loads, Is.EqualTo(1)); Assert.That(loader.Releases, Is.EqualTo(1));
      cancellation.Dispose();
    }

    [Test] public void Concurrent_prefab_requests_share_one_owned_load_and_release_once()
    {
      var loader = new DeferredLoader(); var cache = new MenuPrefabCache(loader);
      var first = cache.LoadAsync("shared", default).Preserve();
      var second = cache.LoadAsync("shared", default).Preserve();
      loader.Source.TrySetResult(prefab);
      Assert.That(first.GetAwaiter().GetResult(), Is.SameAs(prefab));
      Assert.That(second.GetAwaiter().GetResult(), Is.SameAs(prefab));
      cache.Dispose(); cache.Dispose();
      Assert.That(loader.Loads, Is.EqualTo(1)); Assert.That(loader.Releases, Is.EqualTo(1));
    }

    [Test] public void Throwing_outgoing_presenter_unbind_still_destroys_it_and_releases_its_input_lease()
    {
      UseLifecyclePrefab();
      var replacement = ReplacementProfile();
      using var gameplay = new InputAction("Gameplay", InputActionType.Button);
      gameplay.Enable();
      using var ownership = new MenuInputOwnership();
      using var host = new ContextMenuHostService(config, inputOwnership: ownership);
      var branch = new MenuAction("branch", new MenuLabel("Branch"),
        children: new[] { new MenuAction("leaf", new MenuLabel("Leaf")) }, presentationOverride: replacement);
      host.OpenAsync(new MenuOpenRequest(new[] { branch }, new MenuContext(), canvasOwner.GetComponent<Canvas>(),
        Vector2.zero, gameplayActions: new[] { gameplay })).GetAwaiter().GetResult();
      var previous = host.Presenter.Root.gameObject;
      MenuHostLifecyclePresenter.Unbinding = item =>
      { if (item.gameObject == previous) throw new InvalidOperationException("outgoing unbind failure"); };
      host.Session.EnterSubmenuAsync(branch.Id).GetAwaiter().GetResult();
      host.Close();
      Assert.That(previous == null, Is.True, "Throwing Unbind orphaned the outgoing presenter instance");
      Assert.That(gameplay.enabled, Is.True, "Throwing Unbind orphaned the outgoing gameplay input lease");
    }

    [Test] public void Throwing_partial_presenter_unbind_preserves_bind_error_and_destroys_partial_instance()
    {
      UseLifecyclePrefab();
      MenuHostLifecyclePresenter.Binding = _ => throw new InvalidOperationException("original bind failure");
      MenuHostLifecyclePresenter.Unbinding = _ => throw new InvalidOperationException("cleanup unbind failure");
      using var host = new ContextMenuHostService(config);
      var error = Assert.Throws<InvalidOperationException>(() => host.OpenAsync(Request()).GetAwaiter().GetResult());
      Assert.That(error.Message, Does.Contain("original bind failure"), "Cleanup replaced the meaningful configuration failure");
      Assert.That(canvasOwner.transform.childCount, Is.Zero, "Throwing cleanup left a partially bound menu GameObject");
      Assert.That(host.IsOpen, Is.False);
    }

    [UnityTest] public IEnumerator Worker_completed_children_provider_switches_profile_on_the_main_thread()
    {
      UseLifecyclePrefab();
      var replacement = ReplacementProfile();
      var source = new UniTaskCompletionSource<IReadOnlyList<MenuAction>>();
      var branch = new MenuAction("branch", new MenuLabel("Branch"),
        childrenProvider: new DelegateMenuChildrenProvider((_, __) => source.Task), presentationOverride: replacement);
      using var host = new ContextMenuHostService(config);
      host.OpenAsync(new MenuOpenRequest(new[] { branch }, new MenuContext(), canvasOwner.GetComponent<Canvas>(), Vector2.zero)).GetAwaiter().GetResult();
      var previous = host.Presenter.Root.gameObject;
      var entered = host.Session.EnterSubmenuAsync(branch.Id).Preserve();
      var children = new[] { new MenuAction("child", new MenuLabel("Child")) };
      var worker = Task.Run(() => source.TrySetResult(children));
      float deadline = Time.realtimeSinceStartup + 3;
      while (!worker.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
      Assert.That(worker.IsCompleted, Is.True, "Worker provider did not complete");
      worker.GetAwaiter().GetResult();
      while ((!host.IsOpen || !(host.Presenter is MenuHostLifecyclePresenter presenter) || presenter.Profile != replacement)
          && Time.realtimeSinceStartup < deadline) yield return null;
      entered.GetAwaiter().GetResult();
      Assert.That(host.IsOpen, Is.True);
      Assert.That(((MenuHostLifecyclePresenter)host.Presenter).Profile, Is.SameAs(replacement), "Worker completion left profile replacement stuck");
      Assert.That(previous == null, Is.True);
    }

    [Test] public void Reentrant_OnEnable_does_not_subscribe_the_replacement_presenter_twice()
    {
      UseLifecyclePrefab();
      prefab.SetActive(false);
      var replacement = ReplacementProfile();
      using var host = new ContextMenuHostService(config);
      bool replaced = false;
      MenuHostLifecyclePresenter.Enabled = _ =>
      {
        if (replaced) return;
        replaced = true;
        host.OpenAsync(Request(replacement)).GetAwaiter().GetResult();
      };
      host.OpenAsync(Request()).GetAwaiter().GetResult();
      Assert.That(replaced, Is.True);
      var current = (MenuHostLifecyclePresenter)host.Presenter;
      Assert.That(current.Profile, Is.SameAs(replacement));
      Assert.That(current.ContainmentSubscribers, Is.EqualTo(1), "Outer activation subscribed a newer install a second time");
      host.Close();
      Assert.That(current.ContainmentSubscribers, Is.Zero);
    }

    [Test] public void Request_anchor_and_live_Back_ownership_are_exposed_to_the_custom_presenter()
    {
      UseLifecyclePrefab();
      bool allowBack = false;
      using var host = new ContextMenuHostService(config);
      host.OpenAsync(new MenuOpenRequest(new[] { new MenuAction("leaf", new MenuLabel("Leaf")) }, new MenuContext(),
        canvasOwner.GetComponent<Canvas>(), Vector2.zero, anchorOverride: MenuAnchorPolicy.BottomCenter,
        backInputAllowed: () => allowBack)).GetAwaiter().GetResult();
      var context = ((MenuHostLifecyclePresenter)host.Presenter).Context;
      Assert.That(context.ResolveAnchor(MenuAnchorPolicy.Centered), Is.EqualTo(MenuAnchorPolicy.BottomCenter));
      Assert.That(context.CanHandleBackInput, Is.False);
      allowBack = true;
      Assert.That(context.CanHandleBackInput, Is.True, "The request predicate was snapshotted instead of evaluated live");
    }

    [Test] public void Semantic_Back_obeys_the_request_ownership_predicate_before_calling_custom_presenter()
    {
      UseLifecyclePrefab();
      var asset = ScriptableObject.CreateInstance<InputActionAsset>(); extras.Add(asset);
      var back = asset.AddActionMap("Menu").AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
      var reference = InputActionReference.Create(back); extras.Add(reference);
      profile.InputBindings.Configure(back: reference);
      var previousKeyboard = Keyboard.current;
      var keyboard = InputSystem.AddDevice<Keyboard>();
      const string playerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
      var settings = InputSystem.settings;
      var previousMode = settings.updateMode;
      bool previousFeature = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(settings, new object[] { playerUpdates });
      bool allowBack = false;
      try
      {
        settings.SetInternalFeatureFlag(playerUpdates, true);
        settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        using var host = new ContextMenuHostService(config);
        host.OpenAsync(new MenuOpenRequest(new[] { new MenuAction("leaf", new MenuLabel("Leaf")) }, new MenuContext(),
          canvasOwner.GetComponent<Canvas>(), Vector2.zero, backInputAllowed: () => allowBack)).GetAwaiter().GetResult();
        var current = (MenuHostLifecyclePresenter)host.Presenter;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
        Assert.That(current.BackCalls, Is.Zero, "A menu Back input escaped its owning game/tool focus policy");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
        allowBack = true;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
        Assert.That(current.BackCalls, Is.EqualTo(1));
      }
      finally
      {
        InputSystem.RemoveDevice(keyboard);
        settings.SetInternalFeatureFlag(playerUpdates, previousFeature);
        settings.updateMode = previousMode;
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      }
    }

    private void UseLifecyclePrefab()
    {
      Object.DestroyImmediate(prefab);
      prefab = new GameObject("Owned lifecycle presenter", typeof(RectTransform), typeof(MenuHostLifecyclePresenter));
      profile.PresenterPrefab = prefab;
    }
    private MenuProfile ReplacementProfile()
    {
      var result = ScriptableObject.CreateInstance<MenuProfile>(); extras.Add(result);
      result.PresenterPrefab = prefab; return result;
    }

    private sealed class DeferredLoader : IRuntimeAssetService
    {
      public readonly UniTaskCompletionSource<GameObject> Source = new();
      public int Loads, Releases;
      public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : Object
      { Loads++; return (T)(Object)await Source.Task; }
      public void Release(Object asset) { Releases++; }
    }
  }
}
