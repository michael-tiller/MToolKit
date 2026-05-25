using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Snapshot of truncation entries accumulated during a single save load, plus a derived
  ///   severity classification. Returned by <see cref="ITruncationReporter.DrainReport"/>.
  ///   Sealed class (not a struct) so <see cref="HasEntries"/> is always safe and a shared
  ///   <see cref="Empty"/> singleton can be returned without per-call allocation.
  /// </summary>
  public sealed class TruncationReport
  {
    /// <summary>
    ///   Shared empty report. Returned by <see cref="ITruncationReporter.DrainReport"/> when
    ///   nothing was reported, and used as the post-throw value of
    ///   <see cref="SaveSystemCoordinator.LastLoadTruncationReport"/>.
    /// </summary>
    public static readonly TruncationReport Empty = new(Array.Empty<TruncationEntry>());

    public IReadOnlyList<TruncationEntry> Entries { get; }
    public SaveTruncatedOnLoadMessage.Severity Severity { get; }
    public bool HasEntries => Entries.Count > 0;

    public TruncationReport(IReadOnlyList<TruncationEntry> entries)
    {
      Entries = entries ?? Array.Empty<TruncationEntry>();
      Severity = ComputeSeverity(Entries);
    }

    /// <summary>
    ///   Classifies a set of truncation entries. Each predicate null-guards
    ///   <c>entry?.Reason?</c>; a malformed entry with a null reason is treated as no-match
    ///   for the hash-drift floor.
    ///   Rules:
    ///     - Empty: <see cref="SaveTruncatedOnLoadMessage.Severity.Info"/>.
    ///     - Any <see cref="TruncationEntry.IsLoadBearing"/> == true: <c>BlockOverwrite</c>.
    ///     - Total <see cref="TruncationEntry.DroppedItemCount"/> &gt;= 10: <c>BlockOverwrite</c>.
    ///     - Any entry whose <see cref="TruncationEntry.Reason"/> starts with
    ///       <see cref="TruncationEntry.ReasonHashDriftShipping"/>: at least <c>Warning</c>
    ///       (schema drift is qualitatively stronger than version drift even at zero drops).
    ///     - Otherwise any non-zero drop: <c>Warning</c>.
    ///     - Otherwise: <c>Info</c>.
    ///   <para>
    ///     Entries whose <see cref="TruncationEntry.Reason"/> starts with
    ///     <see cref="TruncationEntry.ReasonLiveReferenceDemoted"/> are EXEMPT from the drop
    ///     accumulator and the "any non-zero drop" floor — demote preserves data, so a
    ///     demote-only report stays at <c>Info</c>. <see cref="TruncationEntry.IsLoadBearing"/>
    ///     still escalates if set on a demote entry (defensive — no current caller sets it).
    ///   </para>
    /// </summary>
    public static SaveTruncatedOnLoadMessage.Severity ComputeSeverity(IReadOnlyList<TruncationEntry> entries)
    {
      if (entries == null || entries.Count == 0)
        return SaveTruncatedOnLoadMessage.Severity.Info;

      int totalDrops = 0;
      bool hashDriftSeen = false;
      bool anyDrop = false;

      for (int i = 0; i < entries.Count; i++)
      {
        TruncationEntry entry = entries[i];
        if (entry == null) continue;

        if (entry.IsLoadBearing)
          return SaveTruncatedOnLoadMessage.Severity.BlockOverwrite;

        bool isDemote = entry.Reason != null
            && entry.Reason.StartsWith(TruncationEntry.ReasonLiveReferenceDemoted, StringComparison.Ordinal);

        if (!isDemote && entry.DroppedItemCount > 0)
        {
          anyDrop = true;
          totalDrops += entry.DroppedItemCount;
          if (totalDrops >= 10)
            return SaveTruncatedOnLoadMessage.Severity.BlockOverwrite;
        }

        if (!hashDriftSeen
            && entry.Reason != null
            && entry.Reason.StartsWith(TruncationEntry.ReasonHashDriftShipping, StringComparison.Ordinal))
          hashDriftSeen = true;
      }

      if (hashDriftSeen || anyDrop)
        return SaveTruncatedOnLoadMessage.Severity.Warning;

      return SaveTruncatedOnLoadMessage.Severity.Info;
    }
  }
}
