using System;
using System.Collections.Generic;
using MToolKit.ContextMenus.Widgets;

namespace MToolKit.ContextMenus.Samples
{
  /// <summary>Per-gallery model. Reusable definition assets never store a user's checked state or inventory.</summary>
  public sealed class MenuGalleryState
  {
    public sealed class StockEntry
    {
      public StockEntry(string id, string label, int count) { Id = id; Label = label; Remaining = count; }
      public string Id { get; }
      public string Label { get; }
      public int Remaining { get; private set; }
      internal bool Take() { if (Remaining <= 0) return false; Remaining--; return true; }
      internal void Restore() => Remaining = 3;
    }

    public sealed class Details : IMenuActionDetails
    {
      private readonly MenuGalleryState state;
      private readonly string cost;
      public Details(MenuGalleryState state, string cost) { this.state = state; this.cost = cost; }
      public string CostText => cost ?? string.Empty;
      public string VariantText => state.Variant;
      public float? Progress => null;
    }

    private readonly HashSet<string> toggles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> choices = new(StringComparer.Ordinal);
    private readonly StockEntry[] stock =
    {
      new("sample.stock.timber", "Timber", 3), new("sample.stock.stone", "Stone", 3),
      new("sample.stock.fabric", "Fabric", 3)
    };
    private static readonly string[] Variants = { "Standard ammunition", "Alternate ammunition", "Training ammunition" };
    private int variant;
    public bool IsValid { get; private set; } = true;
    public bool CommandsEnabled { get; private set; } = true;
    public bool VariantsEnabled { get; private set; } = true;
    public int CommandsExecuted { get; private set; }
    public int VariantChanges { get; private set; }
    public int GameplayFireEvents { get; private set; }
    public int GameplayMoveEvents { get; private set; }
    public string LastCommand { get; private set; } = "None";
    public string Variant => Variants[variant];
    public IReadOnlyList<StockEntry> Stock => stock;
    public event Action Changed;

    public bool IsChecked(string id) => toggles.Contains(id);
    public bool IsChoice(string group, string value) => choices.TryGetValue(group, out var selected) && selected == value;
    public void Record(string id, string label)
    {
      if (!IsValid || !CommandsEnabled) return;
      CommandsExecuted++; LastCommand = label + " (" + id + ")"; Changed?.Invoke();
    }
    public void Toggle(string id, string label)
    {
      if (!IsValid || !CommandsEnabled) return;
      if (!toggles.Add(id)) toggles.Remove(id);
      Record(id, label);
    }
    public void Choose(string group, string value, string id, string label)
    {
      if (!IsValid || !CommandsEnabled) return;
      choices[group] = value; Record(id, label);
    }
    public void CycleVariant()
    {
      if (!IsValid || !VariantsEnabled) return;
      variant = (variant + 1) % Variants.Length; VariantChanges++; Changed?.Invoke();
    }
    public void Take(StockEntry entry)
    {
      if (IsValid && CommandsEnabled && entry != null && entry.Take()) Record(entry.Id, "Take " + entry.Label);
    }
    public void ToggleCommands() { CommandsEnabled = !CommandsEnabled; Changed?.Invoke(); }
    public void ToggleVariants() { VariantsEnabled = !VariantsEnabled; Changed?.Invoke(); }
    public void Invalidate() { IsValid = false; Changed?.Invoke(); }
    public void Restore()
    {
      IsValid = CommandsEnabled = VariantsEnabled = true;
      foreach (var entry in stock) entry.Restore();
      Changed?.Invoke();
    }
    public void RecordGameplayFire() { GameplayFireEvents++; Changed?.Invoke(); }
    public void RecordGameplayMove() { GameplayMoveEvents++; Changed?.Invoke(); }
  }
}
