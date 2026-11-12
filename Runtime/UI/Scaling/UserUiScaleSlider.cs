using System;
using System.Globalization;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using VContainer;

namespace MToolKit.Runtime.UI.Scaling
{
  /// <summary>Integer percentage positions provide 1% pointer, keyboard and controller steps.</summary>
  [DisallowMultipleComponent]
  public sealed class UserUiScaleSlider : MonoBehaviour
  {
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text percentage;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text warning;
    [SerializeField] private LocalizedString limitedWarning = new("Default String Table",
      "Limited to {0}% by the current screen resolution.");
    [SerializeField] private LocalizedString insufficientWarning = new("Default String Table",
      "Applied {0}%. The current screen is smaller than the minimum supported UI viewport.");
    [SerializeField] private LocalizedString invalidViewportWarning = new("Default String Table",
      "Applied {0}%. The current screen size is unavailable; UI scaling will update when it is available.");
    private ReactiveSetting<float> setting;
    private IDisposable subscription;
    private LocalizedString localizedLabel;
    private LocalizedString warningSource;
    private UserUiScaleCanvas scaleCanvas;
    private ScrollIntoViewOnSelect focusReveal;
    private readonly object[] warningArguments = new object[1];
    private double appliedPercentage = double.NaN;
    private bool revealPending;
    private int revealGeneration;

    [Inject]
    public void BindSettings(ISettingsSystem settings)
    {
      if (settings?.GraphicsSettings != null) Bind(settings.GraphicsSettings.UiScale);
    }

    public void Configure(Slider control, TMP_Text percentageText, ReactiveSetting<float> preference)
    {
      Detach();
      slider = control;
      percentage = percentageText;
      Bind(preference);
    }

    /// <summary>The owning layout reserves this label's space even when its text is hidden.</summary>
    public void ConfigureFeedback(TMP_Text warningText)
    {
      DetachFocusReveal();
      DetachWarning();
      warning = warningText;
      BindFocusReveal();
      UpdateFeedback();
    }

    public void Bind(ReactiveSetting<float> preference)
    {
      Detach();
      setting = preference;
      if (isActiveAndEnabled) Attach();
    }

    public void ResetToDefault()
    {
      if (setting == null) return;
      setting.Value = UiScalePolicy.Default;
      QueueReveal();
    }

    private void OnEnable() => Attach();

    private void Attach()
    {
      Detach();
      if (slider == null || setting?.Property == null || setting.Property.IsDisposed) return;
      slider.minValue = UiScalePolicy.Minimum * 100f;
      slider.maxValue = UiScalePolicy.Maximum * 100f;
      slider.wholeNumbers = true;
      if (label != null)
      {
        if (!Application.isPlaying) label.text = setting.Name;
        else
        {
          localizedLabel = new LocalizedString("Default String Table", setting.Name);
          localizedLabel.StringChanged += UpdateLabel;
        }
      }
      UserUiScaleCanvas.Attached += OnCanvasAttached;
      BindScaleCanvas();
      BindFocusReveal();
      subscription = setting.Property.Subscribe(UpdateControl);
      slider.onValueChanged.AddListener(ChangePreference);
    }

    private void UpdateControl(float value)
    {
      int percent = UiScalePolicy.Percentage(value);
      slider.SetValueWithoutNotify(percent);
      if (percentage != null) percentage.SetText("{0}%", percent);
      UpdateFeedback();
    }

    private void ChangePreference(float percent)
    {
      if (setting == null) return;
      setting.Value = UiScalePolicy.Normalize(percent / 100f);
      QueueReveal();
    }

    private void UpdateLabel(string value)
    {
      if (label != null) label.text = value;
    }

    private void OnCanvasAttached(UserUiScaleCanvas owner)
    {
      // Explicit Edit Mode bindings can outlive Unity's runtime-only destruction callbacks.
      if (this == null)
      {
        UserUiScaleCanvas.Attached -= OnCanvasAttached;
        return;
      }
      if (owner != null && transform.IsChildOf(owner.transform)) BindScaleCanvas();
    }

    private void OnTransformParentChanged()
    {
      if (isActiveAndEnabled && setting != null) BindScaleCanvas();
    }

    private void BindScaleCanvas()
    {
      var next = GetComponentInParent<UserUiScaleCanvas>(true);
      if (next != scaleCanvas)
      {
        if (scaleCanvas != null) scaleCanvas.ConstraintChanged -= UpdateFeedback;
        scaleCanvas = next;
        if (scaleCanvas != null) scaleCanvas.ConstraintChanged += UpdateFeedback;
      }
      UpdateFeedback();
    }

    private void UpdateFeedback()
    {
      if (this == null) return;
      if (warning == null) return;
      if (!isActiveAndEnabled)
      {
        warning.enabled = false;
        return;
      }

      bool constrained = false;
      float effective = setting != null ? UiScalePolicy.Normalize(setting.Value) : UiScalePolicy.Default;
      LocalizedString source = limitedWarning;
      if (scaleCanvas != null)
      {
        UiScaleCapacityResult capacity = scaleCanvas.Capacity;
        effective = scaleCanvas.EffectiveUserScale;
        constrained = !capacity.HasValidViewport || !capacity.SupportsMinimum || capacity.IsConstrained;
        if (!capacity.HasValidViewport) source = invalidViewportWarning;
        else if (!capacity.SupportsMinimum) source = insufficientWarning;
      }

      // Keep the argument numeric so LocalizedString can format it using the selected locale.
      double percent = Math.Round(effective * 100d, 1, MidpointRounding.AwayFromZero);
      bool percentageChanged = appliedPercentage != percent;
      if (percentageChanged)
      {
        appliedPercentage = percent;
        warningArguments[0] = percent;
      }
      if (source != warningSource)
      {
        DetachWarning();
        warningSource = source;
        if (warningSource != null)
        {
          warningSource.Arguments = warningArguments;
          if (Application.isPlaying) warningSource.StringChanged += UpdateWarning;
        }
      }
      else if (percentageChanged && Application.isPlaying)
        warningSource?.RefreshString();

      // The preview uses the source key just as the setting label does outside Play Mode.
      // Runtime translations continue through LocalizedString, including locale changes.
      if (!Application.isPlaying && warningSource != null && !string.IsNullOrEmpty(warningSource.TableEntryReference.Key))
        UpdateWarning(string.Format(CultureInfo.CurrentCulture, warningSource.TableEntryReference.Key, warningArguments));
      // Keep the label, its wrapping measurements and the authored reservation in the layout.
      warning.enabled = constrained;
      if (focusReveal != null) focusReveal.Related = constrained ? warning.rectTransform : null;
    }

    private void BindFocusReveal()
    {
      if (!isActiveAndEnabled || slider == null || warning == null || slider.GetComponentInParent<ScrollRect>() == null) return;
      focusReveal = slider.GetComponent<ScrollIntoViewOnSelect>() ?? slider.gameObject.AddComponent<ScrollIntoViewOnSelect>();
      focusReveal.Related = warning.enabled ? warning.rectTransform : null;
    }

    private void DetachFocusReveal()
    {
      if (focusReveal != null && warning != null && focusReveal.Related == warning.rectTransform) focusReveal.Related = null;
      focusReveal = null;
    }

    private void UpdateWarning(string value)
    {
      if (warning != null) warning.text = value;
    }

    private void DetachWarning()
    {
      if (warningSource != null) warningSource.StringChanged -= UpdateWarning;
      warningSource = null;
    }

    private void QueueReveal()
    {
      if (!Application.isPlaying || !isActiveAndEnabled || slider == null || !slider.isActiveAndEnabled
        || slider.GetComponentInParent<ScrollRect>() == null || revealPending) return;
      revealPending = true;
      RevealAfterLayoutAsync(revealGeneration).Forget();
    }

    private async UniTask RevealAfterLayoutAsync(int generation)
    {
      // CanvasScaler and layout finish the live preview before revealing this control once.
      bool canceled = await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate,
        this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
      if (canceled || this == null || generation != revealGeneration) return;
      revealPending = false;
      if (isActiveAndEnabled && slider != null && slider.isActiveAndEnabled
        && slider.GetComponentInParent<ScrollRect>() != null)
        ScrollIntoViewOnSelect.EnsureVisible(slider.transform as RectTransform,
          warning != null && warning.enabled ? warning.rectTransform : null);
    }

    private void Detach()
    {
      revealGeneration++;
      revealPending = false;
      UserUiScaleCanvas.Attached -= OnCanvasAttached;
      if (scaleCanvas != null) scaleCanvas.ConstraintChanged -= UpdateFeedback;
      scaleCanvas = null;
      DetachFocusReveal();
      DetachWarning();
      if (localizedLabel != null) localizedLabel.StringChanged -= UpdateLabel;
      localizedLabel = null;
      subscription?.Dispose();
      subscription = null;
      if (slider != null) slider.onValueChanged.RemoveListener(ChangePreference);
    }

    private void OnDisable()
    {
      Detach();
      if (warning != null) warning.enabled = false;
    }

    private void OnDestroy() => Detach();
  }
}
