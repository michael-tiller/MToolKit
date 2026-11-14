using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Prompts
{
  /// <summary>Inspects effective bindings without changing action enablement or input routing.</summary>
  public sealed class InputBindingResolver
  {
    public ResolvedInputBinding Resolve(InputAction action, InputPromptContext context = default)
      => Resolve(action, context, desktopOnly: false);

    internal ResolvedInputBinding ResolveKeyboardAndMouse(InputAction action, InputPromptContext context)
    {
      // An unpaired/removed caller device is not permission to inspect another player's bindings.
      if (action == null || !ContextDeviceIsAllowed(action, context.Device))
        return new ResolvedInputBinding { Action = action };
      var desktop = new InputPromptContext(null, null, context.FamilyOverride, context.BindingIndex,
        context.FontAsset, context.PlatformOverride, context.UnboundBehavior);
      return Resolve(action, desktop, desktopOnly: true);
    }

    private static ResolvedInputBinding Resolve(InputAction action, InputPromptContext context, bool desktopOnly)
    {
      var empty = new ResolvedInputBinding { Action = action };
      if (action == null) return empty;

      // This establishes Input System's lazy binding state and change callbacks, including
      // for disabled actions. It does not enable an action or choose a global active device.
      _ = action.controls.Count;
      if (!ContextDeviceIsAllowed(action, context.Device)) return empty;
      string group = ResolveBindingGroup(action, context.BindingGroup);
      ResolvedInputBinding selected = null;
      int selectedScore = int.MinValue;
      for (int index = 0; index < action.bindings.Count; index++)
      {
        var binding = action.bindings[index];
        if (context.BindingIndex.HasValue && context.BindingIndex.Value != index) continue;
        if (binding.isPartOfComposite && !context.BindingIndex.HasValue) continue;
        ResolvedInputBinding candidate;
        if (binding.isComposite)
          candidate = ResolveComposite(action, index, context, group);
        else if (Eligible(action, binding, context, group))
          candidate = new ResolvedInputBinding
          {
            Action = action, BindingIndex = index,
            Controls = new[] { Control(binding.effectivePath, FindDevice(action, binding.effectivePath, context.Device)) }
          };
        else continue;
        if (!candidate.IsBound) continue;
        if (desktopOnly && !AllControlsAreKeyboardOrMouse(candidate.Controls)) continue;
        int score = 0;
        if (context.Device == null && string.IsNullOrEmpty(group))
          score = IsKeyboardOrMouse(candidate.Controls[0].DeviceLayout) ? 1 : 0;
        if (selected == null || score > selectedScore)
        {
          selected = candidate;
          selectedScore = score;
        }
      }
      return selected ?? empty;
    }

    private static bool AllControlsAreKeyboardOrMouse(IReadOnlyList<InputPromptControl> controls)
    {
      foreach (var control in controls)
        if (!IsKeyboardOrMouse(control.DeviceLayout)) return false;
      return true;
    }

    private static ResolvedInputBinding ResolveComposite(InputAction action, int index,
      InputPromptContext context, string group)
    {
      var root = action.bindings[index];
      var result = new ResolvedInputBinding { Action = action, BindingIndex = index, CompositeName = root.effectivePath };
      if (string.IsNullOrEmpty(root.effectivePath)) return result;
      var controls = new List<InputPromptControl>();
      var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      for (int part = index + 1; part < action.bindings.Count && action.bindings[part].isPartOfComposite; part++)
      {
        var binding = action.bindings[part];
        var grouped = binding;
        if (string.IsNullOrEmpty(grouped.groups)) grouped.groups = root.groups;
        if (!GroupMatches(grouped.groups, group) || !Eligible(action, binding, context, null)) continue;
        if (!roles.Add(binding.name ?? string.Empty)) continue;
        controls.Add(Control(binding.effectivePath, FindDevice(action, binding.effectivePath, context.Device), binding.name));
      }
      string composite = CompositeType(root.effectivePath);
      bool oneModifier = composite.Equals("OneModifier", StringComparison.OrdinalIgnoreCase)
        || composite.Equals("ButtonWithOneModifier", StringComparison.OrdinalIgnoreCase);
      bool twoModifiers = composite.Equals("TwoModifiers", StringComparison.OrdinalIgnoreCase)
        || composite.Equals("ButtonWithTwoModifiers", StringComparison.OrdinalIgnoreCase);
      if (oneModifier && (!roles.Contains("modifier") || !roles.Contains("binding"))) return result;
      if (twoModifiers && (!roles.Contains("modifier1") || !roles.Contains("modifier2") || !roles.Contains("binding"))) return result;
      if (oneModifier || twoModifiers)
        controls.Sort((left, right) => PartOrder(left.PartName).CompareTo(PartOrder(right.PartName)));
      result.Controls = controls;
      return result;
    }

    internal static string CompositeType(string value)
    {
      if (string.IsNullOrEmpty(value)) return string.Empty;
      int parameters = value.IndexOf('(');
      return parameters < 0 ? value : value.Substring(0, parameters);
    }

    private static int PartOrder(string name) => string.Equals(name, "binding", StringComparison.OrdinalIgnoreCase)
      ? 2 : string.Equals(name, "modifier2", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    private static bool Eligible(InputAction action, InputBinding binding, InputPromptContext context, string group)
    {
      if (string.IsNullOrEmpty(binding.effectivePath) || !GroupMatches(binding.groups, group)) return false;
      if (!MaskMatches(action.bindingMask, binding) || !MaskMatches(action.actionMap?.bindingMask, binding)
        || !MaskMatches(action.actionMap?.asset?.bindingMask, binding)) return false;
      if (context.Device != null && !PathMatchesDevice(binding.effectivePath, context.Device)) return false;
      var devices = action.actionMap?.devices;
      if (devices.HasValue)
      {
        foreach (var device in devices.Value)
          if (device != null && device.added && InputControlPath.TryFindControl(device, binding.effectivePath) != null) return true;
        return false;
      }
      return true;
    }

    private static bool ContextDeviceIsAllowed(InputAction action, InputDevice device)
    {
      if (device == null) return true;
      if (!device.added) return false;
      var devices = action.actionMap?.devices;
      if (!devices.HasValue) return true;
      foreach (var allowed in devices.Value) if (allowed == device) return true;
      return false;
    }

    private static bool PathMatchesDevice(string path, InputDevice device)
    {
      if (InputControlPath.TryFindControl(device, path) != null) return true;
      // A keyboard/mouse scheme can contain chords involving both paired device types.
      // A different keyboard (or mouse) layout is not that companion device.
      string layout = InputControlPath.TryGetDeviceLayout(path);
      return InputGlyphCatalog.IsLayout(device.layout, "Keyboard") && InputGlyphCatalog.IsLayout(layout, "Mouse")
        || InputGlyphCatalog.IsLayout(device.layout, "Mouse") && InputGlyphCatalog.IsLayout(layout, "Keyboard");
    }

    private static InputDevice FindDevice(InputAction action, string path, InputDevice preferred)
    {
      if (preferred != null && InputControlPath.TryFindControl(preferred, path) != null) return preferred;
      var devices = action.actionMap?.devices;
      if (devices.HasValue)
        foreach (var device in devices.Value)
          if (device != null && device.added && InputControlPath.TryFindControl(device, path) != null) return device;
      // Resolve presentation from this action's actual controls, including native keyboard
      // layout names and concrete gamepad layouts, without inferring an active global pad.
      foreach (var control in action.controls)
        if (control.device.added && InputControlPath.Matches(path, control)) return control.device;
      return null;
    }

    private static bool MaskMatches(InputBinding? mask, InputBinding binding)
    {
      if (!mask.HasValue) return true;
      var value = mask.Value;
      // The native resolver uses EmptyGroupMatchesAny; the public Matches overload
      // has no such flag. Preserve that native behavior without reflection.
      if (string.IsNullOrEmpty(binding.groups)) value.groups = null;
      return value.Matches(binding);
    }

    internal static bool GroupMatches(string groups, string requested)
    {
      if (string.IsNullOrEmpty(requested) || string.IsNullOrEmpty(groups)) return true;
      foreach (string item in groups.Split(';'))
        foreach (string candidate in requested.Split(';'))
          if (string.Equals(item.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
      return false;
    }

    private static string ResolveBindingGroup(InputAction action, string requested)
    {
      var asset = action.actionMap?.asset;
      if (asset != null && !string.IsNullOrEmpty(requested))
        foreach (var scheme in asset.controlSchemes)
          if (string.Equals(scheme.name, requested, StringComparison.OrdinalIgnoreCase)) return scheme.bindingGroup;
      return requested;
    }

    internal static bool IsKeyboardOrMouse(string layout) => InputGlyphCatalog.IsLayout(layout, "Keyboard")
      || InputGlyphCatalog.IsLayout(layout, "Mouse");

    internal static InputPromptControl Control(string path, InputDevice device = null, string partName = null)
    {
      string layout = InputControlPath.TryGetDeviceLayout(path) ?? string.Empty;
      int slash = path.IndexOf('/');
      string controlPath = slash < 0 ? path : path.Substring(slash + 1);
      var control = device == null ? null : InputControlPath.TryFindControl(device, path);
      if (control != null)
      {
        layout = device.layout;
        controlPath = control.path.Substring(device.path.Length).TrimStart('/');
      }
      else if (InputGlyphCatalog.IsLayout(layout, "Keyboard"))
      {
        // Binding paths ignore case, while semantic localization keys do not.
        // Native layout metadata supplies the same canonical name without a device.
        InputControlPath.ToHumanReadableString(path, out _, out string canonicalPath,
          InputControlPath.HumanReadableStringOptions.OmitDevice);
        if (!string.IsNullOrEmpty(canonicalPath)) controlPath = canonicalPath;
      }
      string displayName = control?.displayName;
      string character = null;
      if (InputGlyphCatalog.IsLayout(layout, "Keyboard") && IsMainPrintableKey(controlPath))
      {
        if (!string.IsNullOrWhiteSpace(displayName) && StringInfo.ParseCombiningCharacters(displayName).Length == 1)
          character = displayName;
        else character = PrintableCharacter(controlPath);
      }
      return new InputPromptControl(layout, controlPath, displayName, character, partName);
    }

    internal static bool IsMainPrintableKey(string path) => !string.IsNullOrEmpty(PrintableCharacter(path));

    private static string PrintableCharacter(string path)
    {
      if (path.Length == 1 && char.IsLetterOrDigit(path[0])) return path.ToUpperInvariant();
      return path.ToLowerInvariant() switch
      {
        "semicolon" => ";", "quote" => "'", "comma" => ",", "period" => ".", "slash" => "/",
        "backslash" => "\\", "leftbracket" => "[", "rightbracket" => "]", "minus" => "-",
        "equals" => "=", "backquote" => "`", _ => null
      };
    }

    internal static string Humanize(string value)
    {
      if (string.IsNullOrEmpty(value)) return string.Empty;
      value = Regex.Replace(value.Replace('/', ' ').Replace('_', ' '), "([a-z0-9])([A-Z])", "$1 $2");
      return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
  }
}
