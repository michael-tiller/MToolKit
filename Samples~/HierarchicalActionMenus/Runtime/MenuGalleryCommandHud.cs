using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using MToolKit.Runtime.Input.Prompts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MToolKit.ContextMenus.Samples
{
  [Serializable]
  public sealed class MenuGalleryHudRow
  {
    [field: SerializeField] public string Label { get; set; }
    [field: SerializeField] public string ActionPath { get; set; }
    [field: SerializeField] public TMP_Text LabelTarget { get; set; }
    [field: SerializeField] public TMP_Text GlyphTarget { get; set; }
    [field: SerializeField] public Button Button { get; set; }
  }

  /// <summary>Sample-only command HUD bound to the gallery's chosen prompt service and actual runtime actions.</summary>
  public sealed class MenuGalleryCommandHud : MonoBehaviour
  {
    [field: SerializeField] public MenuGalleryHudRow[] Rows { get; set; } = Array.Empty<MenuGalleryHudRow>();

    private readonly List<IDisposable> bindings = new();
    private readonly List<(Button Button, UnityAction Listener)> listeners = new();
    private LayoutElement[] columns = Array.Empty<LayoutElement>();
    private float[] naturalWidths = Array.Empty<float>();
    private long generation;

    public void Bind(IInputPromptService prompts, InputActionAsset actions, Action back)
    {
      if (prompts == null) throw new ArgumentNullException(nameof(prompts));
      if (actions == null) throw new ArgumentNullException(nameof(actions));
      Unbind();
      var rows = Rows ?? Array.Empty<MenuGalleryHudRow>();
      var resolved = new InputAction[rows.Length];
      var unique = new HashSet<string>(StringComparer.Ordinal);
      for (int i = 0; i < rows.Length; i++)
      {
        var row = rows[i];
        if (row == null || string.IsNullOrEmpty(row.ActionPath) || !unique.Add(row.ActionPath)
          || row.LabelTarget == null || row.GlyphTarget == null)
          throw new InvalidOperationException("Each gallery command row requires a unique action path and independent label/glyph targets.");
        resolved[i] = actions.FindAction(row.ActionPath, throwIfNotFound: true);
      }

      long current = ++generation;
      columns = new LayoutElement[rows.Length];
      naturalWidths = new float[rows.Length];
      try
      {
        for (int i = 0; i < rows.Length; i++)
        {
          int index = i;
          var row = rows[i]; var action = resolved[i];
          row.LabelTarget.text = row.Label ?? string.Empty;
          columns[i] = row.GlyphTarget.GetComponent<LayoutElement>() ?? row.GlyphTarget.gameObject.AddComponent<LayoutElement>();
          naturalWidths[i] = 28;
          var binding = prompts.Bind(row.GlyphTarget, () =>
          {
            if (this == null || current != generation) return string.Empty;
            string glyph = prompts.Resolve(action, InputPromptStyle.Glyph) ?? string.Empty;
            // Measure the markup returned by this service. Do not clip a wide Enter/Escape/controller glyph into a fixed28px cell.
            float width = row.GlyphTarget.font != null
              ? row.GlyphTarget.GetPreferredValues(glyph, Mathf.Infinity, Mathf.Infinity).x : 0;
            naturalWidths[index] = string.IsNullOrEmpty(glyph) ? 0 : Mathf.Ceil(Mathf.Max(28, width));
            UpdateColumns();
            return glyph;
          });
          if (current != generation) { binding.Dispose(); return; }
          bindings.Add(binding);
          if (row.ActionPath == "Menu/Back" && row.Button != null && back != null)
          {
            UnityAction listener = () => { if (this != null && current == generation) back(); };
            row.Button.onClick.AddListener(listener);
            listeners.Add((row.Button, listener));
          }
        }
      }
      catch { Unbind(); throw; }
    }

    private void UpdateColumns()
    {
      float width = 0;
      for (int i = 0; i < naturalWidths.Length; i++) width = Mathf.Max(width, naturalWidths[i]);
      for (int i = 0; i < columns.Length; i++)
      {
        var column = columns[i]; if (column == null) continue;
        column.minWidth = column.preferredWidth = width;
        column.flexibleWidth = 0;
      }
    }

    public void Unbind()
    {
      generation++;
      var oldBindings = bindings.ToArray(); bindings.Clear();
      var oldListeners = listeners.ToArray(); listeners.Clear();
      columns = Array.Empty<LayoutElement>(); naturalWidths = Array.Empty<float>();
      foreach (var listener in oldListeners)
        if (listener.Button != null) listener.Button.onClick.RemoveListener(listener.Listener);
      Exception failure = null;
      foreach (var binding in oldBindings)
        try { binding.Dispose(); } catch (Exception error) { failure ??= error; }
      if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void OnDestroy() => Unbind();
  }
}