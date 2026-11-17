using System;

namespace MToolKit.ContextMenus
{
  /// <summary>Stable action identity; sibling placements must have distinct identities.</summary>
  [Serializable]
  public readonly struct MenuActionId : IEquatable<MenuActionId>
  {
    public MenuActionId(string value)
    {
      if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An action requires a stable identity.", nameof(value));
      Value = value;
    }
    public string Value { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(MenuActionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is MenuActionId other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value ?? string.Empty;
    public static implicit operator MenuActionId(string value) => new MenuActionId(value);
    public static bool operator ==(MenuActionId left, MenuActionId right) => left.Equals(right);
    public static bool operator !=(MenuActionId left, MenuActionId right) => !left.Equals(right);
  }

  /// <summary>Localized text identity and literal fallback, independent of any presenter.</summary>
  public readonly struct MenuLabel
  {
    public MenuLabel(string fallback, string key = null, string table = "Default String Table")
    { Fallback = fallback ?? string.Empty; Key = key; Table = table; }
    public string Fallback { get; }
    public string Key { get; }
    public string Table { get; }
    public bool IsLocalized => !string.IsNullOrWhiteSpace(Key);
    public override string ToString() => Fallback ?? string.Empty;
  }

  /// <summary>One evaluated state snapshot; re-evaluated before invoking a command.</summary>
  public readonly struct MenuActionState
  {
    public MenuActionState(bool visible = true, bool enabled = true, bool isChecked = false, string disabledReason = null)
    { Visible = visible; Enabled = enabled; IsChecked = isChecked; DisabledReason = disabledReason; }
    public bool Visible { get; }
    public bool Enabled { get; }
    public bool IsChecked { get; }
    public string DisabledReason { get; }
    public static MenuActionState Available => new MenuActionState(true, true);
  }

  /// <summary>Finite policy after behavior execution; it is not an action-kind dispatcher.</summary>
  public enum MenuActionOutcome { Keep, Refresh, Close, GoBack, Enter }
  public enum MenuInteractionMode { Pointer, Keyboard, Controller }

  public readonly struct MenuRow
  {
    public MenuRow(MenuAction action, int depth, bool expanded)
    { Action = action; Depth = depth; Expanded = expanded; }
    public MenuAction Action { get; }
    public int Depth { get; }
    public bool Expanded { get; }
  }
}
