using System;
using System.Collections.Generic;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Abstract base for forward-only save-data migrators. Provides the dispatch table for
  ///   <see cref="MigrateForLoad"/>, the stamping logic for <see cref="PrepareForSave"/>, and the schema
  ///   hash computation (cached on first access). Concrete subclasses declare the schema surface
  ///   (<see cref="SchemaRoots"/>, <see cref="NamespacePrefixes"/>) and the per-DTO normalization logic.
  /// </summary>
  public abstract class ForwardMigrator<T> : IForwardMigrator<T>, IForwardMigratorBase
    where T : class, ISchemaStampedSaveData
  {
    // Explicit IForwardMigratorBase implementation — exposes the protected DomainKey through a
    // non-generic accessor without changing the accessibility of the override mechanism, so the
    // 13+ existing subclasses keep their `protected override string DomainKey` declarations.
    string IForwardMigratorBase.Domain => DomainKey;

    // SaveDataType exposes typeof(T) so infrastructure (e.g., the save-prewarm checker) can match
    // a migrator to the save file's per-section `__type` field. This avoids relying on DomainKey
    // matching the save's top-level section key, which often differs (e.g., DomainKey="Colony"
    // vs save key="ColonyData") and produced false-positive schema-change diffs.
    Type IForwardMigratorBase.SaveDataType => typeof(T);

    protected readonly ILogger Log;
    protected readonly ITruncationReporter TruncationReporter;

    /// <summary>
    ///   Back-compat ctor for external/in-repo subclasses that predate the truncation reporter.
    ///   Defaults to <see cref="NullTruncationReporter.Instance"/> so opt-in best-effort loads
    ///   compile and run, but emit no <see cref="SaveTruncatedOnLoadMessage"/>.
    /// </summary>
    protected ForwardMigrator(ILogger logger) : this(logger, NullTruncationReporter.Instance) { }

    protected ForwardMigrator(ILogger logger, ITruncationReporter truncationReporter)
    {
      if (logger == null) throw new ArgumentNullException(nameof(logger));
      if (truncationReporter == null) throw new ArgumentNullException(nameof(truncationReporter));
      Log = logger.ForContext(GetType()).ForFeature("Persistence.Migration");
      TruncationReporter = truncationReporter;
    }

    public abstract int CurrentSchemaVersion { get; }

    /// <summary>
    ///   The compatibility floor. Defaults to <see cref="CurrentSchemaVersion"/>. At or above it, a
    ///   version-specific <see cref="Migrate"/> body runs to forward-migrate. Below it (per ADR-0016) a
    ///   stamped save still loads best-effort with a truncation report — it is NOT refused; it simply
    ///   skips the (unwritten) version transform. Lower the floor + override <see cref="Migrate"/> when a
    ///   validated forward transform is warranted.
    /// </summary>
    public virtual int MinimumSupportedVersion => CurrentSchemaVersion;

    public string CurrentSchemaHash => _hash ?? (_hash = ComputeAndLogHash());
    private string _hash;

    protected abstract Type[] SchemaRoots { get; }
    protected abstract IEnumerable<string> NamespacePrefixes { get; }
    protected virtual IMemberEnumerator MemberEnumerator => ES3MemberEnumerator.Instance;

    /// <summary>
    ///   Overridable for dispatch tests. <see cref="Application.isEditor"/> is always true in EditMode,
    ///   so dispatch tests subclass and flip this to cover the shipping hash-drift row.
    /// </summary>
    protected virtual bool IsEditor => Application.isEditor;

    /// <summary>
    ///   Returns true when this migrator accepts saves missing the SaveSchemaHash stamp at the given
    ///   loaded version. Override for legacy controllers that supported saves predating the migration
    ///   framework (e.g., LAIRD ModProfile v1). Default false — pre-scaffolding saves are refused per ADR-0004.
    /// </summary>
    protected virtual bool AllowsLegacyHashlessLoad(int loadedVersion) => false;

    /// <summary>
    ///   Stable short identifier for this migrator's domain, attached to
    ///   <see cref="TruncationEntry.DomainKey"/> rows. Default is the migrator type's
    ///   short name; in-repo migrators override with a curated string (e.g. "Colony",
    ///   "WorkOrders") so the value survives type renames and stays readable in UI/logs.
    /// </summary>
    protected virtual string DomainKey => GetType().Name;

    /// <summary>
    ///   Container initialization (null collections, missing dictionaries, etc.). Runs before <see cref="Normalize"/>.
    ///   Pure container null-coalescing belongs HERE, not in <see cref="Normalize"/>, so that
    ///   <see cref="Normalize"/> returning true reliably signals a semantic change (legacy-shape repair).
    /// </summary>
    protected abstract void EnsureContainers(T data);

    /// <summary>
    ///   Clamp ranges, normalize unknown enum values via Enum.IsDefined, deduplicate, repair legacy shapes.
    ///   Returns true when any semantic change was made (signals callers that a re-stamp is warranted).
    ///   Idempotent — callers may invoke twice safely; the second call must return false.
    ///   Pure container init (`data.Foo ??= new List&lt;...&gt;()`) belongs in <see cref="EnsureContainers"/>, not here.
    /// </summary>
    protected abstract bool Normalize(T data);

    /// <summary>
    ///   Apply version-specific transformations to forward-migrate. No-op by default.
    ///   Called only when <c>loadedVersion &lt; CurrentSchemaVersion</c> and <c>loadedVersion &gt;= MinimumSupportedVersion</c>.
    /// </summary>
    protected virtual void Migrate(T data, int oldVersion, string oldHash) { }

    /// <summary>
    ///   Writes scaffold metadata (SaveVersion + SaveSchemaHash) through the <see cref="ISchemaStampedSaveData"/> interface.
    ///   Concrete migrators can override to add more stamping (e.g., AppVersion), but base guarantees these two are set.
    /// </summary>
    protected virtual void StampMetadata(T data)
    {
      data.SaveVersion = CurrentSchemaVersion;
      data.SaveSchemaHash = CurrentSchemaHash;
    }

    public bool PrepareForSave(T data)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));
      EnsureContainers(data);
      var normalized = Normalize(data);
      var prevVersion = data.SaveVersion;
      var prevHash = data.SaveSchemaHash;
      StampMetadata(data);
      var metadataChanged = prevVersion != data.SaveVersion || prevHash != data.SaveSchemaHash;
      return normalized || metadataChanged;
    }

    public MigrationOutcome MigrateForLoad(T data, int loadedVersion, string loadedHash)
    {
      if (data == null) throw new ArgumentNullException(nameof(data));

      // Row 1: missing scaffold metadata. Default = ADR-0004 refusal. Legacy controllers (LAIRD ModProfile v1)
      // opt in via AllowsLegacyHashlessLoad and fall through to the version-check rows with oldHash=null.
      if (string.IsNullOrEmpty(loadedHash))
      {
        if (!AllowsLegacyHashlessLoad(loadedVersion))
        {
          Log.Error("Save refused: no SaveSchemaHash stamp; cannot safely validate hashless save shape (ADR-0004/ADR-0016). LoadedVersion={LoadedVersion}", loadedVersion);
          return MigrationOutcome.RefusedFatal;
        }
        Log.Information("Loading legacy hashless save. LoadedVersion={LoadedVersion}", loadedVersion);
      }

      // Row 2: from a newer build. Best-effort load + report (per ADR-0016 a stamped save is never refused).
      if (loadedVersion > CurrentSchemaVersion)
      {
        Log.Warning("Loading newer-build save best-effort. LoadedVersion={LoadedVersion}, Current={Current}", loadedVersion, CurrentSchemaVersion);
        EnsureContainers(data);
        Normalize(data); // Per-migrator Normalize may report domain-specific drops via TruncationReporter.
        StampMetadata(data);
        TruncationReporter.Report(new TruncationEntry(DomainKey, loadedVersion, CurrentSchemaVersion, TruncationEntry.ReasonNewerBuild, droppedItemCount: 0));
        return MigrationOutcome.TruncatedBestEffort;
      }

      // Row 3: below the compatibility floor. No version-specific Migrate body was written for these
      // versions, but per ADR-0016 a stamped save is never refused — load best-effort (containers +
      // Normalize + re-stamp) and report so the player learns what may not have carried forward.
      if (loadedVersion < MinimumSupportedVersion)
      {
        Log.Warning("Loading below-floor save best-effort. LoadedVersion={LoadedVersion}, Floor={Floor}, Current={Current}", loadedVersion, MinimumSupportedVersion, CurrentSchemaVersion);
        EnsureContainers(data);
        Normalize(data);
        StampMetadata(data);
        TruncationReporter.Report(new TruncationEntry(DomainKey, loadedVersion, CurrentSchemaVersion, TruncationEntry.ReasonBelowFloor, droppedItemCount: 0));
        return MigrationOutcome.TruncatedBestEffort;
      }

      // Row 4: older but in supported range -> migrate forward. Normalize return value ignored;
      // StampMetadata always runs.
      if (loadedVersion < CurrentSchemaVersion)
      {
        Log.Information("Migrating save: {From} -> {To}", loadedVersion, CurrentSchemaVersion);
        EnsureContainers(data);
        Migrate(data, loadedVersion, loadedHash);
        Normalize(data);
        StampMetadata(data);
        return MigrationOutcome.Migrated;
      }

      // loadedVersion == CurrentSchemaVersion below.

      // Row 5: exact match. Run EnsureContainers + Normalize anyway; if Normalize signals a semantic
      // change (legacy-shape repair surface), stamp + return Migrated so the next save persists the fix.
      if (loadedHash == CurrentSchemaHash)
      {
        EnsureContainers(data);
        var changed = Normalize(data);
        if (changed)
        {
          StampMetadata(data);
          return MigrationOutcome.Migrated;
        }
        return MigrationOutcome.None;
      }

      // Row 6: hash drift, editor build -> re-normalize + stamp (so next save persists corrected hash).
      if (IsEditor)
      {
        Log.Warning("Hash drift detected in editor; re-normalizing. LoadedHash={Got}, Current={Want}", loadedHash, CurrentSchemaHash);
        EnsureContainers(data);
        Normalize(data);
        StampMetadata(data);
        return MigrationOutcome.Migrated;
      }

      // Row 7: hash drift, shipping build. Best-effort load + report (per ADR-0016 a stamped save is never refused).
      Log.Warning("Loading shipping-build hash-drift save best-effort. LoadedHash={Got}, Current={Want}", loadedHash, CurrentSchemaHash);
      EnsureContainers(data);
      Normalize(data);
      StampMetadata(data);
      TruncationReporter.Report(new TruncationEntry(DomainKey, loadedVersion, CurrentSchemaVersion, TruncationEntry.ReasonHashDriftShipping, droppedItemCount: 0));
      return MigrationOutcome.TruncatedBestEffort;
    }

    private string ComputeAndLogHash()
    {
      var result = SchemaHashWalker.For(SchemaRoots)
        .WithNamespacePrefixes(NamespacePrefixes)
        .WithMemberEnumerator(MemberEnumerator)
        .Compute();

      foreach (var diag in result.Diagnostics)
      {
        Log.Warning("Schema walker diagnostic: {Kind} on {Type}.{Field} - {Message}",
          diag.Kind, diag.TypeFullName, diag.FieldName, diag.Message);
      }

      return result.Hash;
    }
  }
}
