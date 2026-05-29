namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Marker interface for save DTOs that carry the migration framework's stamping metadata.
  ///   The framework's <see cref="ForwardMigrator{T}"/> writes through these properties on every save and
  ///   uses them on every load to detect missing-metadata, version-mismatch, and hash-drift conditions.
  /// </summary>
  public interface ISchemaStampedSaveData
  {
    /// <summary>Schema version stamped on save. Null/empty hash gates the missing-metadata refusal, not this field.</summary>
    int SaveVersion { get; set; }

    /// <summary>
    ///   12-hex-char SHA-256 prefix of the schema graph at save time. Null/empty means "no scaffold metadata",
    ///   which produces a fatal refusal at load per ADR-0004 (pre-scaffolding saves are refused, not retrofitted).
    /// </summary>
    string SaveSchemaHash { get; set; }
  }
}
