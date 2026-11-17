using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Input.Interfaces;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.Theme;
using Serilog;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Hosting
{
  /// <summary>One independently owned contextual menu host. All public UI operations run on Unity's main thread.</summary>
  public sealed class ContextMenuHostService : IContextMenuHostService
  {
    private readonly MenuHostConfig config;
    private readonly MenuPrefabCache prefabs;
    private readonly IInputActionSource actions;
    private readonly IInputPromptService prompts;
    private readonly IThemeService themes;
    private readonly IInputRebinderService rebinder;
    private readonly MenuInputOwnership inputOwnership;
    private readonly MenuProfileActionSource profileActions;
    private readonly InputTarget inputTarget;
    private readonly bool unityOwner;
    private CancellationTokenSource pending;
    private CancellationTokenRegistration requestCancellation;
    private MenuInputRouter router;
    private MenuPresentationContext presentationContext;
    private MenuOpenRequest request;
    private GameObject instance, previousFocus;
    private MenuProfile rootProfile, activeProfile, logicalProfile;
    private long generation, presentationGeneration;
    private bool disposed, routingEnabled;
    private bool containmentScheduled;

    public ContextMenuHostService(MenuHostConfig config, IRuntimeAssetService assets = null,
      IInputActionSource actions = null, IInputPromptService prompts = null, IThemeService themes = null,
      IInputRebinderService rebinder = null, MenuInputOwnership inputOwnership = null)
    {
      this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
      this.actions = actions; this.prompts = prompts; this.themes = themes; this.rebinder = rebinder;
      this.inputOwnership = inputOwnership ?? MenuInputOwnership.Shared;
      unityOwner = PlayerLoopHelper.IsMainThread && PlayerLoopHelper.UnitySynchronizationContext != null;
      prefabs = new MenuPrefabCache(assets); inputTarget = new InputTarget(this);
      profileActions = new MenuProfileActionSource(actions);
    }

    public bool IsOpen => Session != null && Session.IsOpen;
    public MenuSession Session { get; private set; }
    public IContextMenuPresenter Presenter { get; private set; }

    public async UniTask PrewarmAsync(MenuProfile profile, CancellationToken cancellationToken = default)
    {
      ThrowIfDisposed(); Validate(profile);
      await ResolvePrefabAsync(profile, cancellationToken);
    }

    public async UniTask OpenAsync(MenuOpenRequest request, CancellationToken cancellationToken = default)
    {
      ThrowIfDisposed();
      if (request == null) throw new ArgumentNullException(nameof(request));
      var profile = request.Profile != null ? request.Profile : config.Resolve(request.ProfileName);
      Validate(profile);
      if (!request.Context.IsValid) return;
      long currentGeneration = ++generation;
      CancelPending();
      var retainedRouter = RetireCurrent(true);
      if (currentGeneration != generation) { retainedRouter?.Dispose(); return; }
      this.request = request; rootProfile = profile; logicalProfile = profile;
      previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
      pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.Context.CancellationToken,
        request.Canvas.GetCancellationTokenOnDestroy());
      var token = pending.Token;
      var opening = pending;
      try
      {
        var registration = token.Register(() => OnRequestCanceled(currentGeneration));
        // Register may synchronously close an already-canceled request, including a replacement
        // opened reentrantly by a close callback. Never overwrite that request's registration.
        if (disposed || currentGeneration != generation || !ReferenceEquals(pending, opening))
        { registration.Dispose(); return; }
        requestCancellation = registration;
        token.ThrowIfCancellationRequested();
        var prefab = await ResolvePrefabAsync(profile, token);
        if (currentGeneration != generation || disposed) return;
        if (request.Canvas == null || !request.Context.IsValid) { Close(); return; }
        profileActions.Prepare(profile.InputBindings);
        presentationContext = new MenuPresentationContext(request.Canvas, request.ScreenPosition, themes, prompts,
          request.LabelResolver, action => profileActions.ActiveSource?.ResolveRuntimeAction(action) ?? action,
          request.AnchorOverride, request.BackInputAllowed, inputOwnership);
        Session = new MenuSession();
        Session.Open(request.Actions, request.Context);
        Session.Closed += OnSessionClosed; Session.Changed += OnSessionChanged;
        await InstallPresenterAsync(prefab, profile, currentGeneration, token);
        if (currentGeneration == generation && request.ActivationHeld) router?.AdoptHeldActivation();
      }
      catch (OperationCanceledException) { if (currentGeneration == generation) Close(); }
      catch
      {
        if (currentGeneration == generation) Close();
        throw;
      }
      finally { retainedRouter?.Dispose(); }
    }

    private static void Validate(MenuProfile profile)
    {
      if (profile == null) throw new ArgumentException("No default or requested context-menu profile is configured.");
      if (!profile.Validate(out var diagnostic)) throw new ArgumentException(diagnostic, nameof(profile));
    }

    private async UniTask<GameObject> ResolvePrefabAsync(MenuProfile profile, CancellationToken cancellationToken)
    {
      try
      {
        if (profile.PresenterPrefab != null) return profile.PresenterPrefab;
        return await prefabs.LoadAsync(profile.PresenterReference?.AssetGUID, cancellationToken);
      }
      finally
      {
        if (unityOwner && !PlayerLoopHelper.IsMainThread) await UniTask.SwitchToMainThread();
      }
    }

    private async UniTask InstallPresenterAsync(GameObject prefab, MenuProfile profile, long openGeneration,
      CancellationToken cancellationToken, HashSet<MenuProfile> fallbackChain = null)
    {
      fallbackChain ??= new HashSet<MenuProfile>();
      if (!fallbackChain.Add(profile)) throw new ArgumentException("Context-menu fallback profiles contain a cycle.");
      if (prefab == null) throw new ArgumentException("The menu presenter prefab is missing or destroyed.");
      routingEnabled = false;
      long swap = ++presentationGeneration;
      GameObject nextInstance = null;
      IContextMenuPresenter nextPresenter = null;
      MenuInputRouter nextRouter = null;
      var oldPresenter = Presenter; var oldInstance = instance; var oldRouter = router;
      try
      {
        cancellationToken.ThrowIfCancellationRequested();
        var bindingSession = Session;
        var bindingRequest = request;
        if (disposed || openGeneration != generation || !IsOpen || bindingRequest == null) return;
        nextInstance = Object.Instantiate(prefab, bindingRequest.Canvas.transform, false);
        // Active prefab Awake/OnEnable callbacks can replace or close this request during Instantiate.
        // Retire that clone before it can bind the replacement's session or mutate its active profile.
        if (disposed || openGeneration != generation || swap != presentationGeneration
            || !ReferenceEquals(Session, bindingSession) || !ReferenceEquals(request, bindingRequest)
            || !IsOpen || cancellationToken.IsCancellationRequested) return;
        foreach (var component in nextInstance.GetComponents<MonoBehaviour>())
          if (component is IContextMenuPresenter match) { nextPresenter = match; break; }
        if (nextPresenter == null || nextPresenter.Root == null)
          throw new ArgumentException("Menu presenter prefab root requires IContextMenuPresenter and a RectTransform: " + prefab.name);
        activeProfile = profile;
        profileActions.Prepare(profile.InputBindings);
        nextPresenter.Bind(bindingSession, profile, presentationContext);
        if (disposed || openGeneration != generation || swap != presentationGeneration
            || !ReferenceEquals(Session, bindingSession) || !IsOpen || request == null) return;
        if (nextPresenter is IMenuViewportAwarePresenter viewport && !viewport.FitsViewport)
        {
          if (profile.FallbackProfile == null)
            throw new InvalidOperationException("Menu cannot fit the viewport. Configure an accessible fallback profile. " + viewport.PlacementDiagnostic);
          RetireVisual(nextPresenter, nextInstance, null, false);
          nextPresenter = null; nextInstance = null;
          var fallback = profile.FallbackProfile;
          Validate(fallback);
          var fallbackPrefab = await ResolvePrefabAsync(fallback, cancellationToken);
          if (openGeneration != generation || swap != presentationGeneration || !IsOpen) return;
          await InstallPresenterAsync(fallbackPrefab, fallback, openGeneration, cancellationToken, fallbackChain);
          return;
        }
        nextRouter = new MenuInputRouter(inputTarget, profile.InputBindings, profileActions.ActiveSource, inputOwnership,
          request.GameplayActions, profileActions.HasOwnedActions && rebinder is not IScopedInputRebinderService ? null : rebinder);
        if (openGeneration != generation || swap != presentationGeneration || !IsOpen) return;
        Presenter = nextPresenter; instance = nextInstance; router = nextRouter;
        var installedPresenter = nextPresenter;
        var installedInstance = nextInstance;
        nextPresenter = null; nextInstance = null; nextRouter = null;
        routingEnabled = true;
        try
        {
          installedInstance.SetActive(true);
          // OnEnable belongs to the custom prefab and may synchronously open another menu.
          if (disposed || openGeneration != generation || swap != presentationGeneration
              || !ReferenceEquals(Presenter, installedPresenter) || !IsOpen) return;
          if (installedPresenter is IMenuContainmentEvents events) events.ContainmentChanged += OnContainmentChanged;
        }
        finally { RetireVisual(oldPresenter, oldInstance, oldRouter, true); }
      }
      finally
      {
        RetireVisual(nextPresenter, nextInstance, nextRouter, false);
      }
    }

    private void OnSessionChanged()
    {
      if (!IsOpen || request == null) return;
      if (!PlayerLoopHelper.IsMainThread)
      {
        var observed = Session;
        long opened = generation;
        UniTask.Post(() =>
        {
          if (opened == generation && ReferenceEquals(Session, observed)) OnSessionChanged();
        });
        return;
      }
      var desired = rootProfile;
      var path = Session.PathActions;
      for (int i = 0; i < path.Count; i++)
      {
        if (path[i].PresentationOverride is MenuProfile selectedOverride) desired = selectedOverride;
      }
      if (desired != logicalProfile)
      {
        logicalProfile = desired;
        SwapProfileAsync(desired, generation).Forget();
      }
    }

    private async UniTask SwapProfileAsync(MenuProfile profile, long openGeneration)
    {
      long selection = ++presentationGeneration;
      try
      {
        routingEnabled = false;
        if (instance != null) instance.SetActive(false);
        Validate(profile);
        var token = pending != null ? pending.Token : CancellationToken.None;
        var prefab = await ResolvePrefabAsync(profile, token);
        if (disposed || openGeneration != generation || selection != presentationGeneration || !IsOpen) return;
        await InstallPresenterAsync(prefab, profile, openGeneration, token);
      }
      catch (OperationCanceledException) { }
      catch (Exception exception)
      {
        Log.ForContext<ContextMenuHostService>().Error(exception, "Failed to replace menu presentation with {Profile}", profile != null ? profile.name : "missing");
        if (openGeneration == generation) Close();
      }
    }

    private void OnSessionClosed()
    {
      var closed = Session;
      if (closed == null || closed.IsOpen) return;
      if (!PlayerLoopHelper.IsMainThread)
      {
        UniTask.Post(() => { if (ReferenceEquals(Session, closed)) Close(); });
        return;
      }
      Close();
    }

    private void OnRequestCanceled(long openGeneration)
    {
      if (!PlayerLoopHelper.IsMainThread)
      {
        UniTask.Post(() => OnRequestCanceled(openGeneration));
        return;
      }
      if (!disposed && openGeneration == generation) Close();
    }

    private void OnContainmentChanged()
    {
      if (containmentScheduled) return;
      containmentScheduled = true;
      var observed = Presenter;
      long observedGeneration = generation;
      UniTask.Post(() =>
      {
        containmentScheduled = false;
        if (disposed || observedGeneration != generation || !ReferenceEquals(observed, Presenter)
            || !IsOpen || !(observed is IMenuViewportAwarePresenter viewport) || viewport.FitsViewport) return;
        var fallback = activeProfile != null ? activeProfile.FallbackProfile : null;
        if (fallback != null) SwapProfileAsync(fallback, generation).Forget();
        else
        {
          Log.ForContext<ContextMenuHostService>().Warning("Menu dismissed because its minimum usable layout no longer fits: {Diagnostic}", viewport.PlacementDiagnostic);
          Close();
        }
      });
    }

    public void Close()
    {
      generation++; CancelPending(); RetireCurrent(false)?.Dispose();
    }

    private MenuInputRouter RetireCurrent(bool retainInput)
    {
      routingEnabled = false; presentationGeneration++;
      var oldSession = Session; var oldPresenter = Presenter; var oldInstance = instance;
      var oldRouter = router; var oldContext = presentationContext; var focus = previousFocus;
      Session = null; Presenter = null; instance = null; router = null; presentationContext = null;
      previousFocus = null; activeProfile = null; logicalProfile = null; request = null;
      if (oldPresenter is IMenuContainmentEvents oldEvents)
        Cleanup(() => oldEvents.ContainmentChanged -= OnContainmentChanged, "containment subscription");
      if (oldSession != null)
      {
        oldSession.Closed -= OnSessionClosed; oldSession.Changed -= OnSessionChanged;
        Cleanup(oldSession.Close, "session close");
        Cleanup(oldSession.Dispose, "session disposal");
      }
      try { if (oldPresenter != null) Cleanup(oldPresenter.Unbind, "presenter unbind"); }
      finally
      {
        if (oldInstance != null)
        {
          Cleanup(() => oldInstance.SetActive(false), "presenter deactivation");
          Cleanup(() => Destroy(oldInstance), "presenter destruction");
        }
        if (oldContext != null) Cleanup(oldContext.Dispose, "presentation context");
        if (!retainInput && oldRouter != null) Cleanup(oldRouter.Dispose, "input routing");
        if (focus != null && focus.activeInHierarchy && EventSystem.current != null)
          EventSystem.current.SetSelectedGameObject(focus);
      }
      return retainInput ? oldRouter : null;
    }

    private void CancelPending()
    {
      var registration = requestCancellation; requestCancellation = default;
      var old = pending; pending = null;
      registration.Dispose();
      if (old == null) return;
      old.Cancel(); old.Dispose();
    }

    private static void Destroy(GameObject target)
    { if (Application.isPlaying) Object.Destroy(target); else Object.DestroyImmediate(target); }
    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(ContextMenuHostService)); }
    private static void Cleanup(Action action, string stage)
    {
      try { action(); }
      catch (Exception exception) { Log.ForContext<ContextMenuHostService>().Error(exception, "Menu cleanup failed during {Stage}", stage); }
    }
    private void RetireVisual(IContextMenuPresenter presenter, GameObject visual, MenuInputRouter input, bool subscribed)
    {
      if (subscribed && presenter is IMenuContainmentEvents events)
        Cleanup(() => events.ContainmentChanged -= OnContainmentChanged, "retired containment subscription");
      if (presenter != null) Cleanup(presenter.Unbind, "retired presenter unbind");
      if (visual != null)
      {
        Cleanup(() => visual.SetActive(false), "retired presenter deactivation");
        Cleanup(() => Destroy(visual), "retired presenter destruction");
      }
      if (input != null) Cleanup(input.Dispose, "retired input routing");
    }
    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      try { Close(); }
      finally
      {
        Cleanup(prefabs.Dispose, "prefab ownership");
        Cleanup(profileActions.Dispose, "profile input actions");
      }
    }

    private sealed class InputTarget : IMenuInputTarget, IMenuPointerConfirmationTarget
    {
      private readonly ContextMenuHostService host;
      public InputTarget(ContextMenuHostService host) { this.host = host; }
      private IContextMenuPresenter Current => host.routingEnabled && host.IsOpen ? host.Presenter : null;
      public void Navigate(Vector2 value, bool analog) => Current?.Navigate(value, analog);
      public void Point(Vector2 value) => Current?.Point(value);
      public void Confirm() => Current?.Confirm();
      public void ConfirmPointer()
      {
        var current = Current;
        if (current is IMenuPointerConfirmationTarget pointer) pointer.ConfirmPointer();
        else current?.Confirm();
      }
      public void Back()
      {
        var current = Current;
        if (current == null) return;
        if (!(host.request?.BackInputAllowed?.Invoke() ?? true)) return;
        if (ReferenceEquals(current, Current)) current.Back();
      }
      public void Cancel() => Current?.Cancel();
      public void InvokeSecondary(int index) => Current?.InvokeSecondary(index);
      public void Page(int direction) => Current?.Page(direction);
      public void SwitchRing() => Current?.SwitchRing();
      public void BeginHold() => Current?.BeginHold();
      public void EndHold() => Current?.EndHold();
      public void EndPointerHold()
      {
        var current = Current;
        if (current is IMenuPointerConfirmationTarget pointer) pointer.EndPointerHold();
        else current?.EndHold();
      }
    }
  }
}
