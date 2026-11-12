using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MToolKit.Runtime.UI.Scaling
{
  /// <summary>
  /// Explicit opt-in for screen-space UI roots. The authored resolution baseline is
  /// retained, so CanvasScaler continues to own resolution changes and hit testing.
  /// Nested canvases inherit their root and never receive a second multiplier.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
  public sealed class UserUiScaleCanvas : MonoBehaviour
  {
    /// <summary>Discovery notification only; each root still owns its independent preference binding.</summary>
    public static event Action<UserUiScaleCanvas> Attached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetNotifications() => Attached = null;

    private Canvas canvas;
    private CanvasScaler scaler;
    private ReactiveSetting<float> setting;
    private IDisposable subscription;
    private Vector2 authoredReference;
    private float authoredScaleFactor;
    private bool captured;
    private bool applied;
    private bool applying;
    private bool pixelSizeViaReference;
    [SerializeField] private Vector2 minimumLogicalViewport = new(1024, 768);
    private Vector2 lastRenderingSize;
    private Rect lastAvailablePixels;

    public float RequestedUserScale { get; private set; } = UiScalePolicy.Default;
    public UiScaleCapacityResult Capacity { get; private set; }
    public Vector2 MinimumLogicalViewport
    {
      get => minimumLogicalViewport;
      set
      {
        if (minimumLogicalViewport == value) return;
        minimumLogicalViewport = value;
        RefreshViewport();
      }
    }
    /// <summary>Requested/applied or constraint state changed, including requests above an unchanged cap.</summary>
    public event Action ConstraintChanged;

    public float EffectiveUserScale { get; private set; } = UiScalePolicy.Default;
    public Vector2 AuthoredReferenceResolution => captured ? authoredReference : GetComponent<CanvasScaler>().referenceResolution;
    /// <summary>Emitted after updating the scaler, before Unity resolves new layout geometry.</summary>
    public event Action<float> ScaleChanged;
    /// <summary>Unity-driven canvas geometry changes; consumers can queue one bounded layout pass.</summary>
    public event Action ViewportChanged;

    [Inject]
    public void Bind(ISettingsSystem settings)
    {
      if (settings?.GraphicsSettings != null) Configure(settings.GraphicsSettings.UiScale);
    }

    public void Configure(ReactiveSetting<float> preference)
    {
      subscription?.Dispose();
      subscription = null;
      setting = preference;
      if (setting?.Property == null || setting.Property.IsDisposed)
      {
        Restore();
        return;
      }
      if (isActiveAndEnabled) Subscribe();
      Attached?.Invoke(this);
    }

    /// <summary>Called by the owning UI lifecycle; never discovers or scales unrelated canvases.</summary>
    public static UserUiScaleCanvas AttachToRoot(Component content, ReactiveSetting<float> preference)
    {
      if (content == null || preference == null) return null;
      Canvas owner = content.GetComponentInParent<Canvas>(true);
      if (owner == null) return null;
      Canvas root = owner.rootCanvas;
      if (root.renderMode == RenderMode.WorldSpace || !root.TryGetComponent<CanvasScaler>(out _)) return null;
      var binding = root.GetComponent<UserUiScaleCanvas>();
      if (binding == null) binding = root.gameObject.AddComponent<UserUiScaleCanvas>();
      binding.Configure(preference);
      return binding;
    }

    private void OnEnable()
    {
      Display.onDisplaysUpdated += RefreshViewport;
      Subscribe();
    }

    /// <summary>Notify after a platform safe-area or viewport change when it has no Unity dimensions callback.</summary>
    public void RefreshViewport()
    {
      if (!isActiveAndEnabled || applying || setting?.Property == null || setting.Property.IsDisposed) return;
      Apply(setting.Value);
    }

    private void OnApplicationFocus(bool focused) { if (focused) RefreshViewport(); }

    private void Subscribe()
    {
      subscription?.Dispose();
      subscription = null;
      if (setting?.Property == null || setting.Property.IsDisposed) return;
      subscription = setting.Property.Subscribe(Apply);
    }

    private void Apply(float value)
    {
      if (applying) return;
      canvas = canvas != null ? canvas : GetComponent<Canvas>();
      scaler = scaler != null ? scaler : GetComponent<CanvasScaler>();
      if (canvas == null || scaler == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
      {
        Restore();
        return;
      }
      if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize &&
          scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
      {
        Restore();
        Debug.LogWarning("User UI scale requires Scale With Screen Size or Constant Pixel Size on its opted-in root.", this);
        return;
      }
      if (!captured)
      {
        authoredReference = scaler.referenceResolution;
        authoredScaleFactor = scaler.scaleFactor;
        captured = true;
      }
      Vector2 renderingSize = canvas.renderingDisplaySize;
      int display = canvas.targetDisplay;
      if (display > 0 && display < Display.displays.Length)
        renderingSize = new Vector2(Display.displays[display].renderingWidth, Display.displays[display].renderingHeight);
      Rect available = canvas.pixelRect;
      // RenderTexture previews and secondary displays do not inherit the primary screen's safe area.
      if (display == 0 && (canvas.worldCamera == null || canvas.worldCamera.targetTexture == null))
      {
        Rect safe = Screen.safeArea;
        available = Rect.MinMaxRect(Mathf.Max(available.xMin, safe.xMin), Mathf.Max(available.yMin, safe.yMin),
          Mathf.Min(available.xMax, safe.xMax), Mathf.Min(available.yMax, safe.yMax));
      }
      bool constantPixels = pixelSizeViaReference || scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize;
      float baseline = constantPixels ? authoredScaleFactor :
        UiScalePolicy.ResolutionFactor(renderingSize, authoredReference, scaler.screenMatchMode, scaler.matchWidthOrHeight);
      UiScaleCapacityResult next = UiScaleCapacity.Resolve(value, available.size, baseline, minimumLogicalViewport);
      float effective = next.HasValidViewport ? next.Effective :
        applied ? Mathf.Min(next.Requested, EffectiveUserScale) : next.Effective;
      float pixelBaseline = PositiveFinite(authoredScaleFactor) ? authoredScaleFactor : 1f;
      float scaleFactor = pixelBaseline * effective;
      // The Constant Pixel Size property setter has a .01 floor. A reference-resolution
      // representation gives Unity the same absolute pixel factor below that floor.
      bool referencePixels = constantPixels && scaleFactor < .01f;
      Vector2 reference = referencePixels ? renderingSize / scaleFactor : authoredReference / effective;
      if ((!constantPixels || referencePixels) &&
          (!PositiveFinite(reference.x) || !PositiveFinite(reference.y)))
      {
        // Invalid authored data or an unrepresentable division must never reach CanvasScaler.
        next = UiScaleCapacity.Resolve(value, Vector2.zero, baseline, minimumLogicalViewport);
        effective = applied ? Mathf.Min(next.Requested, EffectiveUserScale) : next.Effective;
        scaleFactor = pixelBaseline * effective;
        referencePixels = constantPixels && scaleFactor < .01f;
        reference = referencePixels ? renderingSize / scaleFactor : authoredReference / effective;
        if (!PositiveFinite(reference.x) || !PositiveFinite(reference.y))
        {
          effective = UiScalePolicy.Minimum;
          scaleFactor = pixelBaseline * effective;
          referencePixels = constantPixels && scaleFactor < .01f;
          reference = referencePixels && PositiveFinite(renderingSize.x) && PositiveFinite(renderingSize.y) ?
            renderingSize / scaleFactor : new Vector2(1920, 1080) / effective;
        }
      }
      bool geometryChanged = renderingSize != lastRenderingSize || available != lastAvailablePixels;
      bool constraintChanged = !applied || RequestedUserScale != next.Requested ||
        Capacity.MaximumSupported != next.MaximumSupported || Capacity.HasValidViewport != next.HasValidViewport ||
        Capacity.SupportsMinimum != next.SupportsMinimum || Capacity.IsConstrained != next.IsConstrained;
      bool scaleChanged = !applied || !Mathf.Approximately(EffectiveUserScale, effective);
      RequestedUserScale = next.Requested;
      Capacity = next;
      lastRenderingSize = renderingSize;
      lastAvailablePixels = available;
      bool requiresWrite = !applied || scaleChanged ||
        (!constantPixels || referencePixels ? scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
          scaler.referenceResolution != reference : scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize ||
          !Mathf.Approximately(scaler.scaleFactor, scaleFactor));
      applying = true;
      try
      {
        EffectiveUserScale = effective;
        if (requiresWrite)
        {
          if (!constantPixels || referencePixels)
          {
            pixelSizeViaReference = referencePixels;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = reference;
          }
          else
          {
            if (pixelSizeViaReference && PositiveFinite(authoredReference.x) && PositiveFinite(authoredReference.y))
              scaler.referenceResolution = authoredReference;
            pixelSizeViaReference = false;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scaleFactor;
          }
          applied = true;
          LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }
      }
      finally { applying = false; }
      if (scaleChanged) ScaleChanged?.Invoke(EffectiveUserScale);
      if (constraintChanged || scaleChanged) ConstraintChanged?.Invoke();
      if (geometryChanged) ViewportChanged?.Invoke();
    }

    private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnRectTransformDimensionsChange()
    {
      if (!applying && isActiveAndEnabled) RefreshViewport();
    }

    private void OnTransformParentChanged()
    {
      if (isActiveAndEnabled && setting != null) Apply(setting.Value);
    }

    private void Restore()
    {
      if (!applied || scaler == null) return;
      applying = true;
      try
      {
        if (pixelSizeViaReference) scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        pixelSizeViaReference = false;
        if (PositiveFinite(authoredReference.x) && PositiveFinite(authoredReference.y))
          scaler.referenceResolution = authoredReference;
        if (PositiveFinite(authoredScaleFactor)) scaler.scaleFactor = authoredScaleFactor;
        EffectiveUserScale = UiScalePolicy.Default;
        applied = false;
      }
      finally { applying = false; }
      ScaleChanged?.Invoke(EffectiveUserScale);
      ConstraintChanged?.Invoke();
    }

    private void OnDisable()
    {
      Display.onDisplaysUpdated -= RefreshViewport;
      subscription?.Dispose();
      subscription = null;
      Restore();
    }
  }
}
