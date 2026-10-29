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

    private void OnEnable() => Subscribe();

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
      applying = true;
      try
      {
        EffectiveUserScale = UiScalePolicy.Normalize(value);
        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
          scaler.referenceResolution = UiScalePolicy.ReferenceResolution(authoredReference, EffectiveUserScale);
        else
          scaler.scaleFactor = authoredScaleFactor * EffectiveUserScale;
        applied = true;
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
      }
      finally { applying = false; }
      ScaleChanged?.Invoke(EffectiveUserScale);
    }

    private void OnRectTransformDimensionsChange()
    {
      if (!applying && isActiveAndEnabled) ViewportChanged?.Invoke();
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
        scaler.referenceResolution = authoredReference;
        scaler.scaleFactor = authoredScaleFactor;
        EffectiveUserScale = UiScalePolicy.Default;
        applied = false;
      }
      finally { applying = false; }
      ScaleChanged?.Invoke(EffectiveUserScale);
    }

    private void OnDisable()
    {
      subscription?.Dispose();
      subscription = null;
      Restore();
    }
  }
}
