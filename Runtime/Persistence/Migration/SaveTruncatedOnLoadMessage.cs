using System.Collections.Generic;
using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Published via <c>GameMessageBroker</c> when a save load completed with truncation
  ///   (newer-build best-effort load, hash drift in shipping build, polymorphic ref dropped, etc.).
  ///   The save coordinator drains an <see cref="ITruncationReporter"/> after a successful load
  ///   and publishes this message when any entries were reported.
  /// </summary>
  public sealed class SaveTruncatedOnLoadMessage : IGameMessage
  {
    public enum Severity
    {
      Info,

      Warning,

      /// <summary>Severe enough that the caller should offer "back out, do not save over this" UX.</summary>
      BlockOverwrite
    }

    public Severity Level { get; set; }
    public IReadOnlyList<TruncationEntry> Entries { get; set; }

    public SaveTruncatedOnLoadMessage(Severity level, IReadOnlyList<TruncationEntry> entries)
    {
      Level = level;
      Entries = entries;
    }
  }

  /// <summary>One truncation observation, per save-domain controller, accumulated by the load-time reporter.</summary>
  public sealed class TruncationEntry
  {
    /// <summary>Canonical reason string for "loaded version &gt; current schema version" truncation.</summary>
    public const string ReasonNewerBuild = "newer-build";

    /// <summary>
    ///   Canonical reason string for hash-drift-in-shipping-build truncation (row 7 in the
    ///   <see cref="ForwardMigrator{T}"/> dispatch table). Per-migrator subcategories may
    ///   append <c>":subcategory"</c>; severity classification matches by prefix.
    /// </summary>
    public const string ReasonHashDriftShipping = "hash-drift-shipping";

    /// <summary>
    ///   Canonical reason prefix for "polymorphic ref dropped during Normalize" entries.
    ///   Per-migrator subcategories append <c>":field-name"</c>.
    /// </summary>
    public const string ReasonPolymorphicRefDropped = "polymorphic-ref-dropped";

    public string DomainKey { get; set; }
    public int LoadedVersion { get; set; }
    public int CurrentVersion { get; set; }
    public string Reason { get; set; }
    public int DroppedItemCount { get; set; }

    /// <summary>
    ///   Set by per-migrator <c>Normalize</c> when a dropped item belonged to a schema the
    ///   game cannot meaningfully run without (e.g., a player class definition).
    ///   <see cref="TruncationReport.ComputeSeverity"/> promotes the whole report to
    ///   <see cref="SaveTruncatedOnLoadMessage.Severity.BlockOverwrite"/> when any entry has
    ///   this flag set.
    /// </summary>
    public bool IsLoadBearing { get; set; }

    public TruncationEntry(string domainKey, int loadedVersion, int currentVersion, string reason, int droppedItemCount, bool isLoadBearing = false)
    {
      DomainKey = domainKey;
      LoadedVersion = loadedVersion;
      CurrentVersion = currentVersion;
      Reason = reason;
      DroppedItemCount = droppedItemCount;
      IsLoadBearing = isLoadBearing;
    }
  }
}
