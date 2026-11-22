using System;
using System.Collections.Generic;
using MToolKit.ContextMenus.Input;
using MToolKit.Runtime.Input.Prompts;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.ContextMenus.Hosting
{
  /// <summary>
  /// Host-owned runtime input for standalone profile assets. The existing input service retains authority
  /// over its own authored/runtime asset pair; only explicitly prepared foreign menu references are mapped.
  /// </summary>
  public sealed class MenuProfileActionSource : IDisposable
  {
    private sealed class OwnedAsset
    {
      public readonly InputActionAsset Clone;
      public readonly HashSet<Guid> RegisteredActions = new();
      public OwnedAsset(InputActionAsset clone) => Clone = clone;
    }

    private readonly IInputActionSource existing;
    private readonly Dictionary<InputActionAsset, OwnedAsset> owned = new();
    private readonly HashSet<InputActionAsset> ownedClones = new();
    private readonly PreparedSource prepared;
    private InputActionAsset firstClone;
    private bool disposed;

    public MenuProfileActionSource(IInputActionSource existing = null)
    { this.existing = existing; prepared = new PreparedSource(this); }

    public IInputActionSource ActiveSource => disposed ? null : owned.Count > 0 ? prepared : existing;
    /// <summary>Whether this host owns cloned input assets that require scoped rebinder registration.</summary>
    public bool HasOwnedActions => !disposed && owned.Count > 0;

    /// <summary>Prepares only the menu references named by this profile; cloning never enables their source assets.</summary>
    public void Prepare(MenuInputBindings bindings)
    {
      ThrowIfDisposed();
      if (bindings == null) return;
      if (existing != null && existing.RuntimeActions == null)
        throw new InvalidOperationException("Menu profile input requires the existing IInputActionSource to finish initializing its runtime asset.");
      Prepare(bindings.Navigate); Prepare(bindings.Point); Prepare(bindings.Click); Prepare(bindings.Submit);
      Prepare(bindings.Back); Prepare(bindings.Cancel); Prepare(bindings.PageForward); Prepare(bindings.PageBackward);
      Prepare(bindings.SwitchRing); Prepare(bindings.Hold);
      foreach (var reference in bindings.Secondary) Prepare(reference);
    }

    private void Prepare(InputActionReference reference)
    {
      if (reference == null) return;
      var action = reference.action;
      if (action == null) throw new ArgumentException("A menu profile input reference does not identify an existing action.", nameof(reference));
      var resolved = existing?.ResolveRuntimeAction(action) ?? action;
      if (!ReferenceEquals(action, resolved)) return;
      var asset = action.actionMap?.asset;
      if (asset == null || asset == existing?.RuntimeActions || ownedClones.Contains(asset)) return;

      if (!owned.TryGetValue(asset, out var registration))
      {
        var clone = Object.Instantiate(asset);
        clone.name = asset.name + " (Context Menu Runtime)";
        clone.Disable();
        registration = new OwnedAsset(clone);
        owned.Add(asset, registration);
        ownedClones.Add(clone);
        if (firstClone == null) firstClone = clone;
      }
      if (registration.Clone.FindAction(action.id) == null)
        throw new InvalidOperationException("The profile action '" + action.name + "' was added after its runtime asset was prepared; recreate the menu host to apply the new action structure.");
      registration.RegisteredActions.Add(action.id);
    }

    private InputAction Resolve(InputAction action)
    {
      ThrowIfDisposed();
      if (action == null) return null;
      var resolved = existing?.ResolveRuntimeAction(action) ?? action;
      if (!ReferenceEquals(action, resolved)) return resolved;
      var asset = action.actionMap?.asset;
      if (asset != null && owned.TryGetValue(asset, out var registration)
        && registration.RegisteredActions.Contains(action.id))
        return registration.Clone.FindAction(action.id)
          ?? throw new InvalidOperationException("The prepared menu runtime action no longer exists: " + action.name);
      return action;
    }

    private void ThrowIfDisposed()
    {
      if (disposed) throw new ObjectDisposedException(nameof(MenuProfileActionSource));
    }

    /// <summary>Dispose routers/leases before releasing their host-owned input assets.</summary>
    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      foreach (var registration in owned.Values)
      {
        var clone = registration.Clone;
        if (clone == null) continue;
        clone.Disable();
        if (Application.isPlaying) Object.Destroy(clone);
        else Object.DestroyImmediate(clone);
      }
      owned.Clear(); ownedClones.Clear(); firstClone = null;
    }

    private sealed class PreparedSource : IInputActionSource
    {
      private readonly MenuProfileActionSource owner;
      public PreparedSource(MenuProfileActionSource owner) => this.owner = owner;
      public InputActionAsset RuntimeActions
      {
        get
        {
          owner.ThrowIfDisposed();
          return owner.existing?.RuntimeActions != null ? owner.existing.RuntimeActions : owner.firstClone;
        }
      }
      public InputAction ResolveRuntimeAction(InputAction action) => owner.Resolve(action);
    }
  }
}
