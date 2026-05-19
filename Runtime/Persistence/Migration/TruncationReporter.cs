using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Default <see cref="ITruncationReporter"/> backed by a <see cref="ConcurrentQueue{T}"/>.
  ///   <see cref="BeginLoadScope"/> atomically swaps the queue so a fresh load starts with
  ///   an empty accumulator. The save coordinator's load-gate semaphore guarantees only one
  ///   load is in flight at a time; the atomic swap is belt-and-braces in case the
  ///   guarantee is violated by future changes.
  /// </summary>
  public sealed class TruncationReporter : ITruncationReporter
  {
    private ConcurrentQueue<TruncationEntry> _queue = new();

    public void BeginLoadScope()
    {
      Interlocked.Exchange(ref _queue, new ConcurrentQueue<TruncationEntry>());
    }

    public void Report(TruncationEntry entry)
    {
      if (entry == null) throw new ArgumentNullException(nameof(entry));
      _queue.Enqueue(entry);
    }

    public TruncationReport DrainReport()
    {
      ConcurrentQueue<TruncationEntry> snapshot = Interlocked.Exchange(ref _queue, new ConcurrentQueue<TruncationEntry>());
      if (snapshot.IsEmpty)
        return TruncationReport.Empty;

      List<TruncationEntry> entries = new(snapshot.Count);
      while (snapshot.TryDequeue(out TruncationEntry e))
        entries.Add(e);

      if (entries.Count == 0)
        return TruncationReport.Empty;

      return new TruncationReport(entries);
    }
  }

  /// <summary>
  ///   No-op <see cref="ITruncationReporter"/>. Used by the back-compat
  ///   <see cref="ForwardMigrator{T}"/> ctor and by tests that don't exercise truncation.
  /// </summary>
  public sealed class NullTruncationReporter : ITruncationReporter
  {
    public static readonly NullTruncationReporter Instance = new();
    private NullTruncationReporter() { }
    public void BeginLoadScope() { }
    public void Report(TruncationEntry entry) { }
    public TruncationReport DrainReport() => TruncationReport.Empty;
  }
}
