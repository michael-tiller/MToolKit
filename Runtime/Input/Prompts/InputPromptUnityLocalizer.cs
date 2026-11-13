using System;
using MToolKit.Runtime.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MToolKit.Runtime.Input.Prompts
{
  /// <summary>Uses Unity's shared table cache without blocking localization initialization or owning its handles.</summary>
  internal sealed class InputPromptUnityLocalizer : IInputPromptLocalizer, IDisposable
  {
    private readonly Func<string> tableName;
    private readonly Action changed;
    private AsyncOperationHandle<StringTable> pending;
    private Action<AsyncOperationHandle<StringTable>> completed;
    private string requestedTable;
    private Locale requestedLocale;
    private StringTable table;
    private bool disposed;

    public InputPromptUnityLocalizer(Func<string> tableName, Action changed)
    { this.tableName = tableName; this.changed = changed; }

    public string GetName(string semanticKey, string fallback) => Localize(semanticKey, fallback);

    public string Localize(string key, string fallback, params object[] arguments)
    {
      EnsureTable();
      var entry = table != null && !string.IsNullOrEmpty(key) ? table.GetEntry(key) : null;
      return entry != null && !string.IsNullOrEmpty(entry.Value)
        ? entry.GetLocalizedString(arguments)
        : LocalizationHelper.FormatSmartString(fallback ?? string.Empty, arguments);
    }

    private void EnsureTable()
    {
      string selectedTable = tableName();
      if (disposed) return;
      if (string.IsNullOrWhiteSpace(selectedTable) || !LocalizationSettings.HasSettings)
      {
        Invalidate();
        return;
      }
      var selection = LocalizationSettings.SelectedLocaleAsync;
      Locale locale = selection.IsDone ? selection.Result : null;
      if (pending.IsValid() && requestedTable == selectedTable && requestedLocale == locale) return;
      Invalidate();
      requestedTable = selectedTable;
      requestedLocale = locale;
      pending = LocalizationSettings.StringDatabase.GetTableAsync(selectedTable, locale);
      if (pending.IsDone)
      {
        table = pending.Status == AsyncOperationStatus.Succeeded ? pending.Result : null;
        return;
      }
      completed = operation =>
      {
        if (disposed || !pending.Equals(operation)) return;
        table = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
        changed();
      };
      pending.Completed += completed;
    }

    public void Invalidate()
    {
      if (pending.IsValid() && completed != null) pending.Completed -= completed;
      // The StringDatabase owns its cached operation; releasing it here would invalidate other consumers.
      pending = default;
      completed = null;
      table = null;
      requestedTable = null;
      requestedLocale = null;
    }

    public void Dispose() { disposed = true; Invalidate(); }
  }
}
