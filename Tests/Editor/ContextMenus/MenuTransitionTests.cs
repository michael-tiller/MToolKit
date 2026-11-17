using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuTransitionTests
  {
    private readonly List<Object> objects = new List<Object>();
    private readonly List<MenuSession> sessions = new List<MenuSession>();
    private readonly List<MenuPresentationContext> contexts = new List<MenuPresentationContext>();

    [TearDown]
    public void Cleanup()
    {
      foreach (var session in sessions) session.Dispose(); sessions.Clear();
      foreach (var context in contexts) context.Dispose(); contexts.Clear();
      for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
      objects.Clear();
    }

    private T Asset<T>() where T : ScriptableObject
    { var value = ScriptableObject.CreateInstance<T>(); objects.Add(value); return value; }

    private ListMenuPresenter Presenter(MenuTransitionAsset transition, out MenuSession session,
      out MenuProfile profile, bool reducedMotion = false, float originalAlpha = .4f)
    {
      var canvasRoot = new GameObject("Transition test canvas", typeof(RectTransform), typeof(Canvas)); objects.Add(canvasRoot);
      var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
      ((RectTransform)canvas.transform).sizeDelta = new Vector2(1200, 900);
      var root = new GameObject("Transition test presenter", typeof(RectTransform), typeof(CanvasGroup));
      root.transform.SetParent(canvasRoot.transform, false); root.GetComponent<CanvasGroup>().alpha = originalAlpha;
      profile = Asset<MenuProfile>(); profile.Transition = transition; profile.ReducedMotion = reducedMotion;
      session = new MenuSession(); sessions.Add(session);
      var parent = new MenuAction("parent", new MenuLabel("Parent"), children: new[] { Command("child") });
      session.Open(new[] { parent, Command("leaf") }, new MenuContext());
      var context = new MenuPresentationContext(canvas, Vector2.zero, labelOverride: label => label.Fallback); contexts.Add(context);
      var presenter = root.AddComponent<ListMenuPresenter>(); presenter.Bind(session, profile, context);
      return presenter;
    }
    private static MenuAction Command(string id) => new MenuAction(id, new MenuLabel(id), new MenuCommandBehavior(_ => { }, MenuActionOutcome.Keep));

    [Test]
    public void OpenAndHierarchyTransitionsDoNotReplayForSelectionOnlyChanges()
    {
      var asset = Asset<RecordingTransitionAsset>();
      Presenter(asset, out var session, out _);
      Assert.That(asset.Created.Count, Is.EqualTo(1));
      var controller = asset.Created[0];
      Assert.That(controller.Started, Is.EqualTo(new[] { MenuTransitionKind.Open }));
      session.Select("leaf"); session.Select("parent"); session.Selected.NotifyChanged();
      Assert.That(controller.Started.Count, Is.EqualTo(1));
      session.EnterSubmenuAsync("parent").GetAwaiter().GetResult();
      Assert.That(controller.Started, Is.EqualTo(new[] { MenuTransitionKind.Open, MenuTransitionKind.Navigation }));
      session.GoBack();
      Assert.That(controller.Started.Count, Is.EqualTo(3));
    }

    [Test]
    public void InlineFoldoutChangesUseNavigationTransition()
    {
      var asset = Asset<RecordingTransitionAsset>();
      Presenter(asset, out var session, out _);
      session.ExpandAsync("parent").GetAwaiter().GetResult();
      session.Collapse("parent");
      Assert.That(asset.Created.Count, Is.EqualTo(1));
      Assert.That(asset.Created[0].Started, Is.EqualTo(new[] { MenuTransitionKind.Open, MenuTransitionKind.Navigation, MenuTransitionKind.Navigation }));
    }

    [Test]
    public void ReducedMotionCompletesImmediatelyWithoutStartingAsyncAnimation()
    {
      var asset = Asset<RecordingTransitionAsset>();
      Presenter(asset, out var session, out _, reducedMotion: true);
      Assert.That(asset.Created.Count, Is.EqualTo(1));
      var controller = asset.Created[0];
      Assert.That(controller.Started, Is.Empty);
      Assert.That(controller.Completed, Is.EqualTo(new[] { MenuTransitionKind.Open }));
      session.EnterSubmenuAsync("parent").GetAwaiter().GetResult();
      Assert.That(controller.Started, Is.Empty);
      Assert.That(controller.Completed.Count, Is.EqualTo(2));
    }

    [Test]
    public void RapidNavigationCancelsPriorWorkAndUnbindDisposesExactlyOnce()
    {
      var asset = Asset<RecordingTransitionAsset>(); asset.Deferred = true;
      var presenter = Presenter(asset, out var session, out _);
      Assert.That(asset.Created.Count, Is.EqualTo(1));
      var controller = asset.Created[0];
      var opening = controller.Tokens[0];
      session.EnterSubmenuAsync("parent").GetAwaiter().GetResult();
      Assert.That(opening.IsCancellationRequested, Is.True);
      var navigation = controller.Tokens[1];
      presenter.Unbind(); presenter.Unbind();
      Assert.That(navigation.IsCancellationRequested, Is.True);
      Assert.That(controller.DisposeCount, Is.EqualTo(1));
      foreach (var completion in controller.Pending) completion.TrySetResult();
      Assert.That(session.IsOpen, Is.True, "A visual controller must not close its caller-owned session.");
    }

    [Test]
    public void SharedAssetCreatesIndependentPerPresenterControllers()
    {
      var asset = Asset<RecordingTransitionAsset>(); asset.Deferred = true;
      var first = Presenter(asset, out _, out _);
      Presenter(asset, out var secondSession, out _);
      Assert.That(asset.Created.Count, Is.EqualTo(2));
      Assert.That(asset.Created[0], Is.Not.SameAs(asset.Created[1]));
      first.Unbind();
      Assert.That(asset.Created[0].Tokens[0].IsCancellationRequested, Is.True);
      Assert.That(asset.Created[1].Tokens[0].IsCancellationRequested, Is.False);
      Assert.That(secondSession.IsOpen, Is.True);
    }

    [Test]
    public void FadeImmediatePreservesAuthoredOpacityThemeColorAndTransform()
    {
      var root = new GameObject("Fade ownership", typeof(RectTransform), typeof(CanvasGroup), typeof(Image)); objects.Add(root);
      var group = root.GetComponent<CanvasGroup>(); group.alpha = .35f; group.interactable = false; group.blocksRaycasts = false;
      var image = root.GetComponent<Image>(); image.color = Color.magenta;
      root.transform.localPosition = new Vector3(17, 23, 0); root.transform.localScale = new Vector3(2, .5f, 1);
      var fade = Asset<CanvasGroupFadeTransition>();
      using var controller = fade.Create((RectTransform)root.transform);
      controller.CompleteImmediately(MenuTransitionKind.Open);
      Assert.That(group.alpha, Is.EqualTo(.35f));
      Assert.That(group.interactable, Is.False); Assert.That(group.blocksRaycasts, Is.False);
      Assert.That(image.color, Is.EqualTo(Color.magenta));
      Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(17, 23, 0)));
      Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(2, .5f, 1)));
    }

    [Test]
    public void FadeUnbindCancelsAndRestoresAuthoredAlpha()
    {
      var fade = Asset<CanvasGroupFadeTransition>(); fade.Duration = 60;
      var presenter = Presenter(fade, out _, out _, originalAlpha: .4f);
      var group = presenter.GetComponent<CanvasGroup>();
      Assert.That(group.alpha, Is.EqualTo(0));
      presenter.Unbind();
      Assert.That(group.alpha, Is.EqualTo(.4f));
    }

    [UnityTest]
    public IEnumerator CanceledFadeCannotOverwriteANewerBinding()
    {
      var fade = Asset<CanvasGroupFadeTransition>(); fade.Duration = 60;
      var presenter = Presenter(fade, out var session, out var profile, originalAlpha: .4f);
      var group = presenter.GetComponent<CanvasGroup>();
      presenter.Unbind();
      presenter.Bind(session, profile, contexts[0]);
      yield return null; yield return null;
      Assert.That(group.alpha, Is.LessThan(.4f), "An old canceled transition must not restore alpha over a newer in-flight fade.");
      presenter.Unbind();
      Assert.That(group.alpha, Is.EqualTo(.4f));
    }

    [TestCase(-1)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidFadeDurationProducesConfigurationDiagnostic(float duration)
    {
      var fade = Asset<CanvasGroupFadeTransition>(); fade.Duration = duration;
      Assert.That(fade.Validate(out var diagnostic), Is.False);
      Assert.That(diagnostic, Does.Contain(nameof(CanvasGroupFadeTransition.Duration)));
    }

    public sealed class RecordingTransitionAsset : MenuTransitionAsset
    {
      public bool Deferred;
      public readonly List<RecordingTransition> Created = new List<RecordingTransition>();
      public override IMenuTransition Create(RectTransform root)
      { var controller = new RecordingTransition(Deferred); Created.Add(controller); return controller; }
    }
    public sealed class RecordingTransition : IMenuTransition
    {
      private readonly bool deferred;
      public readonly List<MenuTransitionKind> Started = new List<MenuTransitionKind>();
      public readonly List<MenuTransitionKind> Completed = new List<MenuTransitionKind>();
      public readonly List<CancellationToken> Tokens = new List<CancellationToken>();
      public readonly List<UniTaskCompletionSource> Pending = new List<UniTaskCompletionSource>();
      public int DisposeCount;
      public RecordingTransition(bool deferred) => this.deferred = deferred;
      public UniTask PlayAsync(MenuTransitionKind kind, CancellationToken cancellationToken)
      {
        Started.Add(kind); Tokens.Add(cancellationToken);
        if (!deferred) return UniTask.CompletedTask;
        var completion = new UniTaskCompletionSource(); Pending.Add(completion); return completion.Task;
      }
      public void CompleteImmediately(MenuTransitionKind kind) => Completed.Add(kind);
      public void Dispose() { DisposeCount++; foreach (var completion in Pending) completion.TrySetCanceled(); }
    }
  }
}
