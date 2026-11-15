using System;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Input;
using MToolKit.Runtime.Input.Prompts;
using MToolKit.Theme;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>A prefab-hosted presenter owns its visuals, layout, animation and semantic input.</summary>
  public interface IContextMenuPresenter : IMenuInputTarget
  {
    RectTransform Root { get; }
    void Bind(MenuSession session, MenuProfile profile, MenuPresentationContext context);
    void Unbind();
  }

  /// <summary>Optional containment result used by hosts to select an accessible fallback profile.</summary>
  public interface IMenuViewportAwarePresenter
  {
    bool FitsViewport { get; }
    string PlacementDiagnostic { get; }
  }

  public interface IMenuContainmentEvents
  {
    event Action ContainmentChanged;
  }

  /// <summary>Framework services and the requested placement, without game-specific dependencies.</summary>
  public sealed class MenuPresentationContext : IDisposable
  {
    private readonly MenuLabelResolver labels;
    private readonly Func<InputAction, InputAction> resolveInput;
    public MenuPresentationContext(Canvas canvas, Vector2 screenPosition, IThemeService themes = null,
      IInputPromptService prompts = null, Func<MenuLabel, string> labelOverride = null,
      Func<InputAction, InputAction> resolveInput = null, MenuAnchorPolicy? anchorOverride = null,
      Func<bool> backInputAllowed = null, MenuInputOwnership inputOwnership = null)
    {
      Canvas = canvas != null ? canvas.rootCanvas : throw new ArgumentNullException(nameof(canvas));
      ScreenPosition = screenPosition; Themes = themes; Prompts = prompts;
      this.resolveInput = resolveInput;
      AnchorOverride = anchorOverride; BackInputAllowed = backInputAllowed;
      InputOwnership = inputOwnership ?? MenuInputOwnership.Shared;
      labels = new MenuLabelResolver(labelOverride);
      labels.Changed += OnLabelsChanged;
    }
    public Canvas Canvas { get; }
    public Vector2 ScreenPosition { get; }
    public IThemeService Themes { get; }
    public IInputPromptService Prompts { get; }
    public MenuInputOwnership InputOwnership { get; }
    public MenuAnchorPolicy? AnchorOverride { get; }
    private Func<bool> BackInputAllowed { get; }
    public bool CanHandleBackInput => BackInputAllowed?.Invoke() ?? true;
    public MenuAnchorPolicy ResolveAnchor(MenuAnchorPolicy profileAnchor) => AnchorOverride ?? profileAnchor;
    public event Action LabelsChanged;
    public string Resolve(MenuLabel label) => labels.Resolve(label);
    public InputAction ResolveInputAction(InputActionReference reference)
      => reference == null ? null : resolveInput?.Invoke(reference.action) ?? reference.action;
    private void OnLabelsChanged() => LabelsChanged?.Invoke();
    public void Dispose() { labels.Changed -= OnLabelsChanged; labels.Dispose(); LabelsChanged = null; }
  }
}
