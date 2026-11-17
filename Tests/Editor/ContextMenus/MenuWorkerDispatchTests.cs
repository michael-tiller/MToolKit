using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus;
using MToolKit.ContextMenus.Authoring;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Hosting;
using MToolKit.ContextMenus.Presentation;
using MToolKit.Runtime.AssetLoader.Interfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.ContextMenus
{
  public sealed class MenuWorkerChildrenProvider : MenuChildrenProviderSO
  {
    public readonly UniTaskCompletionSource<IReadOnlyList<MenuAction>> Source = new();
    public override UniTask<IReadOnlyList<MenuAction>> ResolveAsync(MenuContext context, CancellationToken token)
      => Source.Task;
  }

  public sealed class MenuWorkerProbePresenter : MonoBehaviour, IContextMenuPresenter
  {
    public static Action Bound;
    public RectTransform Root => (RectTransform)transform;
    public void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context)
    {
      Bound?.Invoke();
      Root.anchoredPosition = new Vector2(17, 29); // Real Unity ownership boundary, not a simulated thread flag.
    }
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

  /// <summary>Draft native ownership regressions. Must run in Unity; these do not use a portable/stubbed runner.</summary>
  public sealed class MenuWorkerDispatchTests
  {
    private const float Timeout = 3;
    private readonly List<Object> owned = new();
    private readonly ConcurrentQueue<int> states = new(), changes = new();
    private int ownerThread;
    private GameObject target;
    private MenuSession session;

    [SetUp]
    public void Setup()
    {
      ownerThread = Thread.CurrentThread.ManagedThreadId;
      Assert.That(PlayerLoopHelper.IsMainThread, Is.True, "This fixture requires Unity's real initialized owner thread");
      target = Own(new GameObject("Native menu state probe"));
      session = new MenuSession();
    }
    [TearDown]
    public void Cleanup()
    {
      MenuWorkerProbePresenter.Bound = null;
      session?.Dispose(); session = null;
      for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
      owned.Clear();
      while (states.TryDequeue(out _)) { }
      while (changes.TryDequeue(out _)) { }
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private MenuActionState ReadNativeState(MenuContext _)
    {
      states.Enqueue(Thread.CurrentThread.ManagedThreadId);
      string actualName = target.name; // Throws off the Unity thread in a native Editor, exercising game state delegates.
      return new MenuActionState(enabled: actualName.Length > 0);
    }
    private void ObserveSession()
      => session.Changed += () => changes.Enqueue(Thread.CurrentThread.ManagedThreadId);
    private void ClearObservations()
    { while (states.TryDequeue(out _)) { } while (changes.TryDequeue(out _)) { } }
    private void AssertOwnerCallbacks()
    {
      foreach (int thread in states) Assert.That(thread, Is.EqualTo(ownerThread), "Action state was evaluated on the provider/event worker");
      foreach (int thread in changes) Assert.That(thread, Is.EqualTo(ownerThread), "Session Changed escaped its Unity owner");
    }

    [UnityTest]
    public IEnumerator Worker_provider_result_evaluates_children_and_publishes_navigation_on_Unity_owner()
    {
      var provider = Own(ScriptableObject.CreateInstance<MenuWorkerChildrenProvider>());
      var definition = Own(ScriptableObject.CreateInstance<MenuSubmenuDefinitionSO>());
      definition.Id = "branch"; definition.Label = "Branch"; definition.Provider = provider;
      var context = new MenuContext(target, () =>
      {
        states.Enqueue(Thread.CurrentThread.ManagedThreadId);
        return target.name.Length > 0; // Includes authored ScopedProvider's own post-await validity check.
      });
      var branch = definition.Create(context);
      session.Open(new[] { branch }, context); ObserveSession();
      var observed = Observe(session.EnterSubmenuAsync(branch.Id));
      var child = new MenuAction("child", new MenuLabel("Child"), new MenuCommandBehavior(_ => { }), state: ReadNativeState);
      var worker = Task.Run(() => provider.Source.TrySetResult(new[] { child }));
      yield return WaitFor(() => worker.IsCompleted && observed.Done, "Provider continuation did not finish");
      Assert.That(worker.Exception, Is.Null);
      Assert.That(observed.Error, Is.Null, observed.Error?.ToString());
      Assert.That(session.Path.Count, Is.EqualTo(1));
      Assert.That(session.Selected.Id.Value, Is.EqualTo("branch/child"));
      Assert.That(states.Count, Is.GreaterThan(0));
      Assert.That(changes.Count, Is.GreaterThan(0));
      AssertOwnerCallbacks();
    }

    [UnityTest]
    public IEnumerator Worker_refresh_provider_result_reconciles_live_state_on_Unity_owner()
    {
      UniTaskCompletionSource<IReadOnlyList<MenuAction>> pending = null;
      var initial = new MenuAction("initial", new MenuLabel("Initial"));
      var branch = new MenuAction("branch", new MenuLabel("Branch"),
        childrenProvider: new DelegateMenuChildrenProvider((_, __) => pending == null
          ? UniTask.FromResult<IReadOnlyList<MenuAction>>(new[] { initial }) : pending.Task));
      session.Open(new[] { branch }, new MenuContext());
      session.EnterSubmenuAsync(branch.Id).GetAwaiter().GetResult(); ObserveSession(); ClearObservations();
      pending = new UniTaskCompletionSource<IReadOnlyList<MenuAction>>();
      var observed = Observe(session.RefreshAsync());
      var replacement = new MenuAction("replacement", new MenuLabel("Replacement"), new MenuCommandBehavior(_ => { }), state: ReadNativeState);
      var worker = Task.Run(() => pending.TrySetResult(new[] { replacement }));
      yield return WaitFor(() => worker.IsCompleted && observed.Done, "Refresh reconciliation did not finish");
      Assert.That(worker.Exception, Is.Null);
      Assert.That(observed.Error, Is.Null, observed.Error?.ToString());
      Assert.That(session.Selected.Id.Value, Is.EqualTo("replacement"));
      Assert.That(states.Count, Is.GreaterThan(0));
      AssertOwnerCallbacks();
    }

    [UnityTest]
    public IEnumerator Worker_command_outcome_refreshes_live_state_and_resets_execution_on_Unity_owner()
    {
      var completion = new UniTaskCompletionSource<MenuActionOutcome>();
      int invocations = 0;
      var action = new MenuAction("command", new MenuLabel("Command"),
        new MenuCommandBehavior((_, __) => { invocations++; return completion.Task; }), state: ReadNativeState);
      session.Open(new[] { action }, new MenuContext()); ObserveSession(); ClearObservations();
      var observed = Observe(session.ActivateAsync());
      var worker = Task.Run(() => completion.TrySetResult(MenuActionOutcome.Refresh));
      yield return WaitFor(() => worker.IsCompleted && observed.Done, "Post-command refresh did not finish");
      Assert.That(worker.Exception, Is.Null);
      Assert.That(observed.Error, Is.Null, observed.Error?.ToString());
      Assert.That(session.IsExecuting, Is.False);
      Assert.That(session.IsOpen, Is.True);
      Assert.That(invocations, Is.EqualTo(1));
      Assert.That(observed.CompletionThread, Is.EqualTo(ownerThread));
      AssertOwnerCallbacks();
    }

    [UnityTest]
    public IEnumerator Worker_descriptor_change_runs_actual_native_state_and_refresh_callbacks_on_Unity_owner()
    {
      var action = new MenuAction("changed", new MenuLabel("Changed"), new MenuCommandBehavior(_ => { }), state: ReadNativeState);
      session.Open(new[] { action }, new MenuContext()); ObserveSession(); ClearObservations();
      var worker = Task.Run(action.NotifyChanged);
      yield return WaitFor(() => worker.IsCompleted, "Descriptor notification worker did not complete");
      yield return null; yield return null;
      Assert.That(worker.Exception, Is.Null);
      Assert.That(changes.Count, Is.GreaterThan(0), "The worker notification was dropped instead of refreshing the open menu");
      Assert.That(states.Count, Is.GreaterThan(0));
      AssertOwnerCallbacks();
    }

    [UnityTest] public IEnumerator Worker_invalidation_retires_on_owner_and_stale_post_cannot_close_replacement()
      => WorkerClose(false);
    [UnityTest] public IEnumerator Worker_cancellation_retires_on_owner_and_stale_post_cannot_close_replacement()
      => WorkerClose(true);

    private IEnumerator WorkerClose(bool cancel)
    {
      using var cancellation = new CancellationTokenSource();
      var context = new MenuContext(cancellationToken: cancellation.Token);
      int closedThread = 0, disposalThread = 0;
      session.Open(new[] { new MenuAction("old", new MenuLabel("Old")) }, context,
        new DelegateLease(() => { disposalThread = Thread.CurrentThread.ManagedThreadId; target.SetActive(false); }));
      session.Closed += () => closedThread = Thread.CurrentThread.ManagedThreadId;
      ObserveSession(); ClearObservations();
      var worker = Task.Run(() => { if (cancel) cancellation.Cancel(); else context.Invalidate(); });
      // Deliberately do not yield between worker posting and replacement. This is the bounded
      // race being tested: a queued old Close must not retire a newer menu generation.
      Assert.That(((IAsyncResult)worker).AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(Timeout)), Is.True, "Worker Close callback blocked waiting for Unity");
      Assert.That(worker.Exception, Is.Null, worker.Exception?.ToString());
      session.Open(new[] { new MenuAction("new", new MenuLabel("New"), new MenuCommandBehavior(_ => { })) }, new MenuContext());
      yield return null; yield return null;
      Assert.That(session.IsOpen, Is.True);
      Assert.That(session.Selected.Id.Value, Is.EqualTo("new"));
      Assert.That(disposalThread, Is.EqualTo(ownerThread), "Input/focus cleanup callback escaped the session owner");
      Assert.That(closedThread, Is.EqualTo(ownerThread));
      AssertOwnerCallbacks();
    }

    [UnityTest]
    public IEnumerator Worker_asset_success_instantiates_and_binds_the_custom_presenter_on_Unity_owner()
    {
      var loader = new WorkerLoader();
      var profile = Profile();
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = profile;
      var canvas = Own(new GameObject("Asset worker canvas", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
      var prefab = Own(new GameObject("Asset worker presenter", typeof(RectTransform), typeof(MenuWorkerProbePresenter)));
      int boundThread = 0;
      MenuWorkerProbePresenter.Bound = () => boundThread = Thread.CurrentThread.ManagedThreadId;
      using var host = new ContextMenuHostService(config, loader);
      var observed = Observe(host.OpenAsync(Request(canvas)));
      var worker = Task.Run(() => loader.Source.TrySetResult(prefab));
      yield return WaitFor(() => worker.IsCompleted && observed.Done, "Worker asset open did not finish");
      Assert.That(observed.Error, Is.Null, observed.Error?.ToString());
      Assert.That(host.IsOpen, Is.True);
      Assert.That(boundThread, Is.EqualTo(ownerThread));
      Assert.That(observed.CompletionThread, Is.EqualTo(ownerThread));
    }

    [UnityTest]
    public IEnumerator Worker_asset_failure_returns_original_error_and_completes_cleanup_on_Unity_owner()
    {
      var loader = new WorkerLoader();
      var profile = Profile();
      var config = Own(ScriptableObject.CreateInstance<MenuHostConfig>()); config.DefaultProfile = profile;
      var canvas = Own(new GameObject("Failed worker canvas", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
      using var host = new ContextMenuHostService(config, loader);
      var failure = new InvalidOperationException("actual worker loader failure");
      var observed = Observe(host.OpenAsync(Request(canvas)));
      var worker = Task.Run(() => loader.Source.TrySetException(failure));
      yield return WaitFor(() => worker.IsCompleted && observed.Done, "Worker asset failure did not finish");
      Assert.That(observed.Error, Is.SameAs(failure));
      Assert.That(host.IsOpen, Is.False);
      Assert.That(canvas.transform.childCount, Is.Zero);
      Assert.That(observed.CompletionThread, Is.EqualTo(ownerThread), "Error/finally cleanup resumed on loader worker");
    }

    [UnityTest]
    public IEnumerator Disposed_cache_releases_a_worker_loaded_late_Unity_asset_on_its_owner_once()
    {
      var loader = new WorkerLoader();
      var prefab = Own(new GameObject("Late worker asset"));
      using var cache = new MenuPrefabCache(loader);
      var observed = Observe(cache.LoadAsync("late", default).AsUniTask());
      cache.Dispose();
      Assert.That(observed.Done, Is.True);
      Assert.That(observed.Error, Is.InstanceOf<OperationCanceledException>());
      var worker = Task.Run(() => loader.Source.TrySetResult(prefab));
      yield return WaitFor(() => worker.IsCompleted, "Late asset loader worker did not finish");
      yield return null; yield return null;
      Assert.That(loader.ReleaseThread, Is.EqualTo(ownerThread), "Late cleanup called asset Release from the loader worker");
      Assert.That(loader.Releases, Is.EqualTo(1));
      Assert.That(loader.ReleasedName, Is.EqualTo(prefab.name));
    }

    private MenuProfile Profile()
    {
      var profile = Own(ScriptableObject.CreateInstance<MenuProfile>());
      profile.PresenterReference = new ContextMenuPrefabReference("cd64d652b237458c91ea063246164701");
      return profile;
    }
    private static MenuOpenRequest Request(Canvas canvas)
      => new(new[] { new MenuAction("root", new MenuLabel("Root")) }, new MenuContext(), canvas, Vector2.zero);
    private sealed class DelegateLease : IDisposable
    {
      private Action release;
      public DelegateLease(Action release) { this.release = release; }
      public void Dispose() { var previous = release; release = null; previous?.Invoke(); }
    }
    private sealed class WorkerLoader : IRuntimeAssetService
    {
      public readonly UniTaskCompletionSource<GameObject> Source = new();
      public int Releases, ReleaseThread;
      public string ReleasedName;
      public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : Object
        => (T)(Object)await Source.Task;
      public void Release(Object asset)
      {
        ReleaseThread = Thread.CurrentThread.ManagedThreadId;
        ReleasedName = asset.name; // Native Unity access proves this is a real cleanup ownership boundary.
        Releases++;
      }
    }
    private sealed class Observation
    {
      public volatile bool Done;
      public Exception Error;
      public int CompletionThread;
    }
    private static Observation Observe(UniTask operation)
    {
      var observation = new Observation(); ObserveAsync(operation, observation).Forget(); return observation;
    }
    private static async UniTask ObserveAsync(UniTask operation, Observation observation)
    {
      try { await operation; }
      catch (Exception exception) { observation.Error = exception; }
      finally { observation.CompletionThread = Thread.CurrentThread.ManagedThreadId; observation.Done = true; }
    }
    private static IEnumerator WaitFor(Func<bool> condition, string diagnostic)
    {
      float deadline = Time.realtimeSinceStartup + Timeout;
      while (!condition())
      {
        if (Time.realtimeSinceStartup >= deadline) Assert.Fail(diagnostic + " (" + Timeout + "s)");
        yield return null;
      }
    }
  }
}
