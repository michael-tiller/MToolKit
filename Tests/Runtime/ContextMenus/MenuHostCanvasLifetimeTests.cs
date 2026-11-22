using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Runtime.ContextMenus
{
  public sealed class MenuHostCanvasLifetimeTests
  {
    private readonly List<Object> owned = new();
    private ContextMenuHostService host;
    private MenuInputOwnership ownership;
    private MenuProfile profile;
    private InputAction gameplay;
    private Canvas canvas;
    private GameObject template;

    [SetUp]
    public void Setup()
    {
      Assert.That(Application.isPlaying, Is.True, "Canvas OnDestroy must be validated in actual PlayMode.");
      Assert.That(PlayerLoopHelper.IsMainThread, Is.True);
      canvas = NewCanvas("Opening canvas");
      template = Own(new GameObject("Lifetime presenter template", typeof(RectTransform), typeof(ProbePresenter)));
      profile = Own(ScriptableObject.CreateInstance<MenuProfile>()); profile.PresenterPrefab = template;
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = profile;
      ownership = new MenuInputOwnership();
      gameplay = new InputAction("Standalone gameplay", InputActionType.Button); gameplay.Enable();
      host = new ContextMenuHostService(config, inputOwnership: ownership);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
      host?.Dispose(); host = null;
      ownership?.Dispose(); ownership = null;
      gameplay?.Dispose(); gameplay = null;
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
      owned.Clear();
      yield return null;
    }

    [UnityTest]
    public IEnumerator DestroyingOpeningCanvasClosesDiOwnedSessionAndRestoresInputWithoutSceneHost()
    {
      Assert.That(canvas.GetComponent<ContextMenuHost>(), Is.Null);
      yield return host.OpenAsync(Request(canvas)).ToCoroutine();
      var retired = host.Session;
      int closed = 0; retired.Closed += () => closed++;
      Assert.That(gameplay.enabled, Is.False);
      Object.Destroy(canvas.gameObject);
      yield return null;
      yield return null;
      Assert.That(host.IsOpen, Is.False, "Destroying only the visuals orphaned the DI-owned session.");
      Assert.That(retired.IsOpen, Is.False);
      Assert.That(host.Session, Is.Null); Assert.That(host.Presenter, Is.Null);
      Assert.That(gameplay.enabled, Is.True, "The destroyed canvas retained gameplay ownership.");
      Assert.That(closed, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator DestroyingRetiredCanvasCannotCloseReplacementOnAnotherCanvas()
    {
      var replacementCanvas = NewCanvas("Replacement canvas");
      yield return host.OpenAsync(Request(canvas)).ToCoroutine();
      var first = host.Session;
      yield return host.OpenAsync(Request(replacementCanvas)).ToCoroutine();
      var replacement = host.Session;
      Assert.That(first.IsOpen, Is.False);
      Object.Destroy(canvas.gameObject);
      yield return null;
      yield return null;
      Assert.That(host.Session, Is.SameAs(replacement)); Assert.That(replacement.IsOpen, Is.True);
      Assert.That(gameplay.enabled, Is.False);
      Object.Destroy(replacementCanvas.gameObject);
      yield return null;
      yield return null;
      Assert.That(host.IsOpen, Is.False);
      Assert.That(gameplay.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator WorkerCancellationAfterOpenClosesOnOwnerAndPreservesReentrantReplacement()
    {
      var replacementCanvas = NewCanvas("Reentrant replacement canvas");
      using var cancellation = new CancellationTokenSource();
      yield return host.OpenAsync(Request(canvas), cancellation.Token).ToCoroutine();
      var retired = host.Session;
      int ownerThread = Thread.CurrentThread.ManagedThreadId, closeThread = 0;
      MenuSession replacement = null;
      retired.Closed += () =>
      {
        closeThread = Thread.CurrentThread.ManagedThreadId;
        host.OpenAsync(Request(replacementCanvas)).GetAwaiter().GetResult();
        replacement = host.Session;
      };
      var worker = Task.Run(() => cancellation.Cancel());
      float deadline = Time.realtimeSinceStartup + 3;
      while ((!worker.IsCompleted || replacement == null) && Time.realtimeSinceStartup < deadline) yield return null;
      Assert.That(worker.IsCompleted, Is.True); worker.GetAwaiter().GetResult();
      Assert.That(retired.IsOpen, Is.False, "Post-open request cancellation has no live-session observer.");
      Assert.That(closeThread, Is.EqualTo(ownerThread));
      Assert.That(replacement, Is.Not.Null); Assert.That(host.Session, Is.SameAs(replacement));
      Assert.That(replacement.IsOpen, Is.True); Assert.That(gameplay.enabled, Is.False);
      Object.Destroy(canvas.gameObject);
      yield return null;
      Assert.That(host.Session, Is.SameAs(replacement));
      host.Close(); Assert.That(gameplay.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator DestroyingCanvasDuringLoadCannotInstallLateResultAndReleasesOwnedAsset()
    {
      var loader = new DeferredLoader();
      host.Dispose();
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = profile;
      profile.PresenterPrefab = null;
      profile.PresenterReference = new ContextMenuPrefabReference(Guid.NewGuid().ToString("N"));
      host = new ContextMenuHostService(config, loader, inputOwnership: ownership);
      var opening = host.OpenAsync(Request(canvas)).Preserve();
      Assert.That(loader.Loads, Is.EqualTo(1));
      Object.Destroy(canvas.gameObject);
      yield return null;
      yield return null;
      loader.Source.TrySetResult(template);
      yield return opening.ToCoroutine();
      Assert.That(host.IsOpen, Is.False); Assert.That(host.Presenter, Is.Null);
      host.Dispose();
      Assert.That(loader.Releases, Is.EqualTo(1));
      Assert.That(gameplay.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator DisposingHostDetachesRequestCancellationBeforeCanvasDestruction()
    {
      using var cancellation = new CancellationTokenSource();
      yield return host.OpenAsync(Request(canvas), cancellation.Token).ToCoroutine();
      var retired = host.Session;
      int closed = 0; retired.Closed += () => closed++;
      host.Dispose();
      Object.Destroy(canvas.gameObject);
      yield return null;
      var worker = Task.Run(() => cancellation.Cancel());
      while (!worker.IsCompleted) yield return null;
      worker.GetAwaiter().GetResult();
      Assert.That(closed, Is.EqualTo(1)); Assert.That(host.IsOpen, Is.False);
      Assert.That(gameplay.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator AlreadyCanceledOpenCannotCaptureTheReplacementCancellationRegistration()
    {
      using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
      yield return host.OpenAsync(Request(canvas), cancellation.Token).ToCoroutine();
      Assert.That(host.IsOpen, Is.False);
      var replacementCanvas = NewCanvas("Fresh canvas after canceled open");
      yield return host.OpenAsync(Request(replacementCanvas)).ToCoroutine();
      var replacement = host.Session;
      Object.Destroy(canvas.gameObject);
      yield return null;
      Assert.That(host.Session, Is.SameAs(replacement)); Assert.That(replacement.IsOpen, Is.True);
      Object.Destroy(replacementCanvas.gameObject);
      yield return null;
      yield return null;
      Assert.That(host.IsOpen, Is.False); Assert.That(gameplay.enabled, Is.True);
    }

    private MenuOpenRequest Request(Canvas target)
      => new MenuOpenRequest(new[] { new MenuAction("command", new MenuLabel("Command")) },
        new MenuContext(new object()), target, Vector2.zero, gameplayActions: new[] { gameplay });
    private Canvas NewCanvas(string name)
    {
      var go = Own(new GameObject(name, typeof(RectTransform), typeof(Canvas)));
      var result = go.GetComponent<Canvas>(); result.renderMode = RenderMode.ScreenSpaceOverlay; return result;
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    public sealed class ProbePresenter : MonoBehaviour, IContextMenuPresenter
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
    private sealed class DeferredLoader : IRuntimeAssetService
    {
      public readonly UniTaskCompletionSource<GameObject> Source = new();
      public int Loads, Releases;
      public async UniTask<T> LoadAsync<T>(string key, CancellationToken cancellationToken = default) where T : Object
      { Loads++; return (T)(Object)await Source.Task; }
      public void Release(Object asset) => Releases++;
    }
  }
}
