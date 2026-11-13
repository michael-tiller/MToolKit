using System;
using System.Collections.Generic;
using System.Threading;
using MToolKit.Runtime.Localization;
using R3;
using Serilog;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MToolKit.Runtime.Input.Prompts
{
  public sealed class InputPromptService : IInputPromptService, IDisposable
  {
    private static readonly Lazy<Serilog.ILogger> log = new(() => Log.Logger.ForContext<InputPromptService>().ForFeature("Input.Prompts"));
    private readonly Subject<Unit> changed = new();
    private readonly CompositeDisposable subscriptions = new();
    private readonly Dictionary<InputAction, int> watchedActions = new();
    private readonly HashSet<TMP_SpriteAsset> reportedSpriteCollisions = new();
    private HashSet<InputAction> renderingActions;
    private readonly InputPromptSettings settings;
    private readonly IInputActionSource actionSource;
    private readonly InputActionAsset actions;
    private readonly PlayerInput playerInput;
    private readonly InputPromptUnityLocalizer unityLocalizer;
    private readonly IInputPromptLocalizer localizer;
    private readonly SynchronizationContext synchronizationContext;
    private readonly int ownerThread;
    private InputGlyphCatalog catalog;
    private InputPromptResolver resolver;
    private InputPromptContext? explicitContext;
    private InputDevice activeDevice;
    private string activeGroup;
    private TMP_FontAsset renderFont;
    private int resolving;
    private bool notifying;
    private bool disposed;

    public InputPromptService(InputPromptSettings settings = null, IInputActionSource actionSource = null,
      ILocalizationService localization = null, IInputBindingChangeSource bindingChanges = null,
      InputActionAsset actions = null, PlayerInput playerInput = null, IInputPromptLocalizer localizer = null)
    {
      this.settings = settings;
      this.actionSource = actionSource;
      this.actions = actions;
      this.playerInput = playerInput;
      synchronizationContext = SynchronizationContext.Current;
      ownerThread = Thread.CurrentThread.ManagedThreadId;
      unityLocalizer = new InputPromptUnityLocalizer(() => this.settings != null ? this.settings.LocalizationTable : null, NotifyChanged);
      this.localizer = localizer ?? unityLocalizer;
      RefreshConfiguration();
      if (settings != null) settings.Changed += RefreshConfiguration;
      InputSystem.onActionChange += OnActionChange;
      InputSystem.onEvent += OnInputEvent;
      InputSystem.onDeviceChange += OnDeviceChange;
      InputUser.onChange += OnUserChange;
      LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
      if (localization != null)
        subscriptions.Add(localization.Language.Subscribe(_ => OnLocaleChanged(null)));
      if (bindingChanges != null)
        subscriptions.Add(bindingChanges.BindingChanges.Subscribe(value =>
        {
          if (IsRelevant(value.action)) NotifyChanged();
        }));
      RefreshPlayerContext();
    }

    public Observable<Unit> Changed => changed;

    /// <summary>Automatic context follows relevant action activity or this specific player's paired devices, never Gamepad.current.</summary>
    public InputPromptContext Context
    {
      get
      {
        if (explicitContext.HasValue) return explicitContext.Value;
        bool previewGroup = settings != null && !string.IsNullOrEmpty(settings.BindingGroup);
        return new InputPromptContext(previewGroup ? null : activeDevice,
          previewGroup ? settings.BindingGroup : activeGroup,
          settings != null && settings.OverrideFamily ? settings.Family : (InputGlyphFamily?)null);
      }
    }

    public void SetContext(InputPromptContext? context)
    {
      ThrowIfDisposed();
      explicitContext = context;
      NotifyChanged();
    }

    /// <summary>The supplied action is the actual execution instance and is never silently replaced.</summary>
    public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
    {
      ThrowIfDisposed();
      if (action != null && renderingActions != null && renderingActions.Add(action))
      {
        watchedActions.TryGetValue(action, out int count);
        watchedActions[action] = count + 1;
      }
      resolving++;
      try
      {
        RegisterSpriteAssets();
        return resolver.Resolve(action, EffectiveStyle(style), EffectiveContext(context));
      }
      finally { resolving--; }
    }

    /// <summary>Maps an authored reference through InputService when it owns that asset, preserving foreign live actions.</summary>
    public string ResolveReference(InputActionReference action, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
      => Resolve(actionSource?.ResolveRuntimeAction(action != null ? action.action : null)
        ?? (action != null ? action.action : null), style, context);

    public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto, InputPromptContext? context = null)
    {
      ThrowIfDisposed();
      RegisterSpriteAssets();
      return resolver.ResolvePath(path, EffectiveStyle(style), EffectiveContext(context));
    }

    /// <summary>Formats trusted translated content/arguments. Use Compose with PlainText for external text.</summary>
    public string Localize(string key, string fallback, params object[] arguments)
    {
      ThrowIfDisposed();
      return unityLocalizer.Localize(key, fallback, arguments);
    }

    public string Compose(string key, string fallback, params InputPromptArgument[] arguments)
    {
      var values = new Dictionary<string, object>(StringComparer.Ordinal);
      foreach (var argument in arguments ?? Array.Empty<InputPromptArgument>())
      {
        if (string.IsNullOrWhiteSpace(argument.Name)) throw new ArgumentException("A prompt argument requires a name.", nameof(arguments));
        if (values.ContainsKey(argument.Name)) throw new ArgumentException("Prompt argument names must be unique.", nameof(arguments));
        values.Add(argument.Name, argument.IsText ? InputPromptRichText.Escape(argument.Text)
          : Resolve(argument.Action, argument.Style, argument.Context));
      }
      return Localize(key, fallback, values);
    }

    public IDisposable Bind(TMP_Text text, Func<string> render)
    {
      ThrowIfDisposed();
      if (text == null) throw new ArgumentNullException(nameof(text));
      if (render == null) throw new ArgumentNullException(nameof(render));
      var boundActions = new HashSet<InputAction>();
      bool bindingDisposed = false;
      void ReleaseActions()
      {
        foreach (var action in boundActions)
        {
          if (!watchedActions.TryGetValue(action, out int count)) continue;
          if (count > 1) watchedActions[action] = count - 1;
          else watchedActions.Remove(action);
        }
        boundActions.Clear();
      }
      void Refresh()
      {
        if (text == null || disposed || bindingDisposed) return;
        ReleaseActions();
        var previousFont = renderFont;
        var previousActions = renderingActions;
        renderFont = text.font;
        renderingActions = boundActions;
        try
        {
          text.richText = true;
          var value = render() ?? string.Empty;
          if (text.text != value) text.text = value;
        }
        finally { renderFont = previousFont; renderingActions = previousActions; }
      }
      var subscription = changed.Subscribe(_ => Refresh());
      try { Refresh(); }
      catch { subscription.Dispose(); ReleaseActions(); throw; }
      return Disposable.Create(() =>
      {
        if (bindingDisposed) return;
        bindingDisposed = true;
        subscription.Dispose();
        ReleaseActions();
      });
    }

    private InputPromptStyle EffectiveStyle(InputPromptStyle style)
      => style == InputPromptStyle.Auto && settings != null ? settings.Style : style;

    private InputPromptContext EffectiveContext(InputPromptContext? requested)
    {
      var context = requested ?? Context;
      return new InputPromptContext(context.Device, context.BindingGroup, context.FamilyOverride,
        context.BindingIndex, context.FontAsset != null ? context.FontAsset : renderFont);
    }

    private void RefreshConfiguration()
    {
      if (disposed) return;
      var next = settings != null ? settings.Catalog : null;
      if (catalog != next)
      {
        if (catalog != null) catalog.Changed -= NotifyChanged;
        catalog = next;
        if (catalog != null) catalog.Changed += NotifyChanged;
      }
      resolver = new InputPromptResolver(catalog, localizer) { SpriteAvailable = IsSpriteRegistered };
      NotifyChanged();
    }

    private void RegisterSpriteAssets()
    {
      foreach (var asset in resolver.SpriteAssets)
      {
        if (asset == null || asset.material == null || asset.spriteSheet == null) continue;
        int hash = TMP_TextUtilities.GetHashCode(asset.name);
        if (!MaterialReferenceManager.TryGetSpriteAsset(hash, out var registered))
          MaterialReferenceManager.AddSpriteAsset(hash, asset);
        else if (registered != asset && reportedSpriteCollisions.Add(asset))
          log.Value.Warning("Input glyph sprite assets share the native TMP name {SpriteAssetName}; give each atlas a unique name", asset.name);
      }
    }

    private static bool IsSpriteRegistered(TMP_SpriteAsset asset)
      => MaterialReferenceManager.TryGetSpriteAsset(TMP_TextUtilities.GetHashCode(asset.name), out var registered)
        && registered == asset;

    private InputActionAsset RelevantAsset => playerInput != null ? playerInput.actions : actions ?? actionSource?.RuntimeActions;

    private bool IsRelevant(InputAction action)
    {
      if (action == null) return false;
      var owner = RelevantAsset;
      if (actions != null || playerInput != null) return owner != null && action.actionMap?.asset == owner;
      return watchedActions.ContainsKey(action) || owner != null && action.actionMap?.asset == owner
        || actionSource != null && !ReferenceEquals(actionSource.ResolveRuntimeAction(action), action);
    }

    private bool IsRelevant(object value)
    {
      if (value is InputAction action) return IsRelevant(action);
      if (value is InputActionMap map)
      {
        if (RelevantAsset != null && map.asset == RelevantAsset) return true;
        if (actions != null || playerInput != null) return false;
        if (actionSource != null)
          foreach (var candidate in map.actions)
            if (!ReferenceEquals(actionSource.ResolveRuntimeAction(candidate), candidate)) return true;
        foreach (var watched in watchedActions.Keys) if (watched.actionMap == map) return true;
      }
      if (value is InputActionAsset asset)
      {
        if (asset == null) return false;
        if (asset == RelevantAsset) return true;
        if (actions != null || playerInput != null) return false;
        if (actionSource != null)
          foreach (var candidate in asset)
            if (!ReferenceEquals(actionSource.ResolveRuntimeAction(candidate), candidate)) return true;
        foreach (var watched in watchedActions.Keys) if (watched.actionMap?.asset == asset) return true;
      }
      return false;
    }

    private void OnInputEvent(InputEventPtr inputEvent, InputDevice device)
    {
      if (disposed || resolving != 0 || device == null || !device.added || !device.enabled
        || (!inputEvent.IsA<StateEvent>() && !inputEvent.IsA<DeltaStateEvent>())) return;
      bool relevant = false;
      resolving++;
      try
      {
        foreach (var control in inputEvent.EnumerateChangedControls(device, magnitudeThreshold: 0.2f))
        {
          if (ControlIsRelevant(control)) { relevant = true; break; }
        }
      }
      finally { resolving--; }
      if (!relevant || activeDevice == device) return;
      activeDevice = device;
      if (playerInput != null) RefreshPlayerContext();
      NotifyChanged();
    }

    private bool ControlIsRelevant(InputControl changedControl)
    {
      bool UsesControl(InputAction action)
      {
        foreach (var bound in action.controls)
          for (var control = changedControl; control != null; control = control.parent)
            if (bound == control) return true;
        return false;
      }
      var asset = RelevantAsset;
      if (asset != null)
        foreach (var action in asset) if (UsesControl(action)) return true;
      foreach (var action in watchedActions.Keys)
        if (IsRelevant(action) && UsesControl(action)) return true;
      return false;
    }

    private void OnActionChange(object value, InputActionChange change)
    {
      if (disposed || resolving != 0 || !IsRelevant(value)) return;
      if (change == InputActionChange.ActionPerformed && value is InputAction action)
      {
        var device = action.activeControl?.device;
        if (device == null || !device.added) return;
        bool deviceChanged = activeDevice != device;
        activeDevice = device;
        if (playerInput != null) RefreshPlayerContext();
        if (deviceChanged) NotifyChanged();
      }
      else if (change == InputActionChange.BoundControlsChanged || change == InputActionChange.ActionEnabled
        || change == InputActionChange.ActionDisabled || change == InputActionChange.ActionMapEnabled
        || change == InputActionChange.ActionMapDisabled)
        NotifyChanged();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
      if (disposed) return;
      if ((change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected
        || change == InputDeviceChange.Disabled) && (device == activeDevice || explicitContext?.Device == device))
      {
        activeDevice = null;
        activeGroup = null;
        RefreshPlayerContext();
        NotifyChanged();
      }
      else if (change == InputDeviceChange.Reconnected || change == InputDeviceChange.Added
        || change == InputDeviceChange.ConfigurationChanged || change == InputDeviceChange.UsageChanged)
      {
        // A new connection does not make that device active. Refresh available glyphs/bindings only.
        RefreshPlayerContext();
        NotifyChanged();
      }
    }

    private void OnUserChange(InputUser user, InputUserChange change, InputDevice device)
    {
      if (disposed || playerInput == null || !playerInput.user.valid || user.id != playerInput.user.id) return;
      RefreshPlayerContext();
      NotifyChanged();
    }

    private void RefreshPlayerContext()
    {
      if (playerInput == null) return;
      activeGroup = null;
      var asset = playerInput.actions;
      if (asset != null && !string.IsNullOrEmpty(playerInput.currentControlScheme))
        foreach (var scheme in asset.controlSchemes)
          if (scheme.name == playerInput.currentControlScheme) { activeGroup = scheme.bindingGroup; break; }
      bool paired = false;
      foreach (var device in playerInput.devices) if (device == activeDevice && device.added) paired = true;
      if (paired) return;
      activeDevice = null;
      foreach (var device in playerInput.devices)
      {
        if (!device.added || !device.enabled) continue;
        activeDevice = device;
        break;
      }
    }

    private void OnLocaleChanged(Locale _) { unityLocalizer.Invalidate(); NotifyChanged(); }

    private void NotifyChanged()
    {
      if (disposed || resolving != 0 || notifying) return;
      if (synchronizationContext != null && Thread.CurrentThread.ManagedThreadId != ownerThread)
      {
        synchronizationContext.Post(_ => NotifyChanged(), null);
        return;
      }
      notifying = true;
      try { changed.OnNext(Unit.Default); }
      finally { notifying = false; }
    }

    private void ThrowIfDisposed()
    { if (disposed) throw new ObjectDisposedException(nameof(InputPromptService)); }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      if (settings != null) settings.Changed -= RefreshConfiguration;
      if (catalog != null) catalog.Changed -= NotifyChanged;
      InputSystem.onActionChange -= OnActionChange;
      InputSystem.onEvent -= OnInputEvent;
      InputSystem.onDeviceChange -= OnDeviceChange;
      InputUser.onChange -= OnUserChange;
      LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
      subscriptions.Dispose();
      unityLocalizer.Dispose();
      watchedActions.Clear();
      changed.OnCompleted();
      changed.Dispose();
    }
  }
}
