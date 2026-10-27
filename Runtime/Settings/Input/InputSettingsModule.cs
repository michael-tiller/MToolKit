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

    public InputSettingsModule(InputRebinderService rebinderService, ISettingsSystem settingsController = null)
    {
      this.rebinderService = rebinderService ?? throw new ArgumentNullException(nameof(rebinderService));
      CameraPanSensitivity = new ReactiveSetting<float>(1f, "Camera Pan Sensitivity", settingsController);
      CameraZoomSensitivity = new ReactiveSetting<float>(1f, "Camera Zoom Sensitivity", settingsController);
      panValidationSubscription = CameraPanSensitivity.Property.Subscribe(value =>
        NormalizeCameraSensitivity(CameraPanSensitivity, value));
      zoomValidationSubscription = CameraZoomSensitivity.Property.Subscribe(value =>
        NormalizeCameraSensitivity(CameraZoomSensitivity, value));
    }

    /// <summary>Multiplier for camera panning; 1 preserves the authored speed.</summary>
    public ReactiveSetting<float> CameraPanSensitivity { get; }

    /// <summary>Multiplier for camera zoom input; 1 preserves the authored speed.</summary>
    public ReactiveSetting<float> CameraZoomSensitivity { get; }

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
      if (!CameraPanSensitivity.IsDefault) CameraPanSensitivity.OnRevertToDefault();
      if (!CameraZoomSensitivity.IsDefault) CameraZoomSensitivity.OnRevertToDefault();
      rebinderService.ResetAllBindings();
      log.ForMethod().Verbose("Reverted all input settings to defaults");
    }

    public void Apply()
    {
      if (CameraPanSensitivity.IsDirty) CameraPanSensitivity.OnApply();
      if (CameraZoomSensitivity.IsDirty) CameraZoomSensitivity.OnApply();
      // Binding overrides retain their existing immediate application behavior.
      log.ForMethod().Verbose("Input settings applied");
    }

    public void Cancel()
    {
      CameraPanSensitivity.OnCancel();
      CameraZoomSensitivity.OnCancel();
      // Preserve the existing binding Cancel behavior while reverting sensitivity drafts.
      rebinderService.ResetAllBindings();
      log.ForMethod().Verbose("Input settings cancelled");
    }

    public void OnShutdown()
    {
      panValidationSubscription.Dispose();
      zoomValidationSubscription.Dispose();
      CameraPanSensitivity.Dispose();
      CameraZoomSensitivity.Dispose();
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
  }
}
