using System;
using MToolKit.Theme;
using R3;
using UnityEngine;
using ThemeAsset = MToolKit.Theme.Theme;

namespace MToolKit.ContextMenus.Samples
{
  /// <summary>Scene-owned sample theme service. It never registers or replaces the application's CurrentTheme singleton.</summary>
  public sealed class MenuGalleryTheme : MonoBehaviour, IThemeService
  {
    [field: SerializeField] public ThemeRegistry ThemeRegistry { get; set; }
    [field: SerializeField] public ThemeAsset DefaultTheme { get; set; }
    private readonly ReactiveProperty<ThemeAsset> updated = new();
    public ThemeAsset Current { get; private set; }
    public Observable<ThemeAsset> OnThemeUpdated => updated;
    public Observable<(ThemeAsset Previous, ThemeAsset Current)> OnThemeChanged => updated.Pairwise();
    private void Awake()
    {
      var initial = DefaultTheme;
      if (initial == null && ThemeRegistry != null && ThemeRegistry.Themes != null && ThemeRegistry.Themes.Length > 0)
        initial = ThemeRegistry.Themes[0];
      if (initial == null) throw new InvalidOperationException("The gallery's scoped theme service needs its generated default theme.");
      SetTheme(initial);
    }
    public void SetTheme(string id, bool isSilent = false)
      => SetTheme(ThemeRegistry != null ? ThemeRegistry.GetTheme(id) : null, isSilent);
    public void SetTheme(ThemeAsset theme, bool isSilent = false)
    {
      if (theme == null) throw new ArgumentNullException(nameof(theme));
      Current = theme;
      if (!isSilent) updated.Value = theme;
    }
    private void OnDestroy() => updated.Dispose();
  }
}
