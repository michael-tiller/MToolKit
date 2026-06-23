using R3;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Theme
{
  /// <summary>
  ///   Base for components that bind a themed asset (swatch, typeset, ...) to a target component and
  ///   follow <see cref="CurrentTheme" />. <c>start</c> is the immutable wired anchor; on every theme
  ///   change the same-Id asset is resolved from the new theme and applied, falling back to <c>start</c>
  ///   when the new theme has no asset under that Id. Enables light/dark and localization-font themes.
  /// </summary>
  /// <typeparam name="TAsset">The themed ScriptableObject; its asset name is its stable Id.</typeparam>
  public abstract class ThemePreset<TAsset> : MonoBehaviour where TAsset : ScriptableObject
  {
    [SerializeField]
    [Required]
    private TAsset start;

    [SerializeField]
    [InfoBox(
      "Theme override is ON: this preset ignores the active theme and always shows its configured asset.",
      InfoMessageType.Warning,
      nameof(overrideTheme))]
    private bool overrideTheme;

    [ShowInInspector]
    [ReadOnly]
    private TAsset cached;

    private readonly CompositeDisposable _disposables = new();

    /// <summary>
    ///   The asset currently in effect: the wired <c>start</c> while overriding (or before any
    ///   resolve), otherwise the theme-resolved <c>cached</c>.
    /// </summary>
    protected TAsset Current => !overrideTheme && cached != null ? cached : start;

    /// <summary>Look up the same-Id asset in <paramref name="theme" />; which registry is asset-specific.</summary>
    protected abstract TAsset Resolve(Theme theme, string id);

    /// <summary>Push <paramref name="asset" /> onto the concrete target (graphic color, TMP font, ...).</summary>
    protected abstract void Apply(TAsset asset);

    /// <summary>Auto-wire the target component when the component is first added (Unity <c>Reset</c>).</summary>
    protected virtual void AutoWire() { }

    private void Reset()
    {
      AutoWire();
    }

    private void OnValidate()
    {
      ApplyCurrent();
      // edit-time preview
    }

    private void Start()
    {
      // OnThemeUpdated is a ReactiveProperty -> subscribing immediately replays the current theme,
      // so this both applies now and follows every future change.
      if (CurrentTheme.HasInstance)
        CurrentTheme.Instance.OnThemeUpdated.Subscribe(ApplyTheme).AddTo(_disposables);
      else
        ApplyCurrent(); // no theme service -> show the wired `start`
    }

    private void OnDestroy()
    {
      _disposables.Dispose();
    }

    private void ApplyTheme(Theme theme)
    {
      if (start == null) return;

      if (!overrideTheme) // emergency hatch: when overriding, always show the configured `start`
      {
        TAsset next = theme != null ? Resolve(theme, start.name) : null; // name == Id, stable across themes
        if (next != null) cached = next;
        else if (cached == null) cached = start; // no match in the new theme -> keep what we have
      }

      Apply(Current);
    }

    private void ApplyCurrent()
    {
      if (Current != null) Apply(Current);
    }
  }
}