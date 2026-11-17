using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.ContextMenus.Configuration;
using MToolKit.ContextMenus.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.ContextMenus.Hosting
{
  public sealed class MenuOpenRequest
  {
    public MenuOpenRequest(IReadOnlyList<MenuAction> actions, MenuContext context, Canvas canvas,
      Vector2 screenPosition, string profileName = null, MenuProfile profile = null,
      IEnumerable<InputAction> gameplayActions = null, bool activationHeld = false,
      Func<MenuLabel, string> labelResolver = null, MenuAnchorPolicy? anchorOverride = null,
      Func<bool> backInputAllowed = null)
    {
      if (actions == null) throw new ArgumentNullException(nameof(actions));
      var snapshot = new MenuAction[actions.Count];
      for (int i = 0; i < snapshot.Length; i++) snapshot[i] = actions[i];
      Actions = Array.AsReadOnly(snapshot);
      Context = context ?? throw new ArgumentNullException(nameof(context));
      Canvas = canvas != null ? canvas.rootCanvas : throw new ArgumentNullException(nameof(canvas));
      ScreenPosition = screenPosition; ProfileName = profileName; Profile = profile;
      GameplayActions = gameplayActions; ActivationHeld = activationHeld; LabelResolver = labelResolver;
      AnchorOverride = anchorOverride; BackInputAllowed = backInputAllowed;
    }
    public IReadOnlyList<MenuAction> Actions { get; }
    public MenuContext Context { get; }
    public Canvas Canvas { get; }
    public Vector2 ScreenPosition { get; }
    public string ProfileName { get; }
    public MenuProfile Profile { get; }
    public IEnumerable<InputAction> GameplayActions { get; }
    public bool ActivationHeld { get; }
    public Func<MenuLabel, string> LabelResolver { get; }
    public MenuAnchorPolicy? AnchorOverride { get; }
    public Func<bool> BackInputAllowed { get; }
  }

  public interface IContextMenuHostService : IDisposable
  {
    bool IsOpen { get; }
    MenuSession Session { get; }
    IContextMenuPresenter Presenter { get; }
    UniTask OpenAsync(MenuOpenRequest request, CancellationToken cancellationToken = default);
    UniTask PrewarmAsync(MenuProfile profile, CancellationToken cancellationToken = default);
    void Close();
  }

}
