using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Input;
using MToolKit.ContextMenus.Presentation;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  /// <summary>Shared authoring source. Runtime profiles change only on an explicit rebuild.</summary>
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Authoring/Common Settings", fileName = "MenuCommonSettings")]
  public sealed class MenuCommonSettings : ScriptableObject
  {
    [Tooltip("Sparse MTK theme tokens for this authoring layer. Null inherits the next layer.")]
    public MenuSkin ThemeOverrides;
    public MenuSkin Skin;
    public MenuInputBindings InputBindings = new MenuInputBindings();
    public MenuTransitionAsset Transition;
    public MenuAnchorPolicy Anchor = MenuAnchorPolicy.Centered;
    [Tooltip("Empty references preserve an existing output's composition; new outputs use native defaults.")]
    public GameObject ItemPrefab;
    public GameObject CenterPrefab;
    public GameObject SecondaryPrefab;
    public MenuProfile FallbackProfile;
    public bool ShowCenter = true;
    public bool ShowBackControl = true;
    public bool Backdrop = true;
    public bool ReducedMotion;
    public float MinimumTargetSize = 32;
    public float AnalogDeadZone = .25f;
    public float AnalogHysteresis = 5;
    [Tooltip("Seconds to retain analog selection after the stick returns to its dead zone. Zero clears immediately.")]
    public float AnalogSelectionResetDelay = .35f;
  }
}
