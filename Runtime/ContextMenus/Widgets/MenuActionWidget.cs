using System;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Typeset;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Widgets
{
  /// <summary>Composable default widget; every visual reference is optional and custom widgets can replace it entirely.</summary>
  public class MenuActionWidget : MonoBehaviour, IMenuWidget, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, ICancelHandler
  {
    [field: SerializeField] public TMP_Text Label { get; set; }
    [field: SerializeField] public TMP_Text Description { get; set; }
    [field: SerializeField] public TMP_Text BindingHint { get; set; }
    [field: SerializeField] public TMP_Text Cost { get; set; }
    [field: SerializeField] public TMP_Text Variant { get; set; }
    [field: SerializeField] public Image Icon { get; set; }
    /// <summary>Optional artwork target. Its authored tint is preserved independently of Icon theming.</summary>
    [field: SerializeField] public Image ActionImage { get; set; }
    [field: SerializeField] public Image Progress { get; set; }
    [field: SerializeField] public Graphic Background { get; set; }
    [field: SerializeField] public Graphic Highlight { get; set; }
    [field: SerializeField] public Graphic Check { get; set; }
    [field: SerializeField] public Button Button { get; set; }
    [field: SerializeField] public bool UseSkin { get; set; } = true;
    [field: SerializeField] public MenuVisualRole BackgroundRole { get; set; } = MenuVisualRole.Normal;
    [field: SerializeField] public SpacingPreset[] SpacingTargets { get; set; } = Array.Empty<SpacingPreset>();

    private readonly MenuWidgetThemeBindings themeBindings = new();

    private IDisposable themeSubscription;
    private IDisposable promptBinding;
    private Button subscribedButton;
    private long bindingGeneration;
    protected MenuWidgetState State { get; private set; }

    public void Configure(TMP_Text label, Image icon, Graphic highlight, Graphic check,
      Button button, TMP_Text description = null, Image actionImage = null)
    {
      Label = label; Icon = icon; Highlight = highlight; Check = check; Button = button; Description = description;
      ActionImage = actionImage;
    }

    public virtual void Bind(MenuWidgetState state)
    {
      long retiring = bindingGeneration;
      Unbind();
      if (state == null || bindingGeneration != retiring + 1 || State != null) return;
      State = state;
      long generation = ++bindingGeneration;
      if (Button != null)
      {
        subscribedButton = Button;
        subscribedButton.onClick.AddListener(Activate);
      }
      var service = MenuSkin.Service(state.ThemeService);
      if (UseSkin && service != null)
      {
        var subscription = service.OnThemeUpdated.Subscribe(_ => ApplyThemeOnMainThread());
        if (!Owns(generation, state)) { subscription.Dispose(); return; }
        themeSubscription = subscription;
      }
      ApplyVisuals();
      if (!Owns(generation, state)) return;
      if (BindingHint != null && state.PromptService != null && state.BindingAction != null)
      {
        var binding = state.PromptService.Bind(BindingHint, () => state.PromptService.Resolve(state.BindingAction, InputPromptStyle.Glyph));
        if (!Owns(generation, state)) { binding.Dispose(); return; }
        promptBinding = binding;
      }
    }

    public virtual void Unbind()
    {
      long generation = ++bindingGeneration;
      var oldTheme = themeSubscription; themeSubscription = null;
      var oldPrompt = promptBinding; promptBinding = null;
      var oldButton = subscribedButton; subscribedButton = null;
      State = null;
      if (oldButton != null) oldButton.onClick.RemoveListener(Activate);
      Exception failure = null;
      // Restore the old complete preset bindings before subscription disposal can bind a new owner.
      try { themeBindings.Restore(); } catch (Exception error) { failure = error; }
      // Preset restoration can synchronously bind a replacement. Only the retired empty binding may clear visuals.
      try { BindSprite(Icon, null, false, generation, null); } catch (Exception error) { failure ??= error; }
      try { BindSprite(ActionImage, null, true, generation, null); } catch (Exception error) { failure ??= error; }
      try { oldTheme?.Dispose(); } catch (Exception error) { failure ??= error; }
      try { oldPrompt?.Dispose(); } catch (Exception error) { failure ??= error; }
      if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private bool Owns(long generation, MenuWidgetState state)
      => generation == bindingGeneration && ReferenceEquals(State, state);

    private void ApplyThemeOnMainThread()
    {
      if (PlayerLoopHelper.IsMainThread) { ApplyVisuals(); return; }
      long generation = bindingGeneration;
      UniTask.Post(() =>
      {
        if (this != null && generation == bindingGeneration && State != null) ApplyVisuals();
      });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
      if (State == null || !State.Availability.Visible) return;
      if (State.HoverChanged != null) State.HoverChanged(true);
      else Select();
    }
    public void OnPointerExit(PointerEventData eventData)
    {
      var state = State;
      if (state?.PointerExited != null) state.PointerExited(eventData.position);
      else state?.HoverChanged?.Invoke(false);
    }
    public void OnSelect(BaseEventData eventData)
    {
      if (eventData is PointerEventData pointer) OnPointerEnter(pointer);
      else Select();
    }
    public void OnCancel(BaseEventData eventData)
    {
      if (eventData.used || transform.parent == null) return;
      ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.cancelHandler);
    }

    private void Select()
    {
      if (State != null && State.Availability.Visible) State.Select?.Invoke();
    }

    private void Activate()
    {
      if (State != null && State.Availability.Visible && State.Availability.Enabled) State.Activate?.Invoke();
    }

    protected virtual void ApplyVisuals()
    {
      var state = State;
      if (state == null) return;
      long generation = bindingGeneration;
      if (Label != null) Label.text = state.Label;
      if (Description != null) Description.text = state.Description;
      if (BindingHint != null && promptBinding == null) BindingHint.text = state.BindingHint;
      if (Cost != null) Cost.text = state.Details?.CostText ?? string.Empty;
      if (Variant != null) Variant.text = state.Details?.VariantText ?? string.Empty;
      if (!BindSprite(Icon, state.Icon, false, generation, state)) return;
      if (!BindSprite(ActionImage, state.Image, true, generation, state)) return;
      if (Highlight != null) Highlight.enabled = state.IsSelected;
      if (Check != null) Check.enabled = state.Availability.IsChecked;
      if (Button != null) Button.interactable = state.Availability.Visible && state.Availability.Enabled
        && (state.Action?.Behavior.IsInteractive ?? (state.Activate != null));
      if (Progress != null)
      {
        var progress = state.Details?.Progress;
        Progress.enabled = progress.HasValue;
        if (progress.HasValue) Progress.fillAmount = Mathf.Clamp01(progress.Value);
      }
      if (!UseSkin) { themeBindings.Restore(); return; }
      var role = !state.Availability.Enabled ? MenuVisualRole.Disabled :
        state.IsHovered ? MenuVisualRole.Hovered : state.IsSelected ? MenuVisualRole.Selected : MenuVisualRole.Normal;
      Tint(Background, BackgroundRole == MenuVisualRole.Normal ? role : BackgroundRole);
      Tint(Highlight, MenuVisualRole.Selected);
      Tint(Check, MenuVisualRole.Checked, state.Availability.Enabled ? MenuThemeState.Checked : MenuThemeState.Disabled);
      Tint(Icon, MenuVisualRole.Icon);
      Tint(Label, MenuVisualRole.Label);
      Tint(Description, MenuVisualRole.Description);
      Tint(BindingHint, MenuVisualRole.Description);
      Tint(Cost, MenuVisualRole.Description);
      Tint(Variant, MenuVisualRole.Description);
      Tint(Progress, MenuVisualRole.Icon);
      ApplyTypeset(Label, false);
      if (!Owns(generation, state)) return;
      ApplyTypeset(Description, true);
      if (!Owns(generation, state)) return;
      ApplyTypeset(BindingHint, true);
      if (!Owns(generation, state)) return;
      ApplyTypeset(Cost, true);
      if (!Owns(generation, state)) return;
      ApplyTypeset(Variant, true);
      if (!Owns(generation, state)) return;
      var token = state.Theme.GetSpacing(state.ThemeState);
      if (SpacingTargets != null && SpacingTargets.Length > 0)
      {
        for (int i = 0; i < SpacingTargets.Length; i++)
        {
          themeBindings.ApplySpacing(SpacingTargets[i], token);
          if (!Owns(generation, state)) return;
        }
      }
      else themeBindings.ApplySpacing(GetComponent<SpacingPreset>(), token);
    }

    private bool BindSprite(Image target, Sprite sprite, bool collapseChild, long generation, MenuWidgetState state)
    {
      if (!Owns(generation, state)) return false;
      if (target == null) return true;
      target.sprite = sprite;
      if (!Owns(generation, state)) return false;
      if (target == null) return true;
      bool visible = sprite != null;
      target.enabled = visible;
      if (!Owns(generation, state)) return false;
      if (target == null) return true;
      // Collapse dedicated artwork leaves, while preserving independently wired overlay children.
      // Never deactivate the widget root or hide child labels/icons with the optional graphic.
      bool containerVisible = visible || target.transform.childCount > 0;
      if (collapseChild && target.gameObject != gameObject && target.gameObject.activeSelf != containerVisible)
        target.gameObject.SetActive(containerVisible);
      return Owns(generation, state);
    }

    protected void Tint(Graphic target, MenuVisualRole role, MenuThemeState? themeState = null)
    {
      if (target == null || State == null) return;
      target.color = State.Theme.GetColor(role, themeState ?? State.ThemeState);
    }

    protected void ApplyTypeset(TMP_Text target, bool description)
    {
      if (target == null || State == null) return;
      themeBindings.ApplyTypography(target, State.Theme.GetTypeset(description, State.ThemeState));
    }

    protected virtual void OnDisable() => Unbind();
    protected virtual void OnDestroy() => Unbind();
  }
}
