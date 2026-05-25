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
    ///   Per-migrator subcategories append <c>":field-name"</c>. Distinct from
    ///   <see cref="ReasonRegistryReferenceDropped"/>: this prefix is for Type.GetType-style
    ///   poly-refs (the <c>PolymorphicResolve</c> helper).
    /// </summary>
    public const string ReasonPolymorphicRefDropped = "polymorphic-ref-dropped";

    /// <summary>
    ///   Canonical reason prefix for "registry-lookup ID dropped during save controller restore"
    ///   entries. Used by save controllers (Phase E.2 Wave 2+) when a string ID present in a
    ///   loaded save does not resolve in the live static registry (recipe / item /
    ///   world-object-definition registry, etc.). Per-controller subcategories append
    ///   <c>":FieldName"</c> (e.g. <c>:RecipeId</c>, <c>:ItemDefId</c>,
    ///   <c>:DefinitionId-orphan</c>).
    /// </summary>
    public const string ReasonRegistryReferenceDropped = "registry-ref-dropped";

    /// <summary>
    ///   Canonical reason prefix for "live runtime cross-reference dropped during post-load
    ///   hydrate" entries. Used by <c>IPostLoadHydrator</c> implementations (Phase E.2 Wave 3)
    ///   when a runtime object's field pointed at another runtime object that did not survive
    ///   the load (orphan-dropped by a sibling controller, layer no longer exists, etc.).
    ///   Per-hydrator subcategories append <c>":FieldName"</c>
    ///   (e.g. <c>:WorkstationId</c>, <c>:ParentBill</c>, <c>:LayerId</c>).
    ///   <para>
    ///     <b>Version sentinel:</b> entries using this reason MUST be reported with
    ///     <see cref="LoadedVersion"/> = 0 and <see cref="CurrentVersion"/> = 0 — they are
    ///     not version-driven (no migration occurred).
    ///   </para>
    ///   <para>
    ///     <b>Single-count rule:</b> when one root entity fails multiple cross-ref checks,
    ///     the hydrator should report ONE primary entry with <see cref="DroppedItemCount"/> = 1
    ///     plus any number of secondary entries with <see cref="DroppedItemCount"/> = 0 (audit
    ///     only). This prevents double-counting in severity computation while preserving full
    ///     diagnostic detail in <c>LastLoadTruncationReport</c>.
    ///   </para>
    /// </summary>
    public const string ReasonLiveReferenceDropped = "live-reference-dropped";

    /// <summary>
    ///   Canonical reason prefix for "live runtime cross-reference demoted during post-load
    ///   hydrate" entries — the referencing entity SURVIVES with the dead field normalized
    ///   (e.g. set to a None/default value). Distinct from
    ///   <see cref="ReasonLiveReferenceDropped"/>: data is preserved, the back-reference is
    ///   the only thing lost. Canonical case: a Workstation work-order whose <c>ParentBill</c>
    ///   no longer resolves on load (e.g. mod uninstall removed the bill); the order keeps
    ///   its recipe and continues executing as a "raw" order.
    ///   <para>
    ///     <b>Severity exemption:</b> entries using this reason DO NOT contribute to
    ///     <see cref="TruncationReport.ComputeSeverity"/>'s drop accumulator. Demote is not
    ///     data loss; a demote-only report should never escalate beyond <c>Info</c>.
    ///   </para>
    ///   <para>
    ///     <b>Version sentinel:</b> as with <see cref="ReasonLiveReferenceDropped"/>, entries
    ///     using this reason MUST be reported with <see cref="LoadedVersion"/> = 0 and
    ///     <see cref="CurrentVersion"/> = 0.
    ///   </para>
    /// </summary>
    public const string ReasonLiveReferenceDemoted = "live-reference-demoted";

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
