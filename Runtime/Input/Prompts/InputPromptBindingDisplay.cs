using MToolKit.Runtime.Input.Config;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MToolKit.Runtime.Input.Prompts
{
  /// <summary>Shared settings-row presentation. An explicit slot is independent of the active input scheme.</summary>
  internal static class InputPromptBindingDisplay
  {
    public static string Resolve(IInputPromptService prompts, InputAction action, int index,
      InputBinding.DisplayStringOptions options, TMP_FontAsset font, GamepadIconsData icons)
    {
      if (action == null || index < 0 || index >= action.bindings.Count) return "-";
      var binding = action.bindings[index];
      string path = (options & InputBinding.DisplayStringOptions.IgnoreBindingOverrides) != 0
        ? binding.path : binding.effectivePath;
      if (string.IsNullOrEmpty(path)) return "-";

      // Keep explicit legacy formatting flags and interaction descriptions exact. The
      // ordinary long/short labels use the shared localized presentation pipeline.
      if ((options & (InputBinding.DisplayStringOptions.DontOmitDevice
        | InputBinding.DisplayStringOptions.IgnoreBindingOverrides)) != 0
        || (options & InputBinding.DisplayStringOptions.DontIncludeInteractions) == 0
          && !string.IsNullOrEmpty(binding.effectiveInteractions))
        return InputPromptRichText.Escape(action.GetBindingDisplayString(index, options));

      var style = (options & InputBinding.DisplayStringOptions.DontUseShortDisplayNames) != 0
        ? InputPromptStyle.LocalizedName : InputPromptStyle.Character;
      var context = Context(prompts, index, font, icons);
      return prompts != null ? prompts.Resolve(action, style, context)
        : new InputPromptResolver().Resolve(action, style, context);
    }

    public static Sprite Icon(IInputPromptService prompts, GamepadIconsData icons, string layout, string path)
    {
      if (icons == null || string.IsNullOrEmpty(layout) || string.IsNullOrEmpty(path)
        || InputBindingResolver.IsKeyboardOrMouse(layout)) return null;
      var family = PresentationFamily(prompts, icons);
      return family.HasValue ? icons.GetIcon(family.Value, path) : icons.GetIcon(layout, path);
    }

    private static InputPromptContext Context(IInputPromptService prompts, int index, TMP_FontAsset font, GamepadIconsData icons)
      => new InputPromptContext(null, null, PresentationFamily(prompts, icons), index, font);

    private static InputGlyphFamily? PresentationFamily(IInputPromptService prompts, GamepadIconsData icons)
    {
      var context = prompts?.Context ?? default;
      if (context.FamilyOverride.HasValue) return context.FamilyOverride;
      if (context.Device == null || InputBindingResolver.IsKeyboardOrMouse(context.Device.layout)) return null;
      return icons?.Catalog != null ? icons.Catalog.ResolveFamily(context.Device.layout)
        : InputGlyphCatalog.FamilyForLayout(context.Device.layout);
    }
  }
}
