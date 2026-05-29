using System;
using System.Collections.Generic;
using MToolKit.Runtime.Persistence.Migration;

namespace MToolKit.Runtime.Persistence.Prewarm
{
  /// <summary>
  ///   Per-migrator entry in the schema-hash registry. Carries the DTO type's full name (used to
  ///   match against the save file's per-section <c>__type</c> field), the domain key (used as a
  ///   human-friendly label in the prewarm modal when the save side has no better identifier), and
  ///   the current build's computed hash.
  /// </summary>
  public readonly struct SchemaHashRegistryEntry
  {
    public string SaveDataTypeFullName { get; }
    public string Domain { get; }
    public string CurrentHash { get; }

    public SchemaHashRegistryEntry(string saveDataTypeFullName, string domain, string currentHash)
    {
      SaveDataTypeFullName = saveDataTypeFullName ?? string.Empty;
      Domain = domain ?? string.Empty;
      CurrentHash = currentHash ?? string.Empty;
    }
  }

  /// <summary>
  ///   Builds the registry that <see cref="SavePrewarmChecker"/> compares against. Pure utility —
  ///   the host enumerates <see cref="IForwardMigratorBase"/> instances (in Dirigible's case via a
  ///   reflection scan of <see cref="ForwardMigrator{T}"/> subclasses, since migrators are not
  ///   DI-registered) and passes them in once at startup. Result is immutable.
  ///
  ///   <para>Keyed by DTO type full name (e.g. <c>Dirigible.Workstations.Bills.Persistence.BillSaveData</c>)
  ///   because that's what the save file's per-section <c>__type</c> field stores. Matching by DomainKey
  ///   alone produced false positives where the migrator's domain string didn't match the save
  ///   controller's top-level section key (e.g. <c>"Colony"</c> vs <c>"ColonyData"</c>).</para>
  /// </summary>
  public static class SchemaHashRegistry
  {
    /// <summary>
    ///   Snapshot every migrator's current schema hash. Returns one entry per distinct DTO type
    ///   (duplicates on the same TSaveData are skipped — the first wins).
    /// </summary>
    public static IReadOnlyList<SchemaHashRegistryEntry> Build(IEnumerable<IForwardMigratorBase> migrators)
    {
      var entries = new List<SchemaHashRegistryEntry>();
      if (migrators == null) return entries;

      var seenTypes = new HashSet<string>(StringComparer.Ordinal);
      foreach (IForwardMigratorBase m in migrators)
      {
        if (m == null) continue;
        Type dtoType = m.SaveDataType;
        if (dtoType == null) continue;
        string typeFullName = dtoType.FullName;
        if (string.IsNullOrEmpty(typeFullName)) continue;
        if (!seenTypes.Add(typeFullName)) continue;

        entries.Add(new SchemaHashRegistryEntry(
          saveDataTypeFullName: typeFullName,
          domain: m.Domain ?? string.Empty,
          currentHash: m.CurrentSchemaHash ?? string.Empty));
      }
      return entries;
    }
  }
}
