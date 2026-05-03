using System;
using System.Collections.Generic;
using Serilog;
using Serilog.Core;
using UnityEngine.InputSystem;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Components
{
  /// <summary>
  ///   Helper class for breaking down composite bindings into individual components
  /// </summary>
  public static class CompositeBindingHelper
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(CompositeBindingHelper)).ForFeature("Components.CompositeBindingHelper"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    /// <summary>
    ///   Break down an action's bindings into grouped components by logical name
    /// </summary>
    /// <param name="action">The input action to analyze</param>
    /// <returns>List of binding components grouped by name</returns>
    public static List<BindingComponent> GetGroupedBindingComponents(InputAction action)
    {
      List<BindingComponent> components = new();

      if (action == null) return components;

      // Group bindings by their logical name (the part before the colon)
      Dictionary<string, List<BindingSlot>> bindingGroups = new();

      for (int i = 0; i < action.bindings.Count; i++)
      {
        InputBinding binding = action.bindings[i];

        // Skip composite bindings themselves, only process their parts
        if (binding.isComposite) continue;

        // Extract the logical name (e.g., "Up" from "Up: W [Keyboard]")
        string logicalName = GetLogicalNameFromBinding(binding, action);
        if (string.IsNullOrEmpty(logicalName)) continue;

        // Determine device type and if it's a gamepad
        string deviceType = GetDeviceTypeFromBinding(binding);
        bool isGamepad = IsGamepadBinding(binding);

        BindingSlot slot = new()
        {
          BindingIndex = i,
          Binding = binding,
          DeviceType = deviceType,
          IsGamepad = isGamepad
        };

        // Debug logging to help troubleshoot control scheme detection
        log.Verbose("Binding {Index}: Name='{Name}', Path='{Path}', Groups='{Groups}', DeviceType='{DeviceType}', IsGamepad={IsGamepad}",
          i, logicalName, binding.path, binding.groups, deviceType, isGamepad);

        if (!bindingGroups.ContainsKey(logicalName))
          bindingGroups[logicalName] = new List<BindingSlot>();

        bindingGroups[logicalName].Add(slot);
      }

      // Convert grouped bindings into components
      foreach (KeyValuePair<string, List<BindingSlot>> group in bindingGroups)
      {
        BindingComponent component = new()
        {
          Name = group.Key,
          DisplayName = GetDisplayNameForComponent(group.Key),
          Action = action,
          Slots = group.Value
        };

        // Sort slots: Primary (keyboard/mouse first), Secondary (other non-gamepad), Gamepad last
        component.Slots.Sort((a, b) =>
        {
          if (a.IsGamepad && !b.IsGamepad) return 1;
          if (!a.IsGamepad && b.IsGamepad) return -1;
          return a.BindingIndex.CompareTo(b.BindingIndex);
        });

        components.Add(component);
      }

      return components;
    }

    /// <summary>
    ///   Extract the logical name from a binding (e.g., "Up" from "Up: W [Keyboard]")
    /// </summary>
    /// <param name="binding">The input binding</param>
    /// <param name="action">The input action this binding belongs to</param>
    /// <returns>The logical name</returns>
    private static string GetLogicalNameFromBinding(InputBinding binding, InputAction action)
    {
      // For composite parts, use the binding name
      if (binding.isPartOfComposite)
        return binding.name;

      // For regular bindings, we need to determine the logical action name
      // This is tricky because we need to map individual bindings back to their logical action

      // Check if this is a stick binding - exclude these from the list
      string path = binding.path?.ToLower() ?? "";
      if (path.Contains("leftstick") || path.Contains("rightstick"))
        // Skip stick bindings - don't include them in the list
        return null;

      // For actions like Jump, Sprint, etc., we need to determine if this is a directional binding
      // or a general action binding
      if (action.name == "Move")
      {
        // For Move action, check if this is a directional binding
        if (path.Contains("up") || path.Contains("w"))
          return "Up";
        if (path.Contains("down") || path.Contains("s"))
          return "Down";
        if (path.Contains("left") || path.Contains("a"))
          return "Left";
        if (path.Contains("right") || path.Contains("d"))
          return "Right";
      }
      else
      {
        // For other actions (Jump, Sprint, etc.), use the action name as the logical name
        // This will group all bindings for the same action together
        return action.name;
      }

      // Fallback: try to extract from the path or use the binding name
      if (string.IsNullOrEmpty(path)) return binding.name;

      // If the path contains a colon, use the part before it
      int colonIndex = path.IndexOf(':');
      if (colonIndex > 0)
        return path.Substring(0, colonIndex).Trim();

      return binding.name;
    }

    /// <summary>
    ///   Determine the device type from a binding using control schemes
    /// </summary>
    /// <param name="binding">The input binding</param>
    /// <returns>Device type string</returns>
    private static string GetDeviceTypeFromBinding(InputBinding binding)
    {
      // First check control schemes - this is the most reliable method
      if (!string.IsNullOrEmpty(binding.groups))
      {
        string[] groups = binding.groups.Split(';');
        foreach (string group in groups)
        {
          string trimmedGroup = group.Trim();
          if (trimmedGroup.Contains("Gamepad") || trimmedGroup.Contains("Xbox") || trimmedGroup.Contains("PS4") || trimmedGroup.Contains("PS5"))
            return "Gamepad";
          if (trimmedGroup.Contains("Keyboard") || trimmedGroup.Contains("Mouse"))
            return "Keyboard/Mouse";
        }
      }

      // Fallback to path parsing if no control scheme info
      string path = binding.path;
      if (string.IsNullOrEmpty(path)) return "Unknown";

      // Check for common device types in the path
      if (path.Contains("[Keyboard]")) return "Keyboard";
      if (path.Contains("[Mouse]")) return "Mouse";
      if (path.Contains("[Gamepad]")) return "Gamepad";
      if (path.Contains("[Xbox")) return "Xbox Controller";
      if (path.Contains("[PS4")) return "PS4 Controller";
      if (path.Contains("[PS5")) return "PS5 Controller";

      // Fallback: try to determine from path patterns
      if (path.Contains("/keyboard/") || path.Contains("/mouse/")) return "Keyboard/Mouse";
      if (path.Contains("/gamepad/") || path.Contains("/xbox/") || path.Contains("/ps4/") || path.Contains("/ps5/")) return "Gamepad";

      return "Unknown";
    }

    /// <summary>
    ///   Check if a binding is for a gamepad device using control schemes
    /// </summary>
    /// <param name="binding">The input binding</param>
    /// <returns>True if it's a gamepad binding</returns>
    private static bool IsGamepadBinding(InputBinding binding)
    {
      // First check control schemes - this is the most reliable method
      if (!string.IsNullOrEmpty(binding.groups))
      {
        string[] groups = binding.groups.Split(';');
        foreach (string group in groups)
        {
          string trimmedGroup = group.Trim();
          if (trimmedGroup.Contains("Gamepad") || trimmedGroup.Contains("Xbox") || trimmedGroup.Contains("PS4") || trimmedGroup.Contains("PS5"))
            return true;
        }
      }

      // Fallback to device type check
      string deviceType = GetDeviceTypeFromBinding(binding);
      return deviceType.Contains("Gamepad") || deviceType.Contains("Controller") || deviceType.Contains("Xbox") || deviceType.Contains("PS4") || deviceType.Contains("PS5");
    }

    /// <summary>
    ///   Get a user-friendly display name for a binding component
    /// </summary>
    /// <param name="componentName">The internal component name</param>
    /// <returns>Display name</returns>
    private static string GetDisplayNameForComponent(string componentName)
    {
      return componentName?.ToLower() switch
      {
        "up" => "Up",
        "down" => "Down", 
        "left" => "Left",
        "right" => "Right",
        "positive" => "Positive",
        "negative" => "Negative",
        "button" => "Button",
        "trigger" => "Trigger",
        "grip" => "Grip",
        _ => componentName ?? "Unknown"
        };
    }

    /// <summary>
    ///   Check if an action has bindings that should be grouped by logical name
    /// </summary>
    /// <param name="action">The input action to check</param>
    /// <returns>True if the action has bindings that can be grouped</returns>
    public static bool HasGroupableBindings(InputAction action)
    {
      if (action == null) return false;

      // Check if there are any non-composite bindings that can be grouped
      foreach (InputBinding binding in action.bindings)
        if (!binding.isComposite && !string.IsNullOrEmpty(GetLogicalNameFromBinding(binding, action)))
          return true;

      return false;
    }

    /// <summary>
    ///   Get control scheme names from an InputActionAsset for debugging
    /// </summary>
    /// <param name="actionAsset">The input action asset</param>
    /// <returns>List of control scheme names</returns>
    public static List<string> GetControlSchemeNames(InputActionAsset actionAsset)
    {
      List<string> schemes = new();

      if (actionAsset == null) return schemes;

      foreach (InputControlScheme controlScheme in actionAsset.controlSchemes)
        schemes.Add(controlScheme.name);

      return schemes;
    }

    #region Nested type: BindingComponent

    /// <summary>
    ///   Represents a single component of a composite binding
    /// </summary>
    public class BindingComponent
    {
      public string Name { get; set; }
      public string DisplayName { get; set; }
      public InputAction Action { get; set; }
      public List<BindingSlot> Slots { get; set; } = new();
    }

    #endregion

    #region Nested type: BindingSlot

    /// <summary>
    ///   Represents a single binding slot (Primary/Secondary/Gamepad)
    /// </summary>
    public class BindingSlot
    {
      public int BindingIndex { get; set; }
      public InputBinding Binding { get; set; }
      public string DeviceType { get; set; }
      public bool IsGamepad { get; set; }
    }

    #endregion
  }
}