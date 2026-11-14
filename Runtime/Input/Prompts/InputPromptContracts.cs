using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Prompts
{
  public enum InputPromptStyle
  {
    Auto = 0,
    Character = 1,
    LocalizedName = 2,
    Glyph = 3,
    GlyphAndName = 4
  }

  public enum InputPromptUnboundBehavior
  {
    ShowUnbound = 0,
    KeyboardAndMouse = 1,
    Hide = 2
  }

  public enum InputGlyphFamily
  {
    KeyboardAndMouse = 0,
    Xbox = 1,
    PlayStation = 2,
    Nintendo = 3,
    Generic = 4
  }

  public enum InputGlyphEntryKind
  {
    Binding = 0,
    Device = 1
  }

  public enum InputKeyboardGlyphMode
  {
    Authored = 0,
    GeneratedKeycaps = 1
  }

  public enum InputKeycapContentMode
  {
    Label = 0,
    Sprite = 1,
    SpriteAndLabel = 2
  }

  public enum InputGlyphPlatform
  {
    Any = 0,
    Windows = 1,
    MacOS = 2,
    Linux = 3
  }

  /// <summary>A caller's binding selection and independent presentation preference.</summary>
  public readonly struct InputPromptContext
  {
    public InputPromptContext(InputDevice device = null, string bindingGroup = null,
      InputGlyphFamily? familyOverride = null, int? bindingIndex = null)
      : this(device, bindingGroup, familyOverride, bindingIndex, null, null) { }

    public InputPromptContext(InputDevice device, string bindingGroup,
      InputGlyphFamily? familyOverride, int? bindingIndex, TMP_FontAsset fontAsset)
      : this(device, bindingGroup, familyOverride, bindingIndex, fontAsset, null) { }

    public InputPromptContext(InputDevice device, string bindingGroup,
      InputGlyphFamily? familyOverride, int? bindingIndex, TMP_FontAsset fontAsset, InputGlyphPlatform? platformOverride)
      : this(device, bindingGroup, familyOverride, bindingIndex, fontAsset, platformOverride,
        InputPromptUnboundBehavior.ShowUnbound) { }

    public InputPromptContext(InputDevice device, string bindingGroup,
      InputGlyphFamily? familyOverride, int? bindingIndex, TMP_FontAsset fontAsset, InputGlyphPlatform? platformOverride,
      InputPromptUnboundBehavior unboundBehavior = InputPromptUnboundBehavior.ShowUnbound)
    {
      Device = device;
      BindingGroup = bindingGroup;
      FamilyOverride = familyOverride;
      BindingIndex = bindingIndex;
      FontAsset = fontAsset;
      PlatformOverride = platformOverride;
      UnboundBehavior = unboundBehavior;
    }

    public InputDevice Device { get; }
    public string BindingGroup { get; }
    public InputGlyphFamily? FamilyOverride { get; }
    public int? BindingIndex { get; }
    public TMP_FontAsset FontAsset { get; }
    public InputGlyphPlatform? PlatformOverride { get; }
    public InputPromptUnboundBehavior UnboundBehavior { get; }

    public InputPromptContext WithPlatform(InputGlyphPlatform? platform)
      => new InputPromptContext(Device, BindingGroup, FamilyOverride, BindingIndex, FontAsset, platform, UnboundBehavior);

    public InputPromptContext WithUnboundBehavior(InputPromptUnboundBehavior unboundBehavior)
      => new InputPromptContext(Device, BindingGroup, FamilyOverride, BindingIndex, FontAsset, PlatformOverride, unboundBehavior);
  }

  public readonly struct InputPromptControl
  {
    public InputPromptControl(string deviceLayout, string controlPath, string displayName, string character = null,
      string partName = null)
    {
      DeviceLayout = deviceLayout;
      ControlPath = controlPath;
      DisplayName = displayName;
      Character = character;
      PartName = partName;
    }

    public string DeviceLayout { get; }
    public string ControlPath { get; }
    public string DisplayName { get; }
    public string Character { get; }
    public string PartName { get; }
  }

  public sealed class ResolvedInputBinding
  {
    public InputAction Action { get; internal set; }
    public int BindingIndex { get; internal set; } = -1;
    public IReadOnlyList<InputPromptControl> Controls { get; internal set; } = new InputPromptControl[0];
    public string CompositeName { get; internal set; }
    public bool IsBound => Controls.Count != 0;
  }

  public interface IInputPromptLocalizer
  {
    string GetName(string semanticKey, string fallback);
  }

  /// <summary>Optional locale identity for localized, text-bearing baked glyphs.</summary>
  public interface IInputPromptLocaleProvider
  {
    string LocaleCode { get; }
  }

  /// <summary>Protects text arguments while preserving native TMP rich text for generated prompts.</summary>
  public static class InputPromptRichText
  {
    // Escape each opening delimiter separately, including an externally supplied </noparse>.
    // Break backslash adjacency too: TMP decodes Unicode escapes before parsing tags.
    // TMP does not implement HTML entity decoding; &lt; would be displayed literally.
    public static string Escape(string value) => string.IsNullOrEmpty(value)
      ? string.Empty : value.Replace("<", "<noparse><</noparse>").Replace("\\", "<noparse>\\</noparse>");
  }
}
