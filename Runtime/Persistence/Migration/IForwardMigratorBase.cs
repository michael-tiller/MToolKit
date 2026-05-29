using System;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Non-generic surface for <see cref="ForwardMigrator{T}"/>. Lets infrastructure code (schema-hash
  ///   registry, prewarm checker, diagnostics) enumerate migrators without knowing each <c>TSaveData</c>.
  ///   Generic <see cref="IForwardMigrator{TSaveData}"/> remains the typed surface used by save controllers.
  /// </summary>
  public interface IForwardMigratorBase
  {
    /// <summary>
    ///   Stable domain identifier matching <see cref="MToolKit.Runtime.Persistence.TruncationEntry.DomainKey"/>.
    ///   Default is the migrator type's short name (e.g., <c>"ColonyMigrator"</c>) unless overridden.
    /// </summary>
    string Domain { get; }

    /// <summary>Schema version this migrator produces on save.</summary>
    int CurrentSchemaVersion { get; }

    /// <summary>Schema hash this migrator produces on save (12-hex-char SHA-256 prefix of the schema graph).</summary>
    string CurrentSchemaHash { get; }

    /// <summary>
    ///   The DTO type this migrator stamps (<c>typeof(TSaveData)</c>). Used by infrastructure code
    ///   (prewarm checker) to match a migrator against the save file's per-section <c>__type</c>
    ///   field — independent of the migrator's <see cref="Domain"/> key, which the save file does
    ///   not store and which often differs from the save controller's top-level section key.
    /// </summary>
    Type SaveDataType { get; }
  }
}
