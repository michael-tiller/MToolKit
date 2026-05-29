namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Result of a forward-migration load attempt.
  /// </summary>
  public enum MigrationOutcome
  {
    /// <summary>Loaded data was already at the current schema version and hash; no work performed.</summary>
    None,

    /// <summary>Loaded data was migrated forward to the current schema (older version or editor-side hash drift).</summary>
    Migrated,

    /// <summary>
    ///   Loaded data could not be exact-matched against the current schema; the framework loaded it
    ///   best-effort (containers + normalize + re-stamp) and produced a truncation report via
    ///   <see cref="ITruncationReporter"/>. Per ADR-0016 every stamped save takes this path rather than
    ///   being refused. Covers three rows of the dispatch table:
    ///     - <c>loadedVersion &gt; CurrentSchemaVersion</c> (newer build),
    ///     - <c>loadedVersion &lt; MinimumSupportedVersion</c> (below the compatibility floor — no
    ///       version-specific <c>Migrate</c> body runs), and
    ///     - <c>loadedVersion == CurrentSchemaVersion</c> with <c>loadedHash != CurrentSchemaHash</c>
    ///       in a shipping build (hash drift).
    /// </summary>
    TruncatedBestEffort,

    /// <summary>Loaded data was refused; the caller must not proceed with materialization.</summary>
    RefusedFatal
  }
}
