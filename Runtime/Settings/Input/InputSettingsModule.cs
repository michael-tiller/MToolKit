using System;
using System.Collections.Generic;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using UnityEngine.InputSystem;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.Input
{
  public class InputSettingsModule : ISettingsModule, IInputSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputSettingsModule>().ForFeature("Settings.Input"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly InputRebinderService rebinderService;
    private readonly Dictionary<string, InputAction> registeredActions = new();
    private readonly IDisposable panValidationSubscription;
    private readonly IDisposable zoomValidationSubscription;
    private readonly IDisposable bindingSubscription;
    private InputActionAsset inputActionAsset;
    private bool restoringBindings;

    public InputSettingsModule(InputRebinderService rebinderService, ISettingsSystem settingsController = null,
      InputActionAsset inputActionAsset = null)
    {
      this.rebinderService = rebinderService ?? throw new ArgumentNullException(nameof(rebinderService));
      CameraPanSensitivity = new ReactiveSetting<float>(1f, "Camera Pan Sensitivity", settingsController);
      CameraZoomSensitivity = new ReactiveSetting<float>(1f, "Camera Zoom Sensitivity", settingsController);
      BindingOverridesJson = new ReactiveSetting<string>(string.Empty, "Binding Overrides", settingsController);
      panValidationSubscription = CameraPanSensitivity.Property.Subscribe(value =>
        NormalizeCameraSensitivity(CameraPanSensitivity, value));
      zoomValidationSubscription = CameraZoomSensitivity.Property.Subscribe(value =>
        NormalizeCameraSensitivity(CameraZoomSensitivity, value));
      bindingSubscription = rebinderService.OnBindingChanged.Subscribe(_ =>
      {
        if (!restoringBindings) BindingOverridesJson.Value = CaptureBindings();
      });
      if (inputActionAsset != null) RegisterAsset(inputActionAsset);
    }

    /// <summary>Multiplier for camera panning; 1 preserves the authored speed.</summary>
    public ReactiveSetting<float> CameraPanSensitivity { get; }

    /// <summary>Multiplier for camera zoom input; 1 preserves the authored speed.</summary>
    public ReactiveSetting<float> CameraZoomSensitivity { get; }

    /// <summary>Current binding draft and its last applied cancellation baseline.</summary>
    public ReactiveSetting<string> BindingOverridesJson { get; }

    public InputRebinderService RebinderService => rebinderService;

    /// <summary>
    ///   Get all registered input actions for the settings UI
    /// </summary>
    public IReadOnlyCollection<InputAction> RegisteredActions => rebinderService.RegisteredActions;

    /// <summary>
    ///   Check if a rebinding operation is currently in progress
    /// </summary>
    public bool IsRebinding => rebinderService.IsRebinding;

    #region ISettingsModule Members

    public void RevertToDefaultSettings()
    {
      rebinderService.CancelRebinding();
      if (!CameraPanSensitivity.IsDefault) CameraPanSensitivity.OnRevertToDefault();
      if (!CameraZoomSensitivity.IsDefault) CameraZoomSensitivity.OnRevertToDefault();
      RestoreBindings(string.Empty);
      BindingOverridesJson.OnRevertToDefault();
      log.ForMethod().Verbose("Reverted all input settings to defaults");
    }

    public void Apply()
    {
      rebinderService.CancelRebinding();
      if (CameraPanSensitivity.IsDirty) CameraPanSensitivity.OnApply();
      if (CameraZoomSensitivity.IsDirty) CameraZoomSensitivity.OnApply();
      BindingOverridesJson.Value = CaptureBindings();
      BindingOverridesJson.OnApply();
      log.ForMethod().Verbose("Input settings applied");
    }

    public void Cancel()
    {
      rebinderService.CancelRebinding();
      CameraPanSensitivity.OnCancel();
      CameraZoomSensitivity.OnCancel();
      RestoreBindings(BindingOverridesJson.LastValue);
      BindingOverridesJson.OnCancel();
      log.ForMethod().Verbose("Input settings cancelled");
    }

    public void OnShutdown()
    {
      rebinderService.CancelRebinding();
      bindingSubscription.Dispose();
      panValidationSubscription.Dispose();
      zoomValidationSubscription.Dispose();
      CameraPanSensitivity.Dispose();
      CameraZoomSensitivity.Dispose();
      BindingOverridesJson.Dispose();
      registeredActions.Clear();
      log.ForMethod().Verbose("Input settings module shutdown");
    }

    #endregion

    private static void NormalizeCameraSensitivity(ReactiveSetting<float> setting, float value)
    {
      float normalized = float.IsNaN(value) || float.IsInfinity(value)
        ? setting.Default
        : Math.Clamp(value, 0.1f, 3f);
      if (value != normalized) setting.Value = normalized;
    }

    /// <summary>
    ///   Register an input action for settings management
    /// </summary>
    /// <param name="action">The input action to register</param>
    public void RegisterAction(InputAction action)
    {
      if (action == null) return;

      if (inputActionAsset == null && action.actionMap?.asset != null)
        RegisterAsset(action.actionMap.asset);

      rebinderService.RegisterAction(action);
      registeredActions[GetActionKey(action)] = action;

      log.ForMethod().Information("Registered action {ActionName} for input settings", action.name);
    }

    /// <summary>
    ///   Start interactive rebinding for an action
    /// </summary>
    /// <param name="action">The action to rebind</param>
    /// <param name="bindingIndex">The binding index</param>
    /// <param name="excludeControls">Controls to exclude from rebinding</param>
    public bool StartInteractiveRebinding(InputAction action, int bindingIndex, string[] excludeControls = null)
    {
      return rebinderService.StartInteractiveRebinding(action, bindingIndex, excludeControls);
    }

    /// <summary>
    ///   Cancel the current rebinding operation
    /// </summary>
    public void CancelRebinding()
    {
      rebinderService.CancelRebinding();
    }

    /// <summary>
    ///   Remove a binding override for an action (reset to default)
    /// </summary>
    /// <param name="action">The action to modify</param>
    /// <param name="bindingIndex">The binding index</param>
    public void RemoveBindingOverride(InputAction action, int bindingIndex)
    {
      rebinderService.RemoveBindingOverride(action, bindingIndex);
    }

    private string GetActionKey(InputAction action)
    {
      return $"{action.actionMap?.name}_{action.name}";
    }

    /// <summary>Loads persisted overrides before advancing the startup applied baseline.</summary>
    public void LoadBindingOverridesJson(string json)
    {
      RestoreBindings(json ?? string.Empty);
      BindingOverridesJson.Value = CaptureBindings();
    }

    private void RegisterAsset(InputActionAsset asset)
    {
      inputActionAsset = asset;
      foreach (InputActionMap map in asset.actionMaps)
        foreach (InputAction action in map.actions)
        {
          rebinderService.RegisterAction(action);
          registeredActions[GetActionKey(action)] = action;
        }
      string baseline = CaptureBindings();
      BindingOverridesJson.LastProperty.Value = baseline;
      BindingOverridesJson.Value = baseline;
    }

    private string CaptureBindings() => inputActionAsset != null
      ? inputActionAsset.SaveBindingOverridesAsJson()
      : string.Empty;

    private void RestoreBindings(string json)
    {
      if (inputActionAsset == null)
      {
        if (!string.IsNullOrEmpty(json))
          throw new InvalidOperationException("An InputActionAsset is required to load saved binding overrides.");
        return;
      }

      string previousJson = CaptureBindings();
      var previousPaths = new Dictionary<InputAction, string[]>();
      foreach (InputActionMap map in inputActionAsset.actionMaps)
        foreach (InputAction action in map.actions)
        {
          var paths = new string[action.bindings.Count];
          for (int i = 0; i < paths.Length; i++) paths[i] = action.bindings[i].effectivePath;
          previousPaths[action] = paths;
        }

      restoringBindings = true;
      try
      {
        try { inputActionAsset.LoadBindingOverridesFromJson(json); }
        catch
        {
          // Unity removes existing overrides before parsing; restore them if parsing fails.
          inputActionAsset.LoadBindingOverridesFromJson(previousJson);
          throw;
        }

        foreach (var entry in previousPaths)
          for (int i = 0; i < entry.Value.Length; i++)
          {
            string path = entry.Key.bindings[i].effectivePath;
            if (path != entry.Value[i]) rebinderService.OnBindingChanged.OnNext((entry.Key, i, path));
          }
      }
      finally { restoringBindings = false; }
    }
  }
}
