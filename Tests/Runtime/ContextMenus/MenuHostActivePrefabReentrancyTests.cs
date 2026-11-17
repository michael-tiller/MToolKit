using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Runtime.ContextMenus
{
  public sealed class MenuHostActivePrefabReentrancyTests
  {
    private readonly List<Object> owned = new();
    private ContextMenuHostService host;
    private MenuInputOwnership ownership;
    private InputAction gameplay;
    private Canvas canvas;
    private MenuProfile initialProfile;

    [SetUp]
    public void Setup()
    {
      Assert.That(Application.isPlaying, Is.True, "Active prefab lifecycle must run in actual Unity PlayMode.");
      Assert.That(PlayerLoopHelper.IsMainThread, Is.True);
      var canvasObject = Own(new GameObject("Reentrant menu canvas", typeof(RectTransform), typeof(Canvas)));
      canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      initialProfile = Profile("Initial active profile");
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = initialProfile;
      ownership = new MenuInputOwnership(); gameplay = new InputAction("Gameplay", InputActionType.Button); gameplay.Enable();
      host = new ContextMenuHostService(config, inputOwnership: ownership);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
      ActivePresenter.Enabling = null;
      host?.Dispose(); host = null;
      ownership?.Dispose(); ownership = null;
      gameplay?.Dispose(); gameplay = null;
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
      owned.Clear(); yield return null;
    }

    [UnityTest]
    public IEnumerator ActivePrefabOpeningReplacementDuringInstantiateNeverBindsTheRetiredInstanceOrOverwritesItsProfile()
    {
      var replacementProfile = Profile("Replacement active profile");
      var correctFallback = Profile("Replacement accessible fallback");
      initialProfile.FallbackProfile = Profile("Retired incorrect fallback");
      replacementProfile.FallbackProfile = correctFallback;
      var replacementContext = new MenuContext(new object());
      Probe retired = null; ActivePresenter replacementPresenter = null; MenuSession replacementSession = null;
      int reentries = 0;
      ActivePresenter.Enabling = presenter =>
      {
        reentries++; retired = presenter.Record;
        host.OpenAsync(Request("replacement", replacementProfile, replacementContext)).GetAwaiter().GetResult();
        replacementSession = host.Session; replacementPresenter = (ActivePresenter)host.Presenter;
      };

      yield return host.OpenAsync(Request("retired", initialProfile, new MenuContext(new object()))).ToCoroutine();

      Assert.That(reentries, Is.EqualTo(1), "The hook must run from the active clone's first OnEnable, once.");
      Assert.That(retired, Is.Not.Null);
      Assert.That(retired.BindCalls, Is.Zero, "The superseded instance must be fenced before binding any session or context.");
      Assert.That(host.Session, Is.SameAs(replacementSession)); Assert.That(host.Session.Context, Is.SameAs(replacementContext));
      Assert.That(host.Session.Selected.Id.Value, Is.EqualTo("replacement"));
      Assert.That(host.Presenter, Is.SameAs(replacementPresenter));
      Assert.That(replacementPresenter.Record.Profile, Is.SameAs(replacementProfile));
      Assert.That(replacementPresenter.Record.BindCalls, Is.EqualTo(1));
      Assert.That(gameplay.enabled, Is.False);

      // Exercise the host's active profile through public containment behavior, rather than reading its private field.
      replacementPresenter.ReportInsufficientSpace();
      yield return null; yield return null;
      Assert.That(((ActivePresenter)host.Presenter).Record.Profile, Is.SameAs(correctFallback),
        "The retired installer overwrote the replacement's profile and selected the wrong accessible fallback.");
      Assert.That(host.Session, Is.SameAs(replacementSession)); Assert.That(host.Session.IsOpen, Is.True);
      host.Close(); Assert.That(gameplay.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator ActivePrefabClosingDuringInstantiateNeverBindsAfterCloseAndDestroysTheUninstalledClone()
    {
      Probe retired = null; ActivePresenter retiredPresenter = null; int reentries = 0;
      ActivePresenter.Enabling = presenter =>
      {
        reentries++; retired = presenter.Record; retiredPresenter = presenter; host.Close();
      };

      yield return host.OpenAsync(Request("retired", initialProfile, new MenuContext(new object()))).ToCoroutine();

      Assert.That(reentries, Is.EqualTo(1)); Assert.That(retired, Is.Not.Null);
      Assert.That(retired.BindCalls, Is.Zero, "A clone retired by its OnEnable must never bind a null or replacement session.");
      Assert.That(host.IsOpen, Is.False); Assert.That(host.Session, Is.Null); Assert.That(host.Presenter, Is.Null);
      Assert.That(gameplay.enabled, Is.True);
      yield return null;
      Assert.That(retiredPresenter == null, Is.True, "The uninstalled clone was leaked after reentrant Close.");
      Assert.That(retired.DestroyCalls, Is.EqualTo(1));
    }

    private MenuProfile Profile(string name)
    {
      var template = Own(new GameObject(name + " template", typeof(RectTransform), typeof(ActivePresenter)));
      Assert.That(template.activeInHierarchy, Is.True, "The fixture must exercise Instantiate callbacks, not deferred SetActive.");
      var profile = Own(ScriptableObject.CreateInstance<MenuProfile>()); profile.name = name; profile.PresenterPrefab = template;
      return profile;
    }
    private MenuOpenRequest Request(string id, MenuProfile profile, MenuContext context)
      => new MenuOpenRequest(new[] { new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(_ => { })) }, context, canvas, Vector2.zero,
        profile: profile, gameplayActions: new[] { gameplay });
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    public sealed class Probe
    {
      public int BindCalls, DestroyCalls;
      public MenuProfile Profile;
    }
    public sealed class ActivePresenter : MonoBehaviour, IContextMenuPresenter, IMenuViewportAwarePresenter, IMenuContainmentEvents
    {
      public static Action<ActivePresenter> Enabling;
      private Probe record;
      public Probe Record => record ??= new Probe();
      public RectTransform Root => (RectTransform)transform;
      public bool FitsViewport { get; private set; } = true;
      public string PlacementDiagnostic => FitsViewport ? null : "Fixture viewport became too small";
      public event Action ContainmentChanged;
      private void OnEnable()
      {
        var callback = Enabling; Enabling = null; callback?.Invoke(this);
      }
      private void OnDestroy() => Record.DestroyCalls++;
      public void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context)
      { Record.BindCalls++; Record.Profile = profile; }
      public void ReportInsufficientSpace() { FitsViewport = false; ContainmentChanged?.Invoke(); }
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
  }
}
