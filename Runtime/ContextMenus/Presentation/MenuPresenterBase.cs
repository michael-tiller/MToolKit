using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.ContextMenus.Widgets;
using MToolKit.Runtime.UI.Scaling;
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
    private GameObject previousFocus;
    private bool refreshing, analogLatched, holding;
    private long bindingGeneration;
    private MenuNativeNavigationLease navigationLease;
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private PointerEventData pointerEvent;
    private MenuAction pointerHovered;
    private bool pointerConfirmation;
    private bool holdConfirmation;
    private bool widgetConfirmation;
    private IMenuTransition transition;
    private MenuTransitionAsset transitionAsset;
    private CancellationTokenSource transitionCancellation;
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
      bindingGeneration++;
      // Detach ownership before custom callbacks can synchronously bind a replacement.
      var oldSession = Session; var oldContext = Context;
      var oldThemes = themes; var oldPrompts = prompts;
      var oldScale = scaleCanvas; var oldFocus = previousFocus;
      var oldItems = items.ToArray(); items.Clear();
      var oldCenter = center; var oldSecondary = secondaryTemplate;
      var oldBackdrop = backdrop; var oldFooter = footer;
      var oldNavigation = navigationLease;
      var oldTransition = transition; var oldCancellation = transitionCancellation;
      Session = null; Profile = null; Context = null;
      themes = null; prompts = null; scaleCanvas = null; previousFocus = null;
      center = null; secondaryTemplate = null; backdrop = null; footer = null;
      footerControls.Clear();
      navigationLease = null; transition = null; transitionCancellation = null; transitionAsset = null;
      transitionGeneration++; hasTransitionSnapshot = false;
      transitionPath.Clear(); transitionRows.Clear();
      pointerEvent = null; pointerHits.Clear(); PointerControlOwnsInput = false;
      PointerHitObject = null;
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
          var hint = index < Profile.InputBindings.Secondary.Count ? Profile.InputBindings.Secondary[index] : null;
          states.Add(StateFor(auxiliary, false, () => InvokeSecondary(index), bindingOverride: hint));
        }
        secondaryStates = states;
      }
      var bindingReference = action.BindingHint != null ? action.BindingHint : bindingOverride ?? Profile.InputBindings.Submit;
      var bindingAction = Context.ResolveInputAction(bindingReference);
      string binding = Context.Prompts != null && bindingReference != null
        ? Context.Prompts.Resolve(bindingAction, InputPromptStyle.Glyph) : string.Empty;
      return new MenuWidgetState(action, availability, selected, Context.Resolve(action.Label), Context.Resolve(action.Description),
        Profile.Skin, Context.Themes, activate, () => SelectWidget(select), binding, secondaryStates,
        Context.Prompts, bindingAction, IsPointerHovered(action), entered => HoverWidget(action, entered, select), Profile.GetTheme(Context.Themes));
    }

    protected bool IsPointerHovered(MenuAction action)
      => IsBound && Session.InteractionMode == MenuInteractionMode.Pointer && ReferenceEquals(pointerHovered, action);

    protected bool SetPointerHovered(MenuAction action)
    {
      if (action != null && !action.Behavior.IsInteractive) action = null;
      if (ReferenceEquals(pointerHovered, action)) return false;
      pointerHovered = action;
      return true;
    }

    protected void ClearPointerHover(MenuAction action)
    {
      if (ReferenceEquals(pointerHovered, action) && SetPointerHovered(null)) Refresh(false);
    }

    private void HoverWidget(MenuAction action, bool entered, Action select)
    {
      if (!IsBound) return;
      if (!entered) { ClearPointerHover(action); return; }
      if (!AcceptNativePointerHover) return;
      var before = Session.Selected; var mode = Session.InteractionMode;
      bool changed = SetPointerHovered(action);
      Session.InteractionMode = MenuInteractionMode.Pointer;
      select?.Invoke();
      if (changed && ReferenceEquals(before, Session.Selected) && mode == Session.InteractionMode) Refresh(false);
    }

    private void SelectWidget(Action select)
    {
      if (!IsBound) return;
      var before = Session.Selected; var mode = Session.InteractionMode;
      bool changed = SetPointerHovered(null);
      Session.InteractionMode = MenuInteractionMode.Keyboard;
      select?.Invoke();
      if (changed && ReferenceEquals(before, Session.Selected) && mode == Session.InteractionMode) Refresh(false);
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
      center.Bind(StateFor(Session.Selected, true, secondary: true));

    }

    protected void SelectAction(MenuAction action)
    { if (IsBound && action != null) Session.Select(action.Id); }
    protected virtual void ActivateAction(MenuAction action)
    { if (IsBound && Session.Select(action.Id)) ConfirmChosenWidget(); }
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
      public bool Enabled = true, Hovered;
    }

    protected void SetFooter(Vector2 position, float width) => SetFooter(position, width, true);

    protected void SetFooter(Vector2 position, float width, bool showPagination)
    {
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
          label = MenuWidgetFactory.Text(name + " label", rect, 16);
          label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
          var hint = MenuWidgetFactory.Text(name + " binding", rect, 16);
          var hintLayout = hint.gameObject.AddComponent<LayoutElement>();
          hintLayout.minWidth = 28; hintLayout.preferredWidth = -1;
          var widget = rect.gameObject.AddComponent<MenuActionWidget>(); widget.Background = image;
          widget.BackgroundRole = MenuVisualRole.Auxiliary; widget.Configure(label, null, null, null, button);
          widget.BindingHint = hint;
          footerControls.Add(new FooterControl { Widget = widget, Activate = callback, Label = name });
          return button;
        }
        Button("Back", Back, out backLabel);
        previousButton = Button("Previous", () => Page(-1), out previousLabel);
        nextButton = Button("Next", () => Page(1), out nextLabel);
      }
      MenuWidgetFactory.Place((RectTransform)footer.transform, position, new Vector2(width, 36));
      float itemWidth = showPagination ? width / 3 : width;
      for (int i = 0; i < 3; i++)
      {
        var rect = (RectTransform)footer.transform.GetChild(i);
        rect.gameObject.SetActive(showPagination || i == 0);
        MenuWidgetFactory.Place(rect, new Vector2(showPagination ? (i - 1) * itemWidth : 0, 0), new Vector2(itemWidth - 2, 36));
      }
      footerControls[0].Label = Session.Path.Count == 0 ? "Close" : "Back";
      footerControls[0].Binding = Profile.InputBindings.Back;
      footerControls[1].Label = "Previous"; footerControls[1].Enabled = Session.PageIndex > 0;
      footerControls[1].Binding = Profile.InputBindings.PageBackward;
      footerControls[2].Label = "Next " + (Session.PageIndex + 1) + "/" + Session.PageCount;
      footerControls[2].Binding = Profile.InputBindings.PageForward;
      footerControls[2].Enabled = Session.PageIndex + 1 < Session.PageCount;
      ThemeFooterControls();
    }

    private void ThemeFooterControls()
    {
      if (!IsBound) return;
      for (int i = 0; i < footerControls.Count; i++) ThemeFooter(footerControls[i]);
    }

    private void ThemeFooter(FooterControl control)
    {
      if (!IsBound || control.Widget == null) return;
      var events = navigationLease?.Owner != null ? navigationLease.Owner : EventSystem.current;
      bool selected = events != null && events.currentSelectedGameObject == control.Widget.gameObject;
      var action = Context.ResolveInputAction(control.Binding);
      string hint = Context.Prompts != null && action != null
        ? Context.Prompts.Resolve(action, InputPromptStyle.Glyph) : string.Empty;
      bool showHint = !string.IsNullOrEmpty(hint);
      if (control.Widget.BindingHint != null) control.Widget.BindingHint.gameObject.SetActive(showHint);
      control.Widget.Bind(new MenuWidgetState(null, new MenuActionState(true, control.Enabled), selected,
        label: control.Label, skin: Profile.Skin, themeService: Context.Themes,
        activate: control.Activate, select: ThemeFooterControls, isHovered: control.Hovered,
        hoverChanged: hot => { control.Hovered = hot; ThemeFooter(control); }, theme: Profile.GetTheme(Context.Themes),
        bindingHint: hint, promptService: Context.Prompts, bindingAction: action));
      if (control.Widget != null && control.Widget.BindingHint != null)
      {
        var text = control.Widget.BindingHint;
        var layout = text.GetComponent<LayoutElement>();
        if (layout != null) layout.minWidth = Mathf.Ceil(Mathf.Max(28, text.GetPreferredValues().x));
      }
    }

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
      if (TryRouteControlNavigation(direction, analog)) return;
      if (analog)
      {
        if (direction.sqrMagnitude <= Profile.AnalogDeadZone * Profile.AnalogDeadZone) { analogLatched = false; return; }
        if (analogLatched) return;
        analogLatched = true;
      }
      if (direction == Vector2.zero || Session.Rows.Count == 0) return;
      Session.InteractionMode = analog ? MenuInteractionMode.Controller : MenuInteractionMode.Keyboard;
      SetPointerHovered(null);
      int selected = 0;
      for (int i = 0; i < Session.Rows.Count; i++) if (Session.Rows[i].Action == Session.Selected) { selected = i; break; }
      int step = direction.y > 0 || direction.x < 0 ? -1 : 1;
      for (int offset = 1; offset <= Session.Rows.Count; offset++)
      {
        int next = (selected + step * offset + Session.Rows.Count) % Session.Rows.Count;
        var action = Session.Rows[next].Action;
        if (action.Behavior.IsInteractive && Session.Select(action.Id)) break;
      }
    }
    public virtual void Point(Vector2 position)
    {
      if (!IsBound) return;
      Session.InteractionMode = MenuInteractionMode.Pointer;
      PointerControlOwnsInput = false;
      PointerHitObject = null;
      var events = navigationLease?.Owner != null ? navigationLease.Owner : EventSystem.current;
      if (events == null) return;
      if (pointerEvent == null) pointerEvent = new PointerEventData(events);
      pointerEvent.Reset(); pointerEvent.position = position;
      pointerHits.Clear(); events.RaycastAll(pointerEvent, pointerHits);
      if (pointerHits.Count == 0) return;
      var hit = pointerHits[0].gameObject;
      PointerHitObject = hit;
      var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)
        ?? ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit);
      if (handler == null || handler == backdrop) return;
      PointerControlOwnsInput = IsControl(handler) || !handler.transform.IsChildOf(transform);
      if (PointerControlOwnsInput && SetPointerHovered(null)) Refresh(false);
    }

    public void ConfirmPointer()
    {
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
    public virtual void Page(int direction) { if (!IsBound) return; if (direction > 0) Session.PageForward(); else Session.PageBackward(); }
    public virtual void SwitchRing() { }
    public virtual void BeginHold() { holding = IsBound; }
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
