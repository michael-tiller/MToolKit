using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.UI.Scaling;
using MToolKit.Theme.Spacing;
using R3;
using Serilog;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using MToolKit.ContextMenus.Input;

namespace MToolKit.ContextMenus.Presentation
{
  [RequireComponent(typeof(RectTransform))]
  public abstract class MenuPresenterBase : MonoBehaviour, IContextMenuPresenter, IMenuPointerConfirmationTarget, IMenuViewportAwarePresenter, IMenuContainmentEvents, ICancelHandler
  {
    private readonly List<MenuWidgetInstance> items = new List<MenuWidgetInstance>();
    private IDisposable themes, prompts;
    private UserUiScaleCanvas scaleCanvas;
    private MenuWidgetInstance center, secondaryTemplate;
    private GameObject backdrop, footer;
    private TMP_Text backLabel, previousLabel, nextLabel;
    private Button previousButton, nextButton;
    private readonly List<FooterControl> footerControls = new();
    private Vector2 footerPosition;
    private float footerWidth, radialNavigationExtent, radialNavigationSpacing;
    private bool radialNavigation;
    private int navigationThemeDepth;
    protected Vector2 NavigationControlSize { get; private set; }
    private GameObject previousFocus;
    private bool refreshing, analogLatched, holding;
    private long bindingGeneration;
    private MenuNativeNavigationLease navigationLease;
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private PointerEventData pointerEvent;
    private MenuAction pointerHovered;
    private bool pointerConfirmation;
    private bool routedOutsideClick;
    private bool holdConfirmation;
    private bool widgetConfirmation;
    private IMenuTransition transition;
    private MenuTransitionAsset transitionAsset;
    private CancellationTokenSource transitionCancellation;
    private CancellationTokenSource selectionResetCancellation;
    private readonly List<MenuActionId> transitionPath = new List<MenuActionId>();
    private readonly List<MenuRow> transitionRows = new List<MenuRow>();
    private int transitionPage;
    private long transitionGeneration;
    private bool hasTransitionSnapshot, transitionReducedMotion;
    private MenuTransitionKind lastTransitionKind;
    protected bool PointerControlOwnsInput { get; private set; }
    protected GameObject PointerHitObject { get; private set; }
    protected bool NativeControlHasFocus => FocusedControl<ISubmitHandler>() != null;
    protected MenuSession Session { get; private set; }
    protected MenuProfile Profile { get; private set; }
    protected MenuPresentationContext Context { get; private set; }
    protected bool IsBound => Session != null && Session.IsOpen;
    /// <summary>A new binding has a new identity even when it reuses the session and context instances.</summary>
    protected long BindingGeneration => bindingGeneration;
    protected bool OwnsBinding(MenuSession observed, long generation)
      => this != null && generation == bindingGeneration && ReferenceEquals(Session, observed) && IsBound;
    public RectTransform Root => (RectTransform)transform;
    public bool FitsViewport { get; protected set; } = true;
    public string PlacementDiagnostic { get; protected set; }
    public int VisibleWidgetCount { get; protected set; }
    public event Action ContainmentChanged;
    protected void NotifyContainmentChanged() => ContainmentChanged?.Invoke();

    public virtual void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context)
    {
      Unbind();
      pointerHovered = null;
      if (session == null || profile == null || context == null) throw new ArgumentNullException("Menu presenter binding requires session, profile and context.");
      if (!profile.Validate(out var diagnostic, false)) throw new ArgumentException(diagnostic, nameof(profile));
      Session = session; Profile = profile; Context = context;
      bindingGeneration++;
      FitsViewport = true; PlacementDiagnostic = null;
      previousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
      navigationLease = MenuNativeNavigationLease.Acquire(EventSystem.current, Profile.InputBindings.UseNativeUguiNavigation, Context.InputOwnership);
      Session.Changed += OnChanged;
      Context.LabelsChanged += OnChanged;
      var themeService = MenuSkin.Service(context.Themes);
      if (themeService != null) themes = themeService.OnThemeUpdated.Subscribe(_ => OnChanged());
      if (context.Prompts != null) prompts = context.Prompts.Changed.Subscribe(_ => OnChanged());
      UserUiScaleCanvas.Attached += OnScaleAttached;
      AttachScaleCanvas(context.Canvas.GetComponent<UserUiScaleCanvas>());
      CreateBackdrop();
      if (!Profile.InputBindings.UseNativeUguiNavigation && EventSystem.current != null)
        EventSystem.current.SetSelectedGameObject(gameObject);
      Refresh(true);
    }

    public virtual void Unbind()
    {
      CancelSelectionReset();
      bindingGeneration++;
      // Detach ownership before custom callbacks can synchronously bind a replacement.
      var oldSession = Session; var oldContext = Context;
      var oldThemes = themes; var oldPrompts = prompts;
      var oldScale = scaleCanvas; var oldFocus = previousFocus;
      var oldItems = items.ToArray(); items.Clear();
      var oldCenter = center; var oldSecondary = secondaryTemplate;
      var oldBackdrop = backdrop; var oldFooter = footer;
      var oldFooterControls = footerControls.ToArray();
      var oldNavigation = navigationLease;
      var oldTransition = transition; var oldCancellation = transitionCancellation;
      Session = null; Profile = null; Context = null;
      themes = null; prompts = null; scaleCanvas = null; previousFocus = null;
      center = null; secondaryTemplate = null; backdrop = null; footer = null;
      backLabel = previousLabel = nextLabel = null;
      previousButton = nextButton = null;
      footerControls.Clear();
      NavigationControlSize = Vector2.zero;
      navigationLease = null; transition = null; transitionCancellation = null; transitionAsset = null;
      transitionGeneration++; hasTransitionSnapshot = false;
      transitionPath.Clear(); transitionRows.Clear();
      pointerEvent = null; pointerHits.Clear(); PointerControlOwnsInput = false;
      PointerHitObject = null; routedOutsideClick = false;
      VisibleWidgetCount = 0; holding = analogLatched = false;
      try
      {
        if (oldSession != null) CleanupOwned(() => oldSession.Changed -= OnChanged, "session subscription");
        if (oldContext != null) CleanupOwned(() => oldContext.LabelsChanged -= OnChanged, "label subscription");
        UserUiScaleCanvas.Attached -= OnScaleAttached;
        if (oldScale != null)
        {
          CleanupOwned(() => oldScale.ViewportChanged -= OnViewport, "viewport subscription");
          CleanupOwned(() => oldScale.ConstraintChanged -= OnViewport, "scale subscription");
        }
        if (oldCancellation != null)
        {
          CleanupOwned(oldCancellation.Cancel, "transition cancellation");
          CleanupOwned(oldCancellation.Dispose, "transition cancellation disposal");
        }
        if (oldTransition != null) CleanupOwned(oldTransition.Dispose, "transition disposal");
        if (oldThemes != null) CleanupOwned(oldThemes.Dispose, "theme subscription");
        if (oldPrompts != null) CleanupOwned(oldPrompts.Dispose, "prompt subscription");
        CleanupOwned(() =>
        {
          if (this == null || EventSystem.current == null) return;
          var selected = EventSystem.current.currentSelectedGameObject;
          if (selected != null && selected.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(oldFocus != null && oldFocus.activeInHierarchy ? oldFocus : null);
        }, "focus restoration");
        for (int i = 0; i < oldItems.Length; i++) CleanupOwned(oldItems[i].Dispose, "item widget disposal");
        if (oldCenter != null) CleanupOwned(oldCenter.Dispose, "center widget disposal");
        if (oldSecondary != null) CleanupOwned(oldSecondary.Dispose, "secondary template disposal");
        for (int i = 0; i < oldFooterControls.Length; i++)
        {
          var widget = oldFooterControls[i].Widget;
          if (widget != null) CleanupOwned(widget.Unbind, "navigation prompt disposal");
        }
        CleanupOwned(() => DestroyOwned(oldBackdrop), "backdrop destruction");
        CleanupOwned(() => DestroyOwned(oldFooter), "footer destruction");
      }
      finally
      {
        if (oldNavigation != null) CleanupOwned(oldNavigation.Dispose, "native navigation lease");
      }
    }

    private void CleanupOwned(Action cleanup, string stage)
    {
      try { cleanup(); }
      catch (Exception exception)
      { Log.ForContext(GetType()).Error(exception, "Menu presenter cleanup failed during {Stage}", stage); }
    }

    protected void Refresh(bool layout)
    {
      if (refreshing || !IsBound) return;
      if (!PlayerLoopHelper.IsMainThread)
      {
        long pendingGeneration = bindingGeneration;
        UniTask.Post(() => { if (this != null && pendingGeneration == bindingGeneration) Refresh(layout); });
        return;
      }
      long generation = bindingGeneration;
      bool fitted = FitsViewport;
      string diagnostic = PlacementDiagnostic;
      var focusedControl = FocusedControl<ISubmitHandler>();
      refreshing = true;
      try { UpdateBackdrop(); Render(layout); }
      finally { refreshing = false; }
      if (generation == bindingGeneration && IsBound && FitsViewport) UpdateTransition();
      if (generation == bindingGeneration && focusedControl != null && focusedControl.activeInHierarchy &&
          EventSystem.current != null && !EventSystem.current.alreadySelecting &&
          EventSystem.current.currentSelectedGameObject != focusedControl)
        EventSystem.current.SetSelectedGameObject(focusedControl);
      if (generation == bindingGeneration &&
          (fitted != FitsViewport || !string.Equals(diagnostic, PlacementDiagnostic, StringComparison.Ordinal)))
        ContainmentChanged?.Invoke();
    }

    private void UpdateTransition()
    {
      if (Profile.Transition == null) { ReleaseTransition(); return; }
      if (transitionAsset != Profile.Transition)
      {
        ReleaseTransition();
        transitionAsset = Profile.Transition;
        transition = transitionAsset.Create(Root)
          ?? throw new InvalidOperationException("Menu transition asset returned no per-presenter controller: " + transitionAsset.name);
      }
      bool changed = !hasTransitionSnapshot || transitionPage != Session.PageIndex ||
        transitionPath.Count != Session.Path.Count || transitionRows.Count != Session.Rows.Count;
      if (!changed)
      {
        for (int i = 0; i < transitionPath.Count; i++)
          if (transitionPath[i] != Session.Path[i]) { changed = true; break; }
        for (int i = 0; !changed && i < transitionRows.Count; i++)
          if (transitionRows[i].Action.Id != Session.Rows[i].Action.Id ||
              transitionRows[i].Depth != Session.Rows[i].Depth || transitionRows[i].Expanded != Session.Rows[i].Expanded)
            changed = true;
      }
      if (!changed)
      {
        if (Profile.ReducedMotion && !transitionReducedMotion)
        {
          CancelTransition();
          transition.CompleteImmediately(lastTransitionKind);
        }
        transitionReducedMotion = Profile.ReducedMotion;
        return;
      }
      lastTransitionKind = hasTransitionSnapshot ? MenuTransitionKind.Navigation : MenuTransitionKind.Open;
      transitionPath.Clear();
      for (int i = 0; i < Session.Path.Count; i++) transitionPath.Add(Session.Path[i]);
      transitionRows.Clear();
      for (int i = 0; i < Session.Rows.Count; i++) transitionRows.Add(Session.Rows[i]);
      transitionPage = Session.PageIndex;
      hasTransitionSnapshot = true;
      transitionReducedMotion = Profile.ReducedMotion;
      CancelTransition();
      if (Profile.ReducedMotion) { transition.CompleteImmediately(lastTransitionKind); return; }
      var cancellation = new CancellationTokenSource();
      transitionCancellation = cancellation;
      long current = ++transitionGeneration;
      RunTransitionAsync(transition, lastTransitionKind, cancellation, bindingGeneration, current).Forget();
    }

    private async UniTask RunTransitionAsync(IMenuTransition controller, MenuTransitionKind kind,
      CancellationTokenSource cancellation, long binding, long generation)
    {
      var token = cancellation.Token;
      try { await controller.PlayAsync(kind, token); }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
      catch (Exception exception)
      {
        await UniTask.SwitchToMainThread();
        if (this != null && binding == bindingGeneration && generation == transitionGeneration && ReferenceEquals(controller, transition))
        {
          Log.ForContext(GetType()).Warning(exception, "Menu {TransitionKind} transition failed; restoring its completed visual state", kind);
          controller.CompleteImmediately(kind);
        }
      }
      finally
      {
        if (binding == bindingGeneration && generation == transitionGeneration && ReferenceEquals(transitionCancellation, cancellation))
        { transitionCancellation = null; cancellation.Dispose(); }
      }
    }

    private void CancelTransition()
    {
      transitionGeneration++;
      var cancellation = transitionCancellation;
      transitionCancellation = null;
      if (cancellation == null) return;
      try { cancellation.Cancel(); }
      finally { cancellation.Dispose(); }
    }

    private void ReleaseTransition()
    {
      CancelTransition();
      var old = transition;
      transition = null; transitionAsset = null;
      hasTransitionSnapshot = false;
      transitionPath.Clear(); transitionRows.Clear();
      old?.Dispose();
    }

    protected abstract void Render(bool forceLayout);
    private void OnChanged()
    {
      if (!PlayerLoopHelper.IsMainThread)
      {
        long generation = bindingGeneration;
        var session = Session;
        UniTask.Post(() =>
        {
          if (generation == bindingGeneration && ReferenceEquals(session, Session)) OnChanged();
        });
        return;
      }
      // EditMode-created or never-enabled objects may be destroyed without Unity delivering OnDestroy.
      // A managed session callback must still retire its subscriptions without touching a dead component.
      if (this == null) { Unbind(); return; }
      if (Session != null && !Session.IsOpen)
      {
        Unbind();
        if (this != null) gameObject.SetActive(false);
        return;
      }
      Refresh(false);
    }
    protected virtual void OnRectTransformDimensionsChange() => Refresh(true);
    protected virtual void OnDisable() => Unbind();
    protected virtual void OnDestroy() => Unbind();

    private void OnScaleAttached(UserUiScaleCanvas canvas)
    { if (Context != null && canvas != null && Context.Canvas.transform == canvas.transform) AttachScaleCanvas(canvas); }
    private void AttachScaleCanvas(UserUiScaleCanvas canvas)
    {
      if (scaleCanvas != null) { scaleCanvas.ViewportChanged -= OnViewport; scaleCanvas.ConstraintChanged -= OnViewport; }
      scaleCanvas = canvas;
      if (scaleCanvas != null) { scaleCanvas.ViewportChanged += OnViewport; scaleCanvas.ConstraintChanged += OnViewport; }
    }
    private void OnViewport() => Refresh(true);

    protected MenuWidgetInstance Item(int index)
    {
      while (items.Count <= index) items.Add(MenuWidgetFactory.CreateItem(Root, Profile.ItemPrefab));
      return items[index];
    }
    protected void FinishItems(int visible)
    {
      VisibleWidgetCount = visible;
      for (int i = visible; i < items.Count; i++) items[i].Unbind();
      if (Profile.InputBindings.UseNativeUguiNavigation && EventSystem.current != null && visible > 0)
      {
        if (NativeControlHasFocus) return;
        for (int i = 0; i < visible; i++)
        {
          var widget = items[i].DefaultWidget;
          if (items[i].State?.Action != Session.Selected || widget?.Button == null || !widget.Button.interactable) continue;
          if (EventSystem.current.currentSelectedGameObject != widget.Button.gameObject && !EventSystem.current.alreadySelecting)
            EventSystem.current.SetSelectedGameObject(widget.Button.gameObject);
          break;
        }
      }
    }
    protected void ConfigureNavigation(MenuWidgetInstance item)
    {
      if (item.DefaultWidget?.Button == null) return;
      item.DefaultWidget.Button.enabled = !Profile.InputBindings.RoutePointerClick || PreferNativeItemPointer;
      var navigation = item.DefaultWidget.Button.navigation;
      navigation.mode = Profile.InputBindings.UseNativeUguiNavigation ? Navigation.Mode.Automatic : Navigation.Mode.None;
      item.DefaultWidget.Button.navigation = navigation;
    }

    protected MenuWidgetState StateFor(MenuAction action, bool selected, Action activate = null, Action select = null,
      bool secondary = false, InputActionReference bindingOverride = null)
    {
      var availability = action.Evaluate(Session.Context);
      IReadOnlyList<MenuWidgetState> secondaryStates = null;
      if (secondary && action.SecondaryActions.Count != 0)
      {
        var states = new List<MenuWidgetState>();
        for (int i = 0; i < action.SecondaryActions.Count; i++)
        {
          var auxiliary = action.SecondaryActions[i];
          if (!auxiliary.Evaluate(Session.Context).Visible) continue;
          int index = i;
          var hint = auxiliary.BindingHint ?? (index < Profile.InputBindings.Secondary.Count ? Profile.InputBindings.Secondary[index] : null);
          states.Add(StateFor(auxiliary, false, () => InvokeSecondary(index), bindingOverride: hint));
        }
        secondaryStates = states;
      }
      var bindingReference = bindingOverride;
      var bindingAction = Context.ResolveInputAction(bindingReference);
      string binding = Context.Prompts != null && bindingReference != null
        ? Context.Prompts.Resolve(bindingAction, InputPromptStyle.Glyph) : string.Empty;
      return new MenuWidgetState(action, availability, selected, Context.Resolve(action.Label), Context.Resolve(action.Description),
        Profile.Skin, Context.Themes, activate, () => SelectWidget(select), binding, secondaryStates,
        Context.Prompts, bindingAction, IsPointerHovered(action), entered => HoverWidget(action, entered, select), Profile.GetTheme(Context.Themes), HandlePointerExit);
    }

    protected bool IsPointerHovered(MenuAction action)
      => IsBound && Session.InteractionMode == MenuInteractionMode.Pointer && ReferenceEquals(pointerHovered, action);

    protected bool SetPointerHovered(MenuAction action)
    {
      var observed = Session; long generation = BindingGeneration;
      if (!OwnsBinding(observed, generation)) return false;
      bool interactive = action == null || action.Behavior.IsInteractive;
      if (!OwnsBinding(observed, generation)) return false;
      if (!interactive) action = null;
      if (ReferenceEquals(pointerHovered, action)) return false;
      pointerHovered = action;
      return true;
    }

    protected void ClearPointerHover(MenuAction action)
    {
      if (ReferenceEquals(pointerHovered, action) && SetPointerHovered(null)) Refresh(false);
    }

    protected void HandlePointerExit(Vector2 destination)
    {
      // uGUI exits the old object before entering the new one. Resolve the destination
      // instead of clearing on an intermediate exit or stealing keyboard/controller focus.
      if (IsBound && Session.InteractionMode == MenuInteractionMode.Pointer) Point(destination);
    }

    private void HoverWidget(MenuAction action, bool entered, Action select)
    {
      var observed = Session; long generation = BindingGeneration;
      if (!OwnsBinding(observed, generation)) return;
      if (!entered) { ClearPointerHover(action); return; }
      bool accept = AcceptNativePointerHover;
      if (!OwnsBinding(observed, generation) || !accept) return;
      CancelSelectionReset();
      if (!OwnsBinding(observed, generation)) return;
      var before = observed.Selected; var mode = observed.InteractionMode;
      bool changed = SetPointerHovered(action);
      if (!OwnsBinding(observed, generation)) return;
      observed.InteractionMode = MenuInteractionMode.Pointer;
      if (!OwnsBinding(observed, generation)) return;
      select?.Invoke();
      if (!OwnsBinding(observed, generation)) return;
      if (changed && ReferenceEquals(before, observed.Selected) && mode == observed.InteractionMode) Refresh(false);
    }

    private void SelectWidget(Action select)
    {
      var observed = Session; long generation = BindingGeneration;
      if (!OwnsBinding(observed, generation)) return;
      CancelSelectionReset();
      if (!OwnsBinding(observed, generation)) return;
      var before = observed.Selected; var mode = observed.InteractionMode;
      bool changed = SetPointerHovered(null);
      if (!OwnsBinding(observed, generation)) return;
      observed.InteractionMode = MenuInteractionMode.Keyboard;
      if (!OwnsBinding(observed, generation)) return;
      select?.Invoke();
      if (!OwnsBinding(observed, generation)) return;
      if (changed && ReferenceEquals(before, observed.Selected) && mode == observed.InteractionMode) Refresh(false);
    }

    protected void BindCenter(bool visible)
    {
      if (!visible || !Profile.ShowCenter || Session.Selected == null) { center?.Unbind(); return; }
      center ??= MenuWidgetFactory.CreateCenter(Root, Profile.CenterPrefab, Profile.SecondaryPrefab);
      if (center.DefaultWidget is MenuCenterWidget defaultCenter)
      {
        if (Profile.SecondaryPrefab == null && Session.Selected.SecondaryActions.Count > 0 && secondaryTemplate == null)
        {
          secondaryTemplate = MenuWidgetFactory.CreateItem(Root);
          secondaryTemplate.DefaultWidget.BackgroundRole = MenuVisualRole.Auxiliary;
          MenuWidgetFactory.Place(secondaryTemplate.Rect, Vector2.zero,
            new Vector2(160, Math.Max(36, Profile.MinimumTargetSize)));
          secondaryTemplate.GameObject.SetActive(false);
        }
        defaultCenter.SecondaryPrefab = Profile.SecondaryPrefab != null ? Profile.SecondaryPrefab : secondaryTemplate?.GameObject;
      }
      // Only framework-created roots receive default dimensions. Custom center roots preserve authored composition.
      if (center.DefaultWidget != null) MenuWidgetFactory.Place(center.Rect, Vector2.zero, new Vector2(170, 150));
      else center.Rect.anchoredPosition = Vector2.zero;
      center.Bind(StateFor(Session.Selected, true, secondary: true,
        bindingOverride: Session.Selected.BindingHint ?? Profile.InputBindings.Submit));

    }

    protected void SelectAction(MenuAction action)
    { CancelSelectionReset(); if (IsBound && action != null) Session.Select(action.Id); }
    protected virtual void ActivateAction(MenuAction action)
    {
      var observed = Session; long generation = BindingGeneration;
      if (!OwnsBinding(observed, generation) || action == null) return;
      bool selected = observed.Select(action.Id);
      if (OwnsBinding(observed, generation) && selected) ConfirmChosenWidget();
    }

    protected void ConfirmChosenWidget()
    {
      widgetConfirmation = true;
      try { Confirm(); }
      finally { widgetConfirmation = false; }
    }
    protected Action PointerActivation(MenuAction action)
      => Profile.InputBindings.RoutePointerClick && !PreferNativeItemPointer || !action.Behavior.IsInteractive ? null : () => ActivateAction(action);
    /// <summary>A native scrolling row may retain release/drag click ownership while navigation stays semantic.</summary>
    protected virtual bool PreferNativeItemPointer => false;
    protected virtual bool NativeItemPointerOwnsConfirmation() => false;
    protected virtual bool AcceptNativePointerHover => true;
    protected Color ColorFor(MenuVisualRole role, MenuThemeState state = MenuThemeState.Any)
      => Profile.GetTheme(Context.Themes).GetColor(role, state);

    private sealed class FooterControl
    {
      public MenuActionWidget Widget;
      public Action Activate;
      public string Label;
      public InputActionReference Binding;
      public bool Enabled = true, Hovered, Visible;
      public Vector2 Size, LabelSize, HintSize;
    }

    protected void SetFooter(Vector2 position, float width) => SetFooter(position, width, true);

    protected void SetFooter(Vector2 position, float width, bool showPagination)
    {
      radialNavigation = false;
      footerPosition = position; footerWidth = width;
      SetNavigationState(Session.PageIndex, showPagination ? Session.PageCount : 1);
    }

    protected void PositionFooter(Vector2 position)
    {
      footerPosition = position;
      if (!radialNavigation) LayoutNavigationControls();
    }

    /// <summary>Native radial controls retain the same semantic actions without a bottom paging strip.</summary>
    protected void SetRadialNavigation(float extent, float spacing, int pageIndex, int pageCount)
    {
      radialNavigation = true;
      radialNavigationExtent = extent; radialNavigationSpacing = Mathf.Max(0, spacing);
      SetNavigationState(pageIndex, pageCount);
    }

    private void SetNavigationState(int pageIndex, int pageCount)
    {
      if (!IsBound) return;
      if (footer == null)
      {
        footer = MenuWidgetFactory.Rect("Menu navigation", Root).gameObject;
        Button Button(string name, Action callback, out TMP_Text label)
        {
          var rect = MenuWidgetFactory.Rect(name, footer.transform);
          var image = rect.gameObject.AddComponent<Image>();
          var button = rect.gameObject.AddComponent<Button>();
          button.targetGraphic = image; button.transition = Selectable.Transition.None;
          var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
          var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
          layout.padding = new RectOffset(6, 6, 0, 0); layout.spacing = 6;
          layout.childAlignment = TextAnchor.MiddleCenter;
          layout.childControlWidth = layout.childControlHeight = true;
          layout.childForceExpandWidth = layout.childForceExpandHeight = false;
          var spacing = rect.gameObject.AddComponent<SpacingPreset>(); spacing.ConfigureTarget(layout);
          label = MenuWidgetFactory.Text(name + " label", rect, 16);
          label.gameObject.AddComponent<LayoutElement>();
          var hint = MenuWidgetFactory.Text(name + " binding", rect, 16);
          var hintLayout = hint.gameObject.AddComponent<LayoutElement>();
          hintLayout.minWidth = 28; hintLayout.preferredWidth = -1;
          var widget = rect.gameObject.AddComponent<MenuActionWidget>(); widget.Background = image;
          widget.BackgroundRole = MenuVisualRole.Auxiliary; widget.Configure(label, null, null, null, button);
          widget.BindingHint = hint;
          widget.SpacingTargets = new[] { spacing };
          footerControls.Add(new FooterControl { Widget = widget, Activate = callback, Label = name });
          return button;
        }
        Button("Back", Back, out backLabel);
        previousButton = Button("Previous", () => Page(-1), out previousLabel);
        nextButton = Button("Next", () => Page(1), out nextLabel);
      }
      var session = Session;
      long generation = bindingGeneration;
      bool restoreItemFocus = false;
      for (int i = 0; i < 3; i++)
      {
        var control = footerControls[i];
        control.Visible = i == 0 ? Profile.ShowBackControl : pageCount > 1;
        if (!control.Visible)
        {
          var events = navigationLease?.Owner != null ? navigationLease.Owner : EventSystem.current;
          var selected = events != null ? events.currentSelectedGameObject : null;
          if (selected != null && selected.transform.IsChildOf(control.Widget.transform))
          { events.SetSelectedGameObject(null); restoreItemFocus = true; }
          control.Widget.Unbind();
          if (!OwnsNavigation(control, session, generation)) return;
        }
        control.Widget.gameObject.SetActive(control.Visible);
        if (!OwnsNavigation(control, session, generation)) return;
      }
      footerControls[0].Label = Session.Path.Count == 0 ? "Close" : "Back";
      footerControls[0].Binding = Profile.InputBindings.Back;
      footerControls[1].Label = "Previous"; footerControls[1].Enabled = pageIndex > 0;
      footerControls[1].Binding = Profile.InputBindings.PageBackward;
      footerControls[2].Label = "Next " + (pageIndex + 1) + "/" + pageCount;
      footerControls[2].Binding = Profile.InputBindings.PageForward;
      footerControls[2].Enabled = pageIndex + 1 < pageCount;
      footer.SetActive(Profile.ShowBackControl || pageCount > 1);
      ThemeFooterControls();
      if (generation == bindingGeneration && ReferenceEquals(session, Session) && restoreItemFocus)
        FinishItems(VisibleWidgetCount);
    }

    private void ThemeFooterControls()
    {
      if (!IsBound) return;
      long generation = bindingGeneration;
      navigationThemeDepth++;
      try
      {
        for (int i = 0; i < footerControls.Count && generation == bindingGeneration; i++)
          ThemeFooter(footerControls[i]);
      }
      finally { navigationThemeDepth--; }
      if (generation == bindingGeneration) LayoutNavigationControls();
    }

    private void ThemeFooter(FooterControl control)
    {
      if (!IsBound || control.Widget == null || !control.Visible) return;
      var session = Session;
      long generation = bindingGeneration;
      var events = navigationLease?.Owner != null ? navigationLease.Owner : EventSystem.current;
      bool selected = events != null && events.currentSelectedGameObject == control.Widget.gameObject;
      var action = Context.ResolveInputAction(control.Binding);
      if (!OwnsNavigation(control, session, generation)) return;
      string hint = Context.Prompts != null && action != null
        ? Context.Prompts.Resolve(action, InputPromptStyle.Glyph) : string.Empty;
      if (!OwnsNavigation(control, session, generation)) return;
      bool showHint = !string.IsNullOrEmpty(hint);
      if (control.Widget.BindingHint != null) control.Widget.BindingHint.gameObject.SetActive(showHint);
      control.Widget.Bind(new MenuWidgetState(null, new MenuActionState(true, control.Enabled), selected,
        label: control.Label, skin: Profile.Skin, themeService: Context.Themes,
        activate: control.Activate, select: ThemeFooterControls, isHovered: control.Hovered,
        hoverChanged: hot => { control.Hovered = hot; ThemeFooter(control); }, theme: Profile.GetTheme(Context.Themes),
        bindingHint: hint, promptService: Context.Prompts, bindingAction: action));
      if (!OwnsNavigation(control, session, generation)) return;
      control.Size = MeasureNavigationControl(control);
      if (navigationThemeDepth == 0) LayoutNavigationControls();
    }

    private bool OwnsNavigation(FooterControl control, MenuSession session, long generation)
      => generation == bindingGeneration && ReferenceEquals(session, Session) && IsBound &&
        control.Widget != null && footerControls.Contains(control);

    private Vector2 MeasureNavigationControl(FooterControl control)
    {
      Vector2 Measure(TMP_Text text)
      {
        if (text == null || !text.gameObject.activeSelf) return Vector2.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        var preferred = text.GetPreferredValues();
        var layout = text.GetComponent<LayoutElement>();
        if (layout != null)
        {
          layout.minWidth = layout.preferredWidth = Mathf.Ceil(preferred.x);
          layout.flexibleWidth = 0;
        }
        return new Vector2(Mathf.Ceil(preferred.x), Mathf.Ceil(preferred.y));
      }
      var label = Measure(control.Widget.Label);
      var hint = Measure(control.Widget.BindingHint);
      control.LabelSize = label; control.HintSize = hint;
      var group = control.Widget.GetComponent<HorizontalLayoutGroup>();
      float gap = hint.x > 0 && label.x > 0 ? group.spacing : 0;
      return new Vector2(Mathf.Max(Profile.MinimumTargetSize, Mathf.Ceil(label.x + hint.x + gap + group.padding.horizontal)),
        Mathf.Max(36, Mathf.Max(Profile.MinimumTargetSize, Mathf.Ceil(Mathf.Max(label.y, hint.y) + group.padding.vertical))));
    }

    private void LayoutNavigationControls()
    {
      if (!IsBound || footer == null) return;
      int count = 0;
      float width = 0, height = 0;
      for (int i = 0; i < footerControls.Count; i++)
      {
        if (!footerControls[i].Visible) continue;
        count++; width += footerControls[i].Size.x; height = Mathf.Max(height, footerControls[i].Size.y);
      }
      if (count == 0) { NavigationControlSize = Vector2.zero; return; }
      if (radialNavigation)
      {
        MenuWidgetFactory.Place((RectTransform)footer.transform, Vector2.zero, Vector2.zero);
        for (int i = 0; i < footerControls.Count; i++)
        {
          var control = footerControls[i];
          if (!control.Visible) continue;
          var direction = i == 0 ? new Vector2(.7071068f, -.7071068f) :
            new Vector2(i == 1 ? -.8660254f : .8660254f, .5f);
          float support = Mathf.Abs(direction.x) * control.Size.x * .5f + Mathf.Abs(direction.y) * control.Size.y * .5f;
          float spacing = Mathf.Max(radialNavigationSpacing, control.Widget.GetComponent<HorizontalLayoutGroup>().spacing);
          MenuWidgetFactory.Place((RectTransform)control.Widget.transform,
            direction * (radialNavigationExtent + spacing + support), control.Size);
          PlaceNavigationContents(control);
        }
        NavigationControlSize = Vector2.zero;
      }
      else
      {
        const float gap = 2;
        float span = Mathf.Max(footerWidth, width + gap * (count - 1));
        MenuWidgetFactory.Place((RectTransform)footer.transform, footerPosition, new Vector2(span, height));
        float extra = (span - width - gap * (count - 1)) / count;
        float cursor = -span * .5f;
        for (int i = 0; i < footerControls.Count; i++)
        {
          var control = footerControls[i];
          if (!control.Visible) continue;
          var size = new Vector2(control.Size.x + extra, height);
          MenuWidgetFactory.Place((RectTransform)control.Widget.transform, new Vector2(cursor + size.x * .5f, 0), size);
          PlaceNavigationContents(control);
          cursor += size.x + gap;
        }
        NavigationControlSize = new Vector2(span, height);
      }
      OnNavigationLayoutChanged();
    }

    private static void PlaceNavigationContents(FooterControl control)
    {
      // Give the owned texts their measured bounds before the normal canvas pass, so
      // screen fitting never sees the temporary 100x100 RectTransform defaults.
      var layout = control.Widget.GetComponent<HorizontalLayoutGroup>();
      float gap = control.LabelSize.x > 0 && control.HintSize.x > 0 ? layout.spacing : 0;
      float cursor = -(control.LabelSize.x + control.HintSize.x + gap) * .5f +
        (layout.padding.left - layout.padding.right) * .5f;
      float y = (layout.padding.bottom - layout.padding.top) * .5f;
      if (control.Widget.Label != null && control.Widget.Label.gameObject.activeSelf)
      {
        MenuWidgetFactory.Place(control.Widget.Label.rectTransform,
          new Vector2(cursor + control.LabelSize.x * .5f, y), control.LabelSize);
        cursor += control.LabelSize.x + gap;
      }
      if (control.Widget.BindingHint != null && control.Widget.BindingHint.gameObject.activeSelf)
        MenuWidgetFactory.Place(control.Widget.BindingHint.rectTransform,
          new Vector2(cursor + control.HintSize.x * .5f, y), control.HintSize);
    }

    protected virtual void OnNavigationLayoutChanged() { }

    private void CreateBackdrop()
    {
      var rect = MenuWidgetFactory.Rect("Menu outside click", Root.parent);
      backdrop = rect.gameObject;
      rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
      rect.offsetMin = rect.offsetMax = Vector2.zero;
      rect.SetSiblingIndex(Root.GetSiblingIndex());
      var image = backdrop.AddComponent<Image>();
      image.color = Profile.Backdrop ? ColorFor(MenuVisualRole.Backdrop) : Color.clear;
      var button = backdrop.AddComponent<Button>();
      button.targetGraphic = image; button.transition = Selectable.Transition.None;
      // Routed presses already own item activation; a native release must not click a newly exposed backdrop.
      routedOutsideClick = Profile.InputBindings.RoutePointerClick
        && Context.ResolveInputAction(Profile.InputBindings.Point) != null
        && Context.ResolveInputAction(Profile.InputBindings.Click) != null;
      button.enabled = !routedOutsideClick;
      var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
      button.onClick.AddListener(Cancel);
    }

    private void UpdateBackdrop()
    {
      if (backdrop == null || Profile == null || Context == null) return;
      var image = backdrop.GetComponent<Image>();
      if (image != null) image.color = Profile.Backdrop ? ColorFor(MenuVisualRole.Backdrop) : Color.clear;
    }

    public virtual void Navigate(Vector2 direction, bool analog)
    {
      if (!IsBound) return;
      if (TryRouteControlNavigation(direction, analog)) { CancelSelectionReset(); return; }
      if (HandleSelectionReset(direction, analog)) { analogLatched = false; return; }
      if (analog)
      {
        if (direction.sqrMagnitude <= Profile.AnalogDeadZone * Profile.AnalogDeadZone) { analogLatched = false; return; }
        if (analogLatched) return;
        analogLatched = true;
      }
      if (direction == Vector2.zero || Session.Rows.Count == 0) return;
      Session.InteractionMode = analog ? MenuInteractionMode.Controller : MenuInteractionMode.Keyboard;
      SetPointerHovered(null);
      int selected = -1;
      for (int i = 0; i < Session.Rows.Count; i++) if (Session.Rows[i].Action == Session.Selected) { selected = i; break; }
      int step = direction.y > 0 || direction.x < 0 ? -1 : 1;
      if (selected < 0) selected = step > 0 ? -1 : 0;
      for (int offset = 1; offset <= Session.Rows.Count; offset++)
      {
        int next = (selected + step * offset + Session.Rows.Count) % Session.Rows.Count;
        var action = Session.Rows[next].Action;
        if (action.Behavior.IsInteractive && Session.Select(action.Id)) break;
      }
    }
    public virtual void Point(Vector2 position)
    {
      var observed = Session; long generation = BindingGeneration;
      if (!OwnsBinding(observed, generation)) return;
      CancelSelectionReset();
      if (!OwnsBinding(observed, generation)) return;
      observed.InteractionMode = MenuInteractionMode.Pointer;
      if (!OwnsBinding(observed, generation)) return;
      PointerControlOwnsInput = false;
      PointerHitObject = null;
      var events = navigationLease?.Owner != null ? navigationLease.Owner : EventSystem.current;
      if (events == null) return;
      if (pointerEvent == null) pointerEvent = new PointerEventData(events);
      pointerEvent.Reset(); pointerEvent.position = position;
      pointerHits.Clear(); events.RaycastAll(pointerEvent, pointerHits);
      if (!OwnsBinding(observed, generation) || pointerHits.Count == 0) return;
      var hit = pointerHits[0].gameObject;
      PointerHitObject = hit;
      // Center information and auxiliary controls belong to the selected action. Crossing
      // from its sector into that authored affordance must not destroy the controls.
      if (center?.GameObject != null && hit.transform.IsChildOf(center.Rect))
      {
        PointerControlOwnsInput = true;
        if (SetPointerHovered(null)) Refresh(false);
        return;
      }
      var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)
        ?? ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit);
      if (handler == null || handler == backdrop) return;
      PointerControlOwnsInput = IsControl(handler) || !handler.transform.IsChildOf(transform);
      if (PointerControlOwnsInput) ClearPointerSelection();
    }

    protected void ClearPointerSelection()
    {
      var observed = Session; long generation = BindingGeneration;
      if (!OwnsBinding(observed, generation) || observed.InteractionMode != MenuInteractionMode.Pointer) return;
      bool hovered = SetPointerHovered(null);
      if (!OwnsBinding(observed, generation)) return;
      bool cleared = observed.ClearSelection();
      if (OwnsBinding(observed, generation) && !cleared && hovered) Refresh(false);
    }

    /// <summary>One event-driven unscaled neutral timer; no per-frame input polling.</summary>
    protected bool HandleSelectionReset(Vector2 direction, bool analog)
    {
      bool neutral = analog && direction.sqrMagnitude <= Profile.AnalogDeadZone * Profile.AnalogDeadZone;
      if (!neutral) { CancelSelectionReset(); return false; }
      analogLatched = false;
      // A late canceled stick callback must not reclaim selection from a mouse or keyboard.
      if (Session.InteractionMode != MenuInteractionMode.Controller || Session.Selected == null) return true;
      if (selectionResetCancellation != null) return true;
      if (Profile.AnalogSelectionResetDelay <= 0) { Session.ClearSelection(); return true; }
      var cancellation = new CancellationTokenSource();
      selectionResetCancellation = cancellation;
      ResetSelectionAfterDelayAsync(Session, Session.Selected, Session.ParentSelected, Session.PageIndex,
        Session.NavigationRevision, Profile.AnalogSelectionResetDelay, cancellation, cancellation.Token).Forget();
      return true;
    }

    protected void CancelSelectionReset()
    {
      var cancellation = selectionResetCancellation;
      selectionResetCancellation = null;
      if (cancellation == null) return;
      try { cancellation.Cancel(); }
      finally { cancellation.Dispose(); }
    }

    private async UniTask ResetSelectionAfterDelayAsync(MenuSession observed, MenuAction selected,
      MenuAction parent, int page, long navigation, float delay, CancellationTokenSource cancellation, CancellationToken token)
    {
      try
      {
        await UniTask.Delay(TimeSpan.FromSeconds(delay), ignoreTimeScale: true, cancellationToken: token);
        if (this == null || !IsBound || !ReferenceEquals(Session, observed)
          || !ReferenceEquals(selectionResetCancellation, cancellation) || Session.InteractionMode != MenuInteractionMode.Controller
          || Session.NavigationRevision != navigation || !ReferenceEquals(Session.Selected, selected)
          || !ReferenceEquals(Session.ParentSelected, parent) || Session.PageIndex != page) return;
        Session.ClearSelection();
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested) { }
      finally
      {
        if (ReferenceEquals(selectionResetCancellation, cancellation))
        { selectionResetCancellation = null; cancellation.Dispose(); }
      }
    }

    public void ConfirmPointer()
    {
      if (IsBound && routedOutsideClick && backdrop != null && PointerHitObject == backdrop)
      { Cancel(); return; }
      pointerConfirmation = true;
      try { Confirm(); }
      finally { pointerConfirmation = false; }
    }
    public void EndPointerHold()
    {
      pointerConfirmation = true;
      try { EndHold(); }
      finally { pointerConfirmation = false; }
    }

    protected bool TryRouteControlSubmit()
    {
      if (widgetConfirmation) return false;
      if (holdConfirmation) return PointerControlOwnsInput || NativeControlHasFocus;
      if (pointerConfirmation) return PointerControlOwnsInput || NativeItemPointerOwnsConfirmation();
      if (!IsBound || Profile.InputBindings.UseNativeUguiNavigation) return false;
      var control = FocusedControl<ISubmitHandler>();
      if (control == null) return false;
      ExecuteEvents.Execute(control, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
      return true;
    }

    protected bool TryRouteControlNavigation(Vector2 direction, bool analog)
    {
      if (!IsBound || Profile.InputBindings.UseNativeUguiNavigation) return false;
      var control = FocusedControl<IMoveHandler>();
      if (control == null) return false;
      if (analog)
      {
        if (direction.sqrMagnitude <= Profile.AnalogDeadZone * Profile.AnalogDeadZone) { analogLatched = false; return true; }
        if (analogLatched) return true;
        analogLatched = true;
      }
      if (direction == Vector2.zero) return true;
      var data = new AxisEventData(EventSystem.current) { moveVector = direction,
        moveDir = Math.Abs(direction.x) > Math.Abs(direction.y)
          ? direction.x > 0 ? MoveDirection.Right : MoveDirection.Left
          : direction.y > 0 ? MoveDirection.Up : MoveDirection.Down };
      ExecuteEvents.Execute(control, data, ExecuteEvents.moveHandler);
      return true;
    }

    private GameObject FocusedControl<T>() where T : IEventSystemHandler
    {
      var events = navigationLease?.Owner != null ? navigationLease.Owner : EventSystem.current;
      var selected = events != null ? events.currentSelectedGameObject : null;
      return selected != null && IsControl(selected) ? ExecuteEvents.GetEventHandler<T>(selected) : null;
    }
    private bool IsControl(GameObject control)
      => center?.GameObject != null && control.transform.IsChildOf(center.Rect)
        || footer != null && control.transform.IsChildOf(footer.transform);

    public virtual void Confirm() { if (IsBound && !TryRouteControlSubmit()) Session.ActivateAsync().Forget(); }
    public virtual void Back()
    {
      if (!IsBound) return;
      CancelSelectionReset();
      if (!Profile.InputBindings.UseNativeUguiNavigation)
      {
        var control = FocusedControl<ICancelHandler>();
        if (control != null)
        {
          var data = new BaseEventData(EventSystem.current);
          ExecuteEvents.Execute(control, data, ExecuteEvents.cancelHandler);
          if (data.used) return;
        }
      }
      Session.GoBack();
    }
    public virtual void Cancel() { if (IsBound) Session.Close(); }
    public virtual void InvokeSecondary(int index) { if (IsBound) Session.InvokeSecondaryAsync(index).Forget(); }
    public virtual void Page(int direction) { if (!IsBound) return; CancelSelectionReset(); if (direction > 0) Session.PageForward(); else Session.PageBackward(); }
    public virtual void SwitchRing() { }
    public virtual void BeginHold() { CancelSelectionReset(); holding = IsBound; }
    public virtual void EndHold()
    {
      if (!holding) return;
      holding = false; holdConfirmation = true;
      try { Confirm(); }
      finally { holdConfirmation = false; }
    }
    public void OnCancel(BaseEventData eventData)
    {
      if (!IsBound || eventData.used || !Profile.InputBindings.UseNativeUguiNavigation || !Context.CanHandleBackInput) return;
      eventData.Use(); Back();
    }

    protected void ReportCannotFit(string diagnostic)
    {
      FitsViewport = false;
      PlacementDiagnostic = diagnostic;
      Log.ForContext(GetType()).Warning("Menu cannot fit without reducing readable targets: {Reason}", diagnostic);
    }
    private static void DestroyOwned(GameObject value)
    { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
  }

  /// <summary>Stacked ownership of uGUI navigation; pointer events remain enabled in semantic profiles.</summary>
  internal sealed class MenuNativeNavigationLease : IDisposable
  {
    private sealed class State
    {
      internal EventSystem Owner;
      internal bool Previous;
      internal bool HandoffPending;
      internal long HandoffGeneration;
      internal readonly List<MenuNativeNavigationLease> Leases = new List<MenuNativeNavigationLease>();
    }
    private static readonly Dictionary<EventSystem, State> States = new Dictionary<EventSystem, State>();
    private readonly State state;
    private readonly bool native;
    private readonly MenuInputOwnership ownership;
    private bool disposed;
    private MenuNativeNavigationLease(State state, bool native, MenuInputOwnership ownership)
    { this.state = state; this.native = native; this.ownership = ownership ?? MenuInputOwnership.Shared; }
    internal EventSystem Owner => state.Owner;
    internal static MenuNativeNavigationLease Acquire(EventSystem events, bool native, MenuInputOwnership ownership = null)
    {
      if (events == null) return null;
      if (!States.TryGetValue(events, out var state))
      {
        state = new State { Owner = events, Previous = events.sendNavigationEvents };
        States.Add(events, state);
      }
      var lease = new MenuNativeNavigationLease(state, native, ownership);
      state.Leases.Add(lease);
      events.sendNavigationEvents = native && !state.HandoffPending;
      return lease;
    }
    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      state.Leases.Remove(this);
      bool restoreNative = state.Leases.Count == 0 ? state.Previous : state.Leases[state.Leases.Count - 1].native;
      bool semanticHandoff = !native && restoreNative;
      try
      {
        if (semanticHandoff && state.Owner != null
          && state.Owner.currentInputModule is InputSystemUIInputModule module)
        {
          var coordinator = state.Leases.Count == 0 ? ownership : state.Leases[state.Leases.Count - 1].ownership;
          coordinator.DeferRestorationUntilNeutral(module.move?.action);
        }
      }
      finally
      {
        if (semanticHandoff && Application.isPlaying && state.Owner != null)
        {
          // The native module has already observed this frame's Submit/Cancel.
          // Returning ownership before it processes would replay that gesture on restored focus.
          state.HandoffPending = true;
          state.Owner.sendNavigationEvents = false;
          long generation = ++state.HandoffGeneration;
          UniTask.Post(() => CompleteHandoff(state, generation), PlayerLoopTiming.LastPostLateUpdate);
        }
        else if (!state.HandoffPending) Restore(state);
      }
    }

    private static void CompleteHandoff(State state, long generation)
    {
      if (generation != state.HandoffGeneration
        || !States.TryGetValue(state.Owner, out var current) || !ReferenceEquals(state, current)) return;
      state.HandoffPending = false;
      Restore(state);
    }

    private static void Restore(State state)
    {
      // A new host may have acquired ownership during the handoff; use its current policy.
      bool native = state.Leases.Count == 0 ? state.Previous : state.Leases[state.Leases.Count - 1].native;
      if (state.Owner != null) state.Owner.sendNavigationEvents = native;
      if (state.Leases.Count == 0) States.Remove(state.Owner);
    }
  }
}
