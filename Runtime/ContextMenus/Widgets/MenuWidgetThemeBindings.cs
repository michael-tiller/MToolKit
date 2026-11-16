using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using MToolKit.Theme.Spacing;
using MToolKit.Theme.Typeset;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Widgets
{
  /// <summary>Temporary menu bindings use the existing MTK presets and restore their authored owner when retired.</summary>
  internal sealed class MenuWidgetThemeBindings
  {
    private Dictionary<TypesetPreset, TypographyBinding> typography;
    private Dictionary<SpacingPreset, SpacingBinding> spacing;
    private RetiringBindings retiring;

    public void ApplyTypography(TMP_Text text, Typeset token)
    {
      if (text == null) return;
      var preset = text.GetComponent<TypesetPreset>();
      if (preset == null) return;
      if (token == null)
      {
        if (typography != null && typography.TryGetValue(preset, out var previous))
        { typography.Remove(preset); previous.Restore(); }
        return;
      }
      if (preset.Text == null) return;
      typography ??= new Dictionary<TypesetPreset, TypographyBinding>();
      if (!typography.TryGetValue(preset, out var binding))
      { binding = new TypographyBinding(this, preset, FindRetiringTypography(preset)); typography.Add(preset, binding); }
      binding.Apply(token);
    }

    public void ApplySpacing(SpacingPreset preset, Spacing token)
    {
      if (preset == null || preset.LayoutGroup == null) return;
      if (token == null)
      {
        if (spacing != null && spacing.TryGetValue(preset, out var previous))
        { spacing.Remove(preset); previous.Restore(); }
        return;
      }
      spacing ??= new Dictionary<SpacingPreset, SpacingBinding>();
      if (!spacing.TryGetValue(preset, out var binding))
      { binding = new SpacingBinding(this, preset, FindRetiringSpacing(preset)); spacing.Add(preset, binding); }
      binding.Apply(token);
    }

    public void Restore()
    {
      // Detach active ownership before callbacks; a new binding can inherit the still-retiring authored baseline.
      var oldTypography = typography; typography = null;
      var oldSpacing = spacing; spacing = null;
      var previous = retiring;
      if (oldTypography != null && oldTypography.Count > 0 || oldSpacing != null && oldSpacing.Count > 0)
        retiring = new RetiringBindings(oldTypography, oldSpacing, previous);
      Exception failure = null;
      try
      {
        if (oldTypography != null)
          foreach (var binding in oldTypography.Values)
            try { binding.Restore(); } catch (Exception error) { failure ??= error; }
        if (oldSpacing != null)
          foreach (var binding in oldSpacing.Values)
            try { binding.Restore(); } catch (Exception error) { failure ??= error; }
      }
      finally
      {
        retiring = previous;
        oldTypography?.Clear(); typography ??= oldTypography;
        oldSpacing?.Clear(); spacing ??= oldSpacing;
      }
      if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private TypographyBinding FindRetiringTypography(TypesetPreset preset)
    {
      for (var frame = retiring; frame != null; frame = frame.Previous)
        if (frame.Typography != null && frame.Typography.TryGetValue(preset, out var binding) && binding.CanTransfer()) return binding;
      return null;
    }
    private SpacingBinding FindRetiringSpacing(SpacingPreset preset)
    {
      for (var frame = retiring; frame != null; frame = frame.Previous)
        if (frame.Spacing != null && frame.Spacing.TryGetValue(preset, out var binding) && binding.CanTransfer()) return binding;
      return null;
    }
    private sealed class RetiringBindings
    {
      public readonly Dictionary<TypesetPreset, TypographyBinding> Typography;
      public readonly Dictionary<SpacingPreset, SpacingBinding> Spacing;
      public readonly RetiringBindings Previous;
      public RetiringBindings(Dictionary<TypesetPreset, TypographyBinding> typography,
        Dictionary<SpacingPreset, SpacingBinding> spacing, RetiringBindings previous)
      { Typography = typography; Spacing = spacing; Previous = previous; }
    }

    private bool HasTypographyOwner(TypesetPreset preset) => typography != null && typography.ContainsKey(preset);
    private bool HasSpacingOwner(SpacingPreset preset) => spacing != null && spacing.ContainsKey(preset);

    private sealed class TypographyBinding
    {
      private readonly MenuWidgetThemeBindings owner;
      private readonly TypesetPreset preset;
      private readonly TMP_Text target;
      private readonly Typeset original;
      private readonly bool overridesTheme;
      private readonly TextStyle baseline;
      private Typeset applied;
      public TypographyBinding(MenuWidgetThemeBindings owner, TypesetPreset preset, TypographyBinding retiring)
      {
        this.owner = owner; this.preset = preset; target = preset.Text;
        original = retiring != null ? retiring.original : preset.ConfiguredAsset;
        overridesTheme = retiring != null ? retiring.overridesTheme : preset.OverridesTheme;
        baseline = retiring != null ? retiring.baseline : new TextStyle(target);
      }
      public bool CanTransfer()
        => preset != null && target != null && preset.Text == target &&
          (preset.ConfiguredAsset == applied && preset.OverridesTheme ||
           preset.ConfiguredAsset == original && preset.OverridesTheme == overridesTheme);
      public void Apply(Typeset token)
      {
        applied = token;
        preset.SetBinding(token, true);
      }
      public void Restore()
      {
        if (preset == null || target == null || preset.Text != target || owner.HasTypographyOwner(preset) ||
            preset.ConfiguredAsset != applied || !preset.OverridesTheme) return;
        preset.SetBinding(original, overridesTheme);
        if (original == null && CanRestoreBaseline()) baseline.Restore(target, CanRestoreBaseline);
      }
      private bool CanRestoreBaseline()
        => preset != null && target != null && preset.Text == target && !owner.HasTypographyOwner(preset)
          && preset.ConfiguredAsset == original && preset.OverridesTheme == overridesTheme;
    }

    private readonly struct TextStyle
    {
      private readonly TMP_FontAsset font;
      private readonly Material material;
      private readonly bool autoSize;
      private readonly float size, minimum, maximum, line, character, word, paragraph;
      private readonly FontStyles style;
      public TextStyle(TMP_Text text)
      {
        font = text.font; material = text.fontSharedMaterial; autoSize = text.enableAutoSizing;
        size = text.fontSize; minimum = text.fontSizeMin; maximum = text.fontSizeMax;
        style = text.fontStyle; line = text.lineSpacing; character = text.characterSpacing;
        word = text.wordSpacing; paragraph = text.paragraphSpacing;
      }
      public void Restore(TMP_Text text, Func<bool> owns)
      {
        if (!owns()) return; text.font = font;
        if (!owns()) return; text.fontSharedMaterial = material;
        if (!owns()) return; text.enableAutoSizing = autoSize;
        if (!owns()) return; text.fontSizeMin = minimum;
        if (!owns()) return; text.fontSizeMax = maximum;
        if (!owns()) return; text.fontSize = size;
        if (!owns()) return; text.fontStyle = style;
        if (!owns()) return; text.lineSpacing = line;
        if (!owns()) return; text.characterSpacing = character;
        if (!owns()) return; text.wordSpacing = word;
        if (!owns()) return; text.paragraphSpacing = paragraph;
      }
    }

    private sealed class SpacingBinding
    {
      private readonly MenuWidgetThemeBindings owner;
      private readonly SpacingPreset preset;
      private readonly LayoutGroup target;
      private readonly Spacing original;
      private readonly bool overridesTheme;
      private readonly RectOffset padding;
      private readonly float horizontalOrVerticalGap;
      private readonly Vector2 gridGap;
      private Spacing applied;
      public SpacingBinding(MenuWidgetThemeBindings owner, SpacingPreset preset, SpacingBinding retiring)
      {
        this.owner = owner; this.preset = preset; target = preset.LayoutGroup;
        original = retiring != null ? retiring.original : preset.ConfiguredAsset;
        overridesTheme = retiring != null ? retiring.overridesTheme : preset.OverridesTheme;
        var authored = retiring != null ? retiring.padding : target.padding;
        padding = new RectOffset(authored.left, authored.right, authored.top, authored.bottom);
        if (target is HorizontalOrVerticalLayoutGroup horizontal)
          horizontalOrVerticalGap = retiring != null ? retiring.horizontalOrVerticalGap : horizontal.spacing;
        if (target is GridLayoutGroup grid) gridGap = retiring != null ? retiring.gridGap : grid.spacing;
      }
      public bool CanTransfer()
        => preset != null && target != null && preset.LayoutGroup == target &&
          (preset.ConfiguredAsset == applied && preset.OverridesTheme ||
           preset.ConfiguredAsset == original && preset.OverridesTheme == overridesTheme);
      public void Apply(Spacing token)
      {
        applied = token;
        preset.SetBinding(token, true);
      }
      public void Restore()
      {
        if (preset == null || target == null || preset.LayoutGroup != target || owner.HasSpacingOwner(preset) ||
            preset.ConfiguredAsset != applied || !preset.OverridesTheme) return;
        preset.SetBinding(original, overridesTheme);
        if (original != null || !CanRestoreBaseline()) return;
        target.padding = new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
        if (!CanRestoreBaseline()) return;
        if (target is HorizontalOrVerticalLayoutGroup horizontal) horizontal.spacing = horizontalOrVerticalGap;
        else if (target is GridLayoutGroup grid) grid.spacing = gridGap;
      }
      private bool CanRestoreBaseline()
        => preset != null && target != null && preset.LayoutGroup == target && !owner.HasSpacingOwner(preset)
          && preset.ConfiguredAsset == original && preset.OverridesTheme == overridesTheme;
    }
  }
}
