using MToolKit.ContextMenus.Configuration;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  /// <summary>Reusable Editor source; visual settings bake into an existing runtime profile.</summary>
  public abstract class MenuPresentationThemeSet : ScriptableObject
  {
    public MenuSkin ThemeOverrides;
    public GameObject PresenterPrefab;
    public GameObject ItemPrefab;
    public GameObject CenterPrefab;
    public GameObject SecondaryPrefab;
    public int MaximumVisibleItems = 8;
    public Vector2 WidgetSize = new Vector2(96, 64);
    public bool ShowCenter = true;
    public bool Backdrop = true;
    public abstract void ApplyTo(MenuProfile profile);
    internal void ApplyComposition(MenuProfile profile)
    {
      if (profile == null) throw new System.ArgumentNullException(nameof(profile));
      profile.ThemeOverrides = ThemeOverrides;
      if (PresenterPrefab != null) profile.PresenterPrefab = PresenterPrefab;
      if (ItemPrefab != null) profile.ItemPrefab = ItemPrefab;
      if (CenterPrefab != null) profile.CenterPrefab = CenterPrefab;
      if (SecondaryPrefab != null) profile.SecondaryPrefab = SecondaryPrefab;
      profile.MaximumVisibleItems = MaximumVisibleItems;
      profile.WidgetSize = WidgetSize;
      profile.ShowCenter = ShowCenter;
      profile.Backdrop = Backdrop;
    }
  }
}
