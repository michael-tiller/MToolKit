using R3;

namespace MToolKit.Theme
{
  public interface IThemeService
  {
    ThemeRegistry ThemeRegistry { get; }

    Theme DefaultTheme { get; }
    Theme Current { get; }
    Observable<Theme> OnThemeUpdated { get; }
    Observable<(Theme Previous, Theme Current)> OnThemeChanged { get; }


    void SetTheme(string id, bool isSilent = false);
    void SetTheme(Theme theme, bool isSilent = false);
  }
}