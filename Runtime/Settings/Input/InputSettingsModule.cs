using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Input;

namespace MToolKit.Runtime.Settings.Input
{
  public class InputSettingsModule : ISettingsModule, IInputSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InputSettingsModule>().ForFeature("Settings.Input"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private readonly InputRebinderService rebinderService;
    private readonly Dictionary<string, InputAction> registeredActions = new();

    public InputSettingsModule(InputRebinderService rebinderService, ISettingsSystem settingsController = null)
    {
      this.rebinderService = rebinderService ?? throw new ArgumentNullException(nameof(rebinderService));
    }

    /// <summary>
    /// Get all registered input actions for the settings UI
    /// </summary>
    public IReadOnlyCollection<InputAction> RegisteredActions => rebinderService.RegisteredActions;

    /// <summary>
    /// Register an input action for settings management
    /// </summary>
    /// <param name="action">The input action to register</param>
    public void RegisterAction(InputAction action)
    {
      if (action == null) return;
      
      rebinderService.RegisterAction(action);
      registeredActions[GetActionKey(action)] = action;
      
      log.ForMethod(nameof(RegisterAction)).Information("Registered action {ActionName} for input settings", action.name);
    }

    /// <summary>
    /// Start interactive rebinding for an action
    /// </summary>
    /// <param name="action">The action to rebind</param>
    /// <param name="bindingIndex">The binding index</param>
    /// <param name="excludeControls">Controls to exclude from rebinding</param>
    public bool StartInteractiveRebinding(InputAction action, int bindingIndex, string[] excludeControls = null)
    {
      return rebinderService.StartInteractiveRebinding(action, bindingIndex, excludeControls);
    }

    /// <summary>
    /// Cancel the current rebinding operation
    /// </summary>
    public void CancelRebinding()
    {
      rebinderService.CancelRebinding();
    }

    /// <summary>
    /// Check if a rebinding operation is currently in progress
    /// </summary>
    public bool IsRebinding => rebinderService.IsRebinding;

    /// <summary>
    /// Remove a binding override for an action (reset to default)
    /// </summary>
    /// <param name="action">The action to modify</param>
    /// <param name="bindingIndex">The binding index</param>
    public void RemoveBindingOverride(InputAction action, int bindingIndex)
    {
      rebinderService.RemoveBindingOverride(action, bindingIndex);
    }

    public void RevertToDefaultSettings()
    {
      rebinderService.ResetAllBindings();
      log.ForMethod(nameof(RevertToDefaultSettings)).Information("Reverted all input settings to defaults");
    }

    public void Apply()
    {
      // Input settings are applied immediately via override paths
      // No additional apply logic needed
      log.ForMethod(nameof(Apply)).Information("Input settings applied");
    }

    public void Cancel()
    {
      // Revert any pending changes
      RevertToDefaultSettings();
      log.ForMethod(nameof(Cancel)).Information("Input settings cancelled");
    }

    public void OnShutdown()
    {
      registeredActions.Clear();
      log.ForMethod(nameof(OnShutdown)).Information("Input settings module shutdown");
    }

    private string GetActionKey(InputAction action)
    {
      return $"{action.actionMap?.name}_{action.name}";
    }
  }
}