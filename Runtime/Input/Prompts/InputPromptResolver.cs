using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Prompts
{
  public sealed class InputPromptResolver
  {
    private readonly InputGlyphCatalog catalog;
    private readonly IInputPromptLocalizer localizer;
    private readonly InputBindingResolver bindings;
    internal Func<TMP_SpriteAsset, bool> SpriteAvailable { get; set; }

    public InputPromptResolver(InputGlyphCatalog catalog = null, IInputPromptLocalizer localizer = null,
      InputBindingResolver bindings = null)
    {
      this.catalog = catalog;
      this.localizer = localizer;
      this.bindings = bindings ?? new InputBindingResolver();
    }

    public IReadOnlyList<TMP_SpriteAsset> SpriteAssets
    {
      get
      {
        var result = new List<TMP_SpriteAsset>();
        if (catalog != null)
          foreach (var entry in catalog.Entries)
            if (entry?.SpriteAsset != null && !result.Contains(entry.SpriteAsset)) result.Add(entry.SpriteAsset);
        return result;
      }
    }

    public string Resolve(InputAction action, InputPromptStyle style = InputPromptStyle.Auto,
      InputPromptContext context = default)
    {
      var binding = bindings.Resolve(action, context);
      if (!binding.IsBound) return Name("input.unbound", "Unbound");
      var parts = new List<string>();
      bool chord = IsChord(binding.CompositeName);
      foreach (var control in binding.Controls)
      {
        string presentation = Render(control, style, context);
        if (!string.IsNullOrEmpty(binding.CompositeName) && !chord)
          presentation = Name("input.composite." + (control.PartName ?? string.Empty).ToLowerInvariant(),
            InputBindingResolver.Humanize(control.PartName)) + ": " + presentation;
        parts.Add(presentation);
      }
      return string.Join(chord ? " + " : " / ", parts);
    }

    public string ResolvePath(string path, InputPromptStyle style = InputPromptStyle.Auto,
      InputPromptContext context = default)
    {
      if (string.IsNullOrEmpty(path)) return Name("input.unbound", "Unbound");
      return Render(InputBindingResolver.Control(path, context.Device), style, context);
    }

    private string Render(InputPromptControl control, InputPromptStyle style, InputPromptContext context)
    {
      control = PrintedKeyboardIdentity(control);
      var actualFamily = catalog != null ? catalog.ResolveFamily(control.DeviceLayout)
        : InputGlyphCatalog.FamilyForLayout(control.DeviceLayout);
      // A preview family changes gamepad artwork, never a keyboard binding or gameplay semantics.
      var family = actualFamily == InputGlyphFamily.KeyboardAndMouse ? actualFamily
        : context.FamilyOverride ?? actualFamily;
      InputGlyphCatalog.Entry entry = null;
      if (catalog != null) catalog.TryGetEntry(family, control.ControlPath, out entry);
      string fallback = !string.IsNullOrEmpty(entry?.DisplayName) ? entry.DisplayName : DefaultName(control, family);
      string key = !string.IsNullOrEmpty(entry?.LocalizationKey) ? entry.LocalizationKey : LocalizationKey(control, family);
      string name = Name(key, fallback);
      if (style == InputPromptStyle.LocalizedName) return name;
      if (style == InputPromptStyle.Character)
      {
        if (InputGlyphCatalog.IsLayout(control.DeviceLayout, "Mouse"))
        {
          string shortName = InputControlPath.ToHumanReadableString("<" + control.DeviceLayout + ">/" + control.ControlPath,
            InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
          if (!string.IsNullOrEmpty(shortName)) return InputPromptRichText.Escape(shortName);
        }
        return !string.IsNullOrEmpty(control.Character) ? InputPromptRichText.Escape(control.Character) : name;
      }
      string glyph = Glyph(entry, context);
      if (style == InputPromptStyle.GlyphAndName) return string.IsNullOrEmpty(glyph) ? name : glyph + " " + name;
      if (style == InputPromptStyle.Glyph) return string.IsNullOrEmpty(glyph) ? name : glyph;
      if (family == InputGlyphFamily.KeyboardAndMouse && !string.IsNullOrEmpty(control.Character))
        return InputPromptRichText.Escape(control.Character);
      return string.IsNullOrEmpty(glyph) ? name : glyph;
    }

    private static InputPromptControl PrintedKeyboardIdentity(InputPromptControl control)
    {
      if (!InputGlyphCatalog.IsLayout(control.DeviceLayout, "Keyboard") || string.IsNullOrEmpty(control.Character))
        return control;
      // Input System key paths are physical locations. Artwork and key names must follow
      // the native printed character (for example Q on the physical A key on AZERTY).
      // Only this presentation value changes; the resolved binding retains its original path.
      string key = control.Character switch
      {
        ";" => "semicolon", "'" => "quote", "," => "comma", "." => "period", "/" => "slash",
        "\\" => "backslash", "[" => "leftBracket", "]" => "rightBracket", "-" => "minus",
        "=" => "equals", "`" => "backquote", _ => control.Character.ToLowerInvariant()
      };
      return new InputPromptControl(control.DeviceLayout, key, control.Character, control.Character, control.PartName);
    }

    private string Glyph(InputGlyphCatalog.Entry entry, InputPromptContext context)
    {
      if (entry == null) return null;
      var asset = entry.SpriteAsset;
      if (asset != null && SafeTagName(asset.name) && SafeTagName(entry.SpriteName)
        && asset.material != null && asset.spriteSheet != null && asset.GetSpriteIndexFromName(entry.SpriteName) >= 0
        && (SpriteAvailable == null || SpriteAvailable(asset)))
        return "<sprite=\"" + asset.name + "\" name=\"" + entry.SpriteName + "\">";
      if (string.IsNullOrEmpty(entry.Symbol)) return null;
      if (context.FontAsset != null
        && !context.FontAsset.HasCharacters(entry.Symbol, out uint[] _, searchFallbacks: true, tryAddCharacter: false)) return null;
      return InputPromptRichText.Escape(entry.Symbol);
    }

    private static bool SafeTagName(string value)
    {
      if (string.IsNullOrEmpty(value)) return false;
      foreach (char character in value)
        if (char.IsControl(character) || character == '"' || character == '\'' || character == '<' || character == '>'
          || character == '\\') return false;
      return true;
    }

    private string Name(string key, string fallback)
    {
      string value = localizer?.GetName(key, fallback);
      return InputPromptRichText.Escape(string.IsNullOrEmpty(value) ? fallback : value);
    }

    private static string LocalizationKey(InputPromptControl control, InputGlyphFamily family)
    {
      if (InputGlyphCatalog.IsLayout(control.DeviceLayout, "Keyboard")) return "input.key." + control.ControlPath;
      if (InputGlyphCatalog.IsLayout(control.DeviceLayout, "Mouse")) return "input.mouse." + control.ControlPath;
      string prefix = family switch
      {
        InputGlyphFamily.Xbox => "xbox", InputGlyphFamily.PlayStation => "playstation",
        InputGlyphFamily.Nintendo => "nintendo", _ => "generic"
      };
      return "input.gamepad." + prefix + "." + control.ControlPath;
    }

    private static bool IsChord(string composite)
    {
      string type = InputBindingResolver.CompositeType(composite);
      return type.Equals("OneModifier", StringComparison.OrdinalIgnoreCase)
        || type.Equals("TwoModifiers", StringComparison.OrdinalIgnoreCase)
        || type.Equals("ButtonWithOneModifier", StringComparison.OrdinalIgnoreCase)
        || type.Equals("ButtonWithTwoModifiers", StringComparison.OrdinalIgnoreCase);
    }

    private static string DefaultName(InputPromptControl control, InputGlyphFamily family)
    {
      string path = control.ControlPath;
      if (InputGlyphCatalog.IsLayout(control.DeviceLayout, "Keyboard"))
        return path.ToLowerInvariant() switch
        {
          "enter" => "Enter", "numpadenter" => "Numpad Enter", "space" => "Space", "escape" => "Escape",
          "tab" => "Tab", "leftctrl" => "Ctrl", "rightctrl" => "Ctrl", "leftshift" => "Shift", "rightshift" => "Shift",
          "leftalt" => "Alt", "rightalt" => "Alt", "backspace" => "Backspace",
          _ => control.Character ?? control.DisplayName ?? InputBindingResolver.Humanize(path)
        };
      if (InputGlyphCatalog.IsLayout(control.DeviceLayout, "Mouse"))
        return path.ToLowerInvariant() switch
        {
          "leftbutton" => "Left Click", "rightbutton" => "Right Click", "middlebutton" => "Middle Click",
          "forwardbutton" => "Mouse Forward", "backbutton" => "Mouse Back", "scroll" => "Scroll",
          _ => control.DisplayName ?? InputBindingResolver.Humanize(path)
        };
      switch (path.ToLowerInvariant())
      {
        case "buttonsouth": return Face(family, "A", "Cross", "B", "South");
        case "buttoneast": return Face(family, "B", "Circle", "A", "East");
        case "buttonwest": return Face(family, "X", "Square", "Y", "West");
        case "buttonnorth": return Face(family, "Y", "Triangle", "X", "North");
        case "start": return Face(family, "Menu", "Options", "Plus", "Start");
        case "select": return Face(family, "View", "Share", "Minus", "Select");
        case "dpad": return "D-Pad";
        case "dpad/up": return "D-Pad Up";
        case "dpad/down": return "D-Pad Down";
        case "dpad/left": return "D-Pad Left";
        case "dpad/right": return "D-Pad Right";
        default: return InputBindingResolver.Humanize(path);
      }
    }

    private static string Face(InputGlyphFamily family, string xbox, string playStation, string nintendo, string generic)
      => family switch
      {
        InputGlyphFamily.Xbox => xbox, InputGlyphFamily.PlayStation => playStation,
        InputGlyphFamily.Nintendo => nintendo, _ => generic
      };
  }
}
