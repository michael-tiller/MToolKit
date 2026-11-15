using System;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
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
    [field: SerializeField] public Image Progress { get; set; }
    [field: SerializeField] public Graphic Background { get; set; }
    [field: SerializeField] public Graphic Highlight { get; set; }
    [field: SerializeField] public Graphic Check { get; set; }
    [field: SerializeField] public Button Button { get; set; }
    [field: SerializeField] public bool UseSkin { get; set; } = true;

    private IDisposable themeSubscription;
    private IDisposable promptBinding;
    private Button subscribedButton;
    private long bindingGeneration;
    protected MenuWidgetState State { get; private set; }

    public void Configure(TMP_Text label, Image icon, Graphic highlight, Graphic check,
      Button button, TMP_Text description = null)
    {
      Label = label; Icon = icon; Highlight = highlight; Check = check; Button = button; Description = description;
    }

    public virtual void Bind(MenuWidgetState state)
    {
      Unbind();
      if (state == null) return;
      State = state;
      bindingGeneration++;
      if (Button != null)
      {
        subscribedButton = Button;
        subscribedButton.onClick.AddListener(Activate);
      }
      var service = MenuSkin.Service(state.ThemeService);
      if (UseSkin && service != null) themeSubscription = service.OnThemeUpdated.Subscribe(_ => ApplyThemeOnMainThread());
      ApplyVisuals();
      if (BindingHint != null && state.PromptService != null && state.BindingAction != null)
        promptBinding = state.PromptService.Bind(BindingHint, () => state.PromptService.Resolve(state.BindingAction));
    }

    public virtual void Unbind()
    {
      bindingGeneration++;
      themeSubscription?.Dispose();
      themeSubscription = null;
      promptBinding?.Dispose(); promptBinding = null;
      if (subscribedButton != null) subscribedButton.onClick.RemoveListener(Activate);
      subscribedButton = null;
      State = null;
    }

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
    public void OnPointerExit(PointerEventData eventData) => State?.HoverChanged?.Invoke(false);
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
      if (Label != null) Label.text = state.Label;
      if (Description != null) Description.text = state.Description;
      if (BindingHint != null && promptBinding == null) BindingHint.text = state.BindingHint;
      if (Cost != null) Cost.text = state.Details?.CostText ?? string.Empty;
      if (Variant != null) Variant.text = state.Details?.VariantText ?? string.Empty;
      if (Icon != null) { Icon.sprite = state.Icon; Icon.enabled = state.Icon != null; }
      if (Highlight != null) Highlight.enabled = state.IsSelected;
      if (Check != null) Check.enabled = state.Availability.IsChecked;
      if (Button != null) Button.interactable = state.Availability.Visible && state.Availability.Enabled && state.Activate != null;
      if (Progress != null)
      {
        var progress = state.Details?.Progress;
        Progress.enabled = progress.HasValue;
        if (progress.HasValue) Progress.fillAmount = Mathf.Clamp01(progress.Value);
      }
      if (!UseSkin) return;
      var role = !state.Availability.Enabled ? MenuVisualRole.Disabled :
        state.IsHovered ? MenuVisualRole.Hovered : state.IsSelected ? MenuVisualRole.Selected : MenuVisualRole.Normal;
      Tint(Background, role);
      Tint(Highlight, MenuVisualRole.Selected);
      Tint(Check, MenuVisualRole.Label);
      Tint(Icon, MenuVisualRole.Icon);
      Tint(Label, MenuVisualRole.Label);
      Tint(Description, MenuVisualRole.Description);
      Tint(BindingHint, MenuVisualRole.Description);
      Tint(Cost, MenuVisualRole.Description);
      Tint(Variant, MenuVisualRole.Description);
      ApplyTypeset(Label, false);
      ApplyTypeset(Description, true);
      var spacing = GetComponent<SpacingPreset>();
      var token = state.Skin != null ? state.Skin.GetSpacing(state.ThemeService) : null;
      if (spacing != null && token != null)
      {
        spacing.SetOverrideTheme(true);
        spacing.SetStart(token);
      }
    }

    protected void Tint(Graphic target, MenuVisualRole role)
    {
      if (target == null || State == null) return;
      target.color = State.Skin != null ? State.Skin.GetColor(role, State.ThemeService) : MenuSkin.DefaultColor(role);
    }

    private void ApplyTypeset(TMP_Text target, bool description)
    {
      if (target == null || State?.Skin == null) return;
      var token = State.Skin.GetTypeset(description, State.ThemeService);
      var preset = target.GetComponent<TypesetPreset>();
      if (preset == null || token == null) return;
      // This widget resolves the injected theme before binding the existing preset. It remains the
      // typography/accessibility implementation; its own global callback cannot override this snapshot.
      preset.SetOverrideTheme(true);
      preset.SetStart(token);
    }

    protected virtual void OnDisable() => Unbind();
    protected virtual void OnDestroy() => Unbind();
  }
}
