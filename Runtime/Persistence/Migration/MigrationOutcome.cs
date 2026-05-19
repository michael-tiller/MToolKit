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
    ///   Loaded data could not be exact-matched against the current schema; framework attempted
    ///   best-effort normalization and produced a truncation report via
    ///   <see cref="ITruncationReporter"/>. Covers both rows of the dispatch table:
    ///     - <c>loadedVersion &gt; CurrentSchemaVersion</c> (newer build), and
    ///     - <c>loadedVersion == CurrentSchemaVersion</c> with <c>loadedHash != CurrentSchemaHash</c>
    ///       in a shipping build (hash drift).
    ///   Migrators that own load-bearing schemas can refuse instead by overriding
    ///   <see cref="ForwardMigrator{T}.AllowsBestEffortNewerBuildLoad"/> to <c>false</c>;
    ///   that path returns <see cref="RefusedFatal"/>.
    /// </summary>
    TruncatedFromNewerBuild,

    /// <summary>Loaded data was refused; the caller must not proceed with materialization.</summary>
    RefusedFatal
  }
}
