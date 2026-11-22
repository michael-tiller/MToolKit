using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Localization;
using Serilog;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MToolKit.ContextMenus.Presentation
{
  /// <summary>Async, menu-lifetime localization cache. Literal/sample labels need no localization setup.</summary>
  internal sealed class MenuLabelResolver : IDisposable
  {
    private sealed class Entry { public MenuLabel Label; public string Text; public int Generation; }
    private readonly Dictionary<(string table, string key, string fallback), Entry> entries = new();
    private readonly Func<MenuLabel, string> overrideResolver;
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    public event Action Changed;

    public MenuLabelResolver(Func<MenuLabel, string> overrideResolver)
    {
      this.overrideResolver = overrideResolver;
      LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    public string Resolve(MenuLabel label)
    {
      if (disposed) return label.Fallback ?? string.Empty;
      if (overrideResolver != null) return overrideResolver(label) ?? label.Fallback ?? string.Empty;
      if (!label.IsLocalized) return label.Fallback ?? string.Empty;
      var key = (label.Table, label.Key, label.Fallback);
      if (!entries.TryGetValue(key, out var entry))
      {
        entry = new Entry { Label = label, Text = label.Fallback ?? string.Empty };
        entries.Add(key, entry);
        RefreshAsync(entry).Forget();
      }
      return entry.Text;
    }

    private void OnLocaleChanged(Locale locale)
    {
      // A cached localization operation can complete synchronously and a Changed listener may
      // register another visible label. Iterate a snapshot rather than the live dictionary.
      foreach (var entry in new List<Entry>(entries.Values))
      {
        entry.Text = entry.Label.Fallback ?? string.Empty;
        RefreshAsync(entry).Forget();
      }
      Changed?.Invoke();
    }

    private async UniTask RefreshAsync(Entry entry)
    {
      int generation = ++entry.Generation;
      try
      {
        // Querying the entry first preserves literal fallback for a missing table/key rather than
        // displaying the localization package's diagnostic string. The string database owns its cache.
        var result = await LocalizationSettings.StringDatabase.GetTableEntryAsync(entry.Label.Table,
          entry.Label.Key).ToUniTask(cancellationToken: lifetime.Token);
        string text = result.Entry == null ? entry.Label.Fallback
          : await LocalizationHelper.GetLocalizedStringAsync(entry.Label.Table, entry.Label.Key)
            .AttachExternalCancellation(lifetime.Token);
        if (disposed || generation != entry.Generation) return;
        entry.Text = string.IsNullOrEmpty(text) ? entry.Label.Fallback ?? string.Empty : text;
        Changed?.Invoke();
      }
      catch (OperationCanceledException) { }
      catch (Exception exception)
      {
        if (!disposed) Log.ForContext<MenuLabelResolver>().Warning(exception,
          "Menu localization unavailable for {Table}/{Key}; using fallback", entry.Label.Table, entry.Label.Key);
      }
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
      lifetime.Cancel(); lifetime.Dispose(); entries.Clear(); Changed = null;
    }
  }
}
