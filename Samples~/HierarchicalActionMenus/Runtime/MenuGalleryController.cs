using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Authoring;
using MToolKit.ContextMenus.Hosting;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.UI.Scaling;
using MToolKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using VContainer;
using ThemeAsset = MToolKit.Theme.Theme;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Samples
{
  /// <summary>Standalone, event-driven gallery for one shared action hierarchy and interchangeable presentations.</summary>
  public sealed class MenuGalleryController : MonoBehaviour
  {
    public const string WheelContextMenu = "WheelContextMenu", PieContextMenu = "PieContextMenu",
      ListContextMenu = "ListContextMenu", NestedWheelContextMenu = "NestedWheelContextMenu";
    [field: SerializeField] public ContextMenuHost Host { get; set; }
    [field: SerializeField] public MenuHostConfig HostConfig { get; set; }
    [field: SerializeField] public MenuActionSetSO ActionSet { get; set; }
    [field: SerializeField] public InputActionAsset InputActions { get; set; }
    [field: SerializeField] public Canvas Canvas { get; set; }
    [field: SerializeField] public InputSystemUIInputModule InputModule { get; set; }
    [field: SerializeField] public TMP_Text Status { get; set; }
    [field: SerializeField] public TMP_Text Hints { get; set; }
    [field: SerializeField] public UserUiScaleCanvas ScaleCanvas { get; set; }
    [field: SerializeField] public MenuGalleryTheme StandaloneTheme { get; set; }
    [field: SerializeField] public bool OwnStandaloneTheme { get; set; }
    [field: SerializeField] public ThemeAsset DarkTheme { get; set; }
    [field: SerializeField] public ThemeAsset LightTheme { get; set; }

    private readonly List<InputActionReference> runtimeReferences = new();
    private readonly MenuGalleryState state = new();
    private InputService input;
    private IInputPromptService prompts, injectedPrompts;
    private InputPromptService ownedPrompts;
    private IThemeService themes, injectedThemes;
    private ThemeAsset priorTheme;
    private ContextMenuHostService service;
    private IDisposable hintBinding;
    private ReactiveSetting<float> scale;
    private InputAction point, open, hold, fire, move, themeAction, scaleUpAction, scaleDownAction, styleAction;
    private Vector2 pointer;
    private bool hasPointer, initialized, changedTheme;
    private string selectedStyle = NestedWheelContextMenu;
    private MenuContext currentContext;
    public MenuGalleryState State => state;
    public IContextMenuHostService MenuService => service;

    [Inject]
    public void Construct(IInputPromptService prompts = null, IThemeService themes = null)
    { injectedPrompts = prompts; injectedThemes = themes; }

    private void Start() => StartAsync().Forget(Report);

    private async UniTask StartAsync()
    {
      Initialize();
      await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
      await OpenAsync(NestedWheelContextMenu, ScreenCenter());
    }

    private void Initialize()
    {
      if (initialized) return;
      if (Host == null || HostConfig == null || ActionSet == null || InputActions == null || Canvas == null)
        throw new InvalidOperationException("The action-menu gallery requires its generated host, action set, input asset and canvas references.");
      input = new InputService();
      input.Initialize(InputActions);
      input.Enable();
      ConfigureEventSystem();
      themes = injectedThemes ?? (IThemeService)StandaloneTheme;
      priorTheme = themes?.Current;
      prompts = injectedPrompts;
      if (prompts == null) prompts = ownedPrompts = new InputPromptService(actionSource: input, actions: input.RuntimeActions);
      service = new ContextMenuHostService(HostConfig, actions: input, prompts: prompts, themes: themes);
      Host.Construct(service);
      point = Required("UI/Point"); open = Required("Shared/OpenContext"); hold = Required("Shared/HoldWheel");
      fire = Required("Player/Fire"); move = Required("Player/Move");
      themeAction = Required("Shared/Theme"); scaleUpAction = Required("Shared/ScaleUp");
      scaleDownAction = Required("Shared/ScaleDown"); styleAction = Required("Shared/NextStyle");
      point.performed += OnPoint; open.performed += OnOpen; hold.performed += OnHold;
      fire.performed += OnGameplayFire; move.performed += OnGameplayMove;
      themeAction.performed += OnTheme; scaleUpAction.performed += OnScaleUp; scaleDownAction.performed += OnScaleDown;
      styleAction.performed += OnNextStyle;
      state.Changed += RefreshStatus;
      scale = new ReactiveSetting<float>(1, "ActionMenuGallery.UiScale");
      if (ScaleCanvas != null) ScaleCanvas.Configure(scale);
      if (Hints != null) hintBinding = prompts.Bind(Hints, RenderHints);
      initialized = true;
      RefreshStatus();
    }

    private InputAction Required(string path)
      => input.RuntimeActions.FindAction(path) ?? throw new InvalidOperationException("The gallery input asset is missing '" + path + "'.");

    private InputActionReference Reference(string path)
    {
      var reference = InputActionReference.Create(Required(path));
      runtimeReferences.Add(reference); return reference;
    }

    private void ConfigureEventSystem()
    {
      if (InputModule == null) return;
      InputModule.actionsAsset = input.RuntimeActions;
      InputModule.point = Reference("UI/Point"); InputModule.leftClick = Reference("UI/Click");
      InputModule.rightClick = Reference("UI/RightClick"); InputModule.scrollWheel = Reference("UI/Scroll");
      InputModule.move = Reference("UI/Navigate"); InputModule.submit = Reference("UI/Submit"); InputModule.cancel = Reference("UI/Cancel");
    }

    private void OnPoint(InputAction.CallbackContext context) { pointer = context.ReadValue<Vector2>(); hasPointer = true; }
    private void OnOpen(InputAction.CallbackContext context)
    {
      if (service.IsOpen) { Host.Close(); return; }
      OpenAsync(selectedStyle, context.control?.device is Gamepad ? ScreenCenter() : hasPointer ? pointer : ScreenCenter()).Forget(Report);
    }
    private void OnHold(InputAction.CallbackContext context)
    {
      if (!service.IsOpen) OpenAsync(WheelContextMenu, ScreenCenter(), true).Forget(Report);
    }
    private void OnGameplayFire(InputAction.CallbackContext _) => state.RecordGameplayFire();
    private void OnGameplayMove(InputAction.CallbackContext _) => state.RecordGameplayMove();
    private void OnTheme(InputAction.CallbackContext _) => ToggleTheme();
    private void OnScaleUp(InputAction.CallbackContext _) => IncreaseScale();
    private void OnScaleDown(InputAction.CallbackContext _) => DecreaseScale();
    private void OnNextStyle(InputAction.CallbackContext _)
    {
      string[] styles = { WheelContextMenu, PieContextMenu, ListContextMenu, NestedWheelContextMenu };
      int index = Array.IndexOf(styles, selectedStyle);
      OpenStyle(styles[(index + 1) % styles.Length]);
    }

    public void OpenStyle(string style)
    {
      Initialize();
      OpenAsync(style, ScreenCenter()).Forget(Report);
    }
    public void OpenAtEdge()
    {
      Initialize();
      OpenAsync(selectedStyle, new Vector2(Screen.width - 8, Screen.height - 8)).Forget(Report);
    }
    public async UniTask OpenAsync(string style, Vector2 position, bool activationHeld = false)
    {
      Initialize();
      selectedStyle = style;
      currentContext = new MenuContext(state, () => this != null && isActiveAndEnabled && state.IsValid,
        cancellationToken: this.GetCancellationTokenOnDestroy());
      var gameplay = new List<InputAction>();
      var gameplayMap = input.RuntimeActions.FindActionMap("Player");
      if (gameplayMap != null) foreach (var action in gameplayMap.actions) gameplay.Add(action);
      await Host.OpenAsync(new MenuOpenRequest(ActionSet.Create(currentContext), currentContext, Canvas,
        position, profileName: style, gameplayActions: gameplay, activationHeld: activationHeld), this.GetCancellationTokenOnDestroy());
      RefreshStatus();
    }
    public void ToggleCommands() { state.ToggleCommands(); RefreshMenu(); }
    public void ToggleSecondary() { state.ToggleVariants(); RefreshMenu(); }
    public void InvalidateTarget() { state.Invalidate(); currentContext?.Invalidate(); }
    public void RestoreTarget() => state.Restore();
    public void IncreaseScale() => SetScale(.25f);
    public void DecreaseScale() => SetScale(-.25f);
    private void SetScale(float delta)
    {
      Initialize(); scale.Value = UiScalePolicy.Normalize(scale.Value + delta); RefreshStatus();
    }
    public void ToggleTheme()
    {
      Initialize();
      if (themes == null || DarkTheme == null || LightTheme == null) return;
      themes.SetTheme(themes.Current == LightTheme ? DarkTheme : LightTheme);
      changedTheme = true;
    }
    private void RefreshMenu()
    {
      if (service?.Session?.IsOpen == true) service.Session.RefreshAsync().Forget(Report);
    }
    private Vector2 ScreenCenter() => new(Screen.width * .5f, Screen.height * .5f);
    private string RenderHints()
    {
      string Hint(string path)
      {
        var action = Required(path);
        return prompts?.Resolve(action, InputPromptStyle.GlyphAndName) ?? action.GetBindingDisplayString();
      }
      return "Open " + Hint("Shared/OpenContext") + "   Hold wheel " + Hint("Shared/HoldWheel")
        + "   Confirm " + Hint("Menu/Submit") + "   Back " + Hint("Menu/Back")
        + "   Ring " + Hint("Menu/SwitchRing") + "   Variant " + Hint("Menu/Secondary");
    }
    private void RefreshStatus()
    {
      if (Status == null) return;
      Status.text = "Style: " + selectedStyle + "  |  Target: " + (state.IsValid ? "valid" : "invalid")
        + "  |  Commands: " + (state.CommandsEnabled ? "enabled" : "disabled")
        + "\nCommands executed: " + state.CommandsExecuted + "  |  Variant changes: " + state.VariantChanges
        + "  |  UI scale requested/applied: " + (scale != null ? UiScalePolicy.Percentage(scale.Value) : 100) + "%/"
        + (ScaleCanvas != null ? UiScalePolicy.Percentage(ScaleCanvas.EffectiveUserScale) : 100) + "%"
        + "\nGameplay callbacks while menus are closed — Fire: " + state.GameplayFireEvents + ", Move: " + state.GameplayMoveEvents
        + "\nLast action: " + state.LastCommand;
    }
    private static void Report(Exception exception)
    {
      if (!(exception is OperationCanceledException)) Debug.LogException(exception);
    }
    private void OnEnable()
    {
      if (initialized) input?.Enable();
    }
    private void OnDisable()
    {
      service?.Close();
      input?.Disable();
    }
    private void OnDestroy()
    {
      state.Changed -= RefreshStatus;
      if (point != null) point.performed -= OnPoint;
      if (open != null) open.performed -= OnOpen;
      if (hold != null) hold.performed -= OnHold;
      if (fire != null) fire.performed -= OnGameplayFire;
      if (move != null) move.performed -= OnGameplayMove;
      if (themeAction != null) themeAction.performed -= OnTheme;
      if (scaleUpAction != null) scaleUpAction.performed -= OnScaleUp;
      if (scaleDownAction != null) scaleDownAction.performed -= OnScaleDown;
      if (styleAction != null) styleAction.performed -= OnNextStyle;
      service?.Dispose(); service = null;
      hintBinding?.Dispose(); ownedPrompts?.Dispose();
      if (ScaleCanvas != null) ScaleCanvas.Configure(null);
      scale?.Dispose();
      if (InputModule != null) InputModule.enabled = false;
      foreach (var reference in runtimeReferences) if (reference != null) Object.Destroy(reference);
      runtimeReferences.Clear();
      input?.Dispose();
      if (changedTheme && injectedThemes != null && priorTheme != null) injectedThemes.SetTheme(priorTheme);
      if (OwnStandaloneTheme && StandaloneTheme != null) Object.Destroy(StandaloneTheme.gameObject);
    }
  }
}
