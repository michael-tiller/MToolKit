using System;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Forward-only migrator for a single save DTO root. Stamps metadata on save, dispatches version/hash
  ///   resolution on load. Constraint forces the DTO to carry the framework's stamping fields at every API
  ///   boundary so controllers cannot accept a non-stamped type.
  /// </summary>
  public interface IForwardMigrator<TSaveData> where TSaveData : class, ISchemaStampedSaveData
  {
    /// <summary>Schema version this migrator produces on save.</summary>
    int CurrentSchemaVersion { get; }

    /// <summary>
    ///   Lowest version this migrator can migrate forward from. Defaults to <see cref="CurrentSchemaVersion"/>
    ///   (refuse all older). Migrators that DO support older versions explicitly opt in by overriding.
    /// </summary>
    int MinimumSupportedVersion { get; }

    /// <summary>Schema hash this migrator produces on save (12-hex-char SHA-256 prefix of the schema graph).</summary>
    string CurrentSchemaHash { get; }

    /// <summary>
    ///   Prepares <paramref name="data"/> for serialization: container init, normalization, metadata stamp.
    ///   Returns true if any field changed during preparation (caller may use this for dirty-tracking).
    ///   Throws <see cref="ArgumentNullException"/> if <paramref name="data"/> is null.
    /// </summary>
    bool PrepareForSave(TSaveData data);

    /// <summary>
    ///   Resolves a just-deserialized <paramref name="data"/> against the current schema. Uses
    ///   <paramref name="loadedVersion"/> and <paramref name="loadedHash"/> as the read-from-disk snapshot
    ///   so callers can pass pre-stamp values for accurate logging. Throws <see cref="ArgumentNullException"/>
    ///   if <paramref name="data"/> is null.
    /// </summary>
    MigrationOutcome MigrateForLoad(TSaveData data, int loadedVersion, string loadedHash);
  }
}
