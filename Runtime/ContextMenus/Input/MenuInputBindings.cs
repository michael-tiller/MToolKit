using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.ContextMenus.Input
{
  /// <summary>Authored action references; runtime identity and rebinding remain owned by MTK input services.</summary>
  [Serializable]
  public sealed class MenuInputBindings
  {
    [SerializeField] private InputActionReference navigate;
    [SerializeField] private InputActionReference point;
    [SerializeField] private InputActionReference click;
    [SerializeField] private InputActionReference submit;
    [SerializeField] private InputActionReference back;
    [SerializeField] private InputActionReference cancel;
    [SerializeField] private InputActionReference pageForward;
    [SerializeField] private InputActionReference pageBackward;
    [SerializeField] private InputActionReference switchRing;
    [SerializeField] private InputActionReference hold;
    [SerializeField] private InputActionReference[] secondary = Array.Empty<InputActionReference>();
    [SerializeField] private bool useNativeUguiNavigation;
    [SerializeField] private bool routePointerClick;

    public InputActionReference Navigate => navigate;
    public InputActionReference Point => point;
    public InputActionReference Click => click;
    public InputActionReference Submit => submit;
    public InputActionReference Back => back;
    public InputActionReference Cancel => cancel;
    public InputActionReference PageForward => pageForward;
    public InputActionReference PageBackward => pageBackward;
    public InputActionReference SwitchRing => switchRing;
    public InputActionReference Hold => hold;
    public System.Collections.Generic.IReadOnlyList<InputActionReference> Secondary => secondary;
    /// <summary>Native Selectable navigation owns Move/Submit/Cancel; the router never emits a second operation.</summary>
    public bool UseNativeUguiNavigation => useNativeUguiNavigation;
    /// <summary>Enable only when the presenter routes pointer activation through this target rather than native Button clicks.</summary>
    public bool RoutePointerClick => routePointerClick;

    public void Configure(InputActionReference navigate = null, InputActionReference point = null,
      InputActionReference click = null, InputActionReference submit = null, InputActionReference back = null,
      InputActionReference cancel = null, InputActionReference pageForward = null, InputActionReference pageBackward = null,
      InputActionReference switchRing = null, InputActionReference hold = null, InputActionReference[] secondary = null,
      bool useNativeUguiNavigation = false, bool routePointerClick = false)
    {
      this.navigate = navigate; this.point = point; this.click = click; this.submit = submit;
      this.back = back; this.cancel = cancel; this.pageForward = pageForward; this.pageBackward = pageBackward;
      this.switchRing = switchRing; this.hold = hold;
      this.secondary = secondary == null ? Array.Empty<InputActionReference>() : (InputActionReference[])secondary.Clone();
      this.useNativeUguiNavigation = useNativeUguiNavigation; this.routePointerClick = routePointerClick;
    }
  }
}
