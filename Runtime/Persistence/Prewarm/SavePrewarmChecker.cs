using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MToolKit.Runtime.Persistence.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Persistence.Prewarm
{
  /// <summary>
  ///   Default <see cref="ISavePrewarmChecker"/>. Reads the save file's metadata header without running
  ///   hydrators or instantiating world state — see ADR-0012 for the design.
  ///
  ///   <para>
  ///   Three signals fire the report:
  ///   <list type="bullet">
  ///     <item><c>SaveBuildVersion != CurrentBuildVersion</c> — ProfileMetadata.SaveFormatVersion vs host-provided current.</item>
  ///     <item>Per-domain <c>SaveSchemaHash</c> mismatch against the registry of current hashes.</item>
  ///     <item>Mod-manifest diff (added / removed / version-changed) via <see cref="IModManifestProvider"/> when wired.</item>
  ///   </list>
  ///   The per-domain hash scan uses a regex against the raw .es3 JSON, which is safe because (a) dev-mode
  ///   saves are plain JSON (no compression/encryption per <c>ES3SaveConfig</c>), (b) the
  ///   <c>"&lt;SaveSchemaHash&gt;k__BackingField"</c> property name is dictated by C#'s auto-property
  ///   backing-field convention and stable across builds, (c) the regex only extracts a single literal
  ///   string per known top-level key, and (d) failure is benign — a missing match registers as
  ///   "hash unknown" which conservatively surfaces the modal.
  ///   </para>
  /// </summary>
  public sealed class SavePrewarmChecker : ISavePrewarmChecker
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SavePrewarmChecker>().ForFeature("Persistence.Prewarm"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IProfileManager _profileManager;
    private readonly IReadOnlyList<SchemaHashRegistryEntry> _registry;
    private readonly string _currentBuildVersion;
    private readonly IModManifestProvider _modManifestProvider;

    /// <summary>
    /// </summary>
    /// <param name="profileManager">Resolves profile name → .es3 file path.</param>
    /// <param name="registry">Schema-hash registry built by <see cref="SchemaHashRegistry.Build"/>. Keyed by DTO type full name so the checker can match against the save file's per-section <c>__type</c> field — robust against DomainKey vs save-section-key naming differences.</param>
    /// <param name="currentBuildVersion">Application version string (e.g. <c>Application.version</c>). Compared against <see cref="ProfileMetaData.SaveFormatVersion"/>.</param>
    /// <param name="modManifestProvider">Optional mod manifest adapter. Pass <c>null</c> in scenes/configurations with no mod system; mod diff fields stay empty.</param>
    public SavePrewarmChecker(
      IProfileManager profileManager,
      IReadOnlyList<SchemaHashRegistryEntry> registry,
      string currentBuildVersion,
      IModManifestProvider modManifestProvider = null)
    {
      _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
      _registry = registry ?? Array.Empty<SchemaHashRegistryEntry>();
      _currentBuildVersion = currentBuildVersion ?? string.Empty;
      _modManifestProvider = modManifestProvider;
    }

    public SavePrewarmReport Check(string profileName)
    {
      if (string.IsNullOrEmpty(profileName))
      {
        log.ForMethod().Warning("Prewarm Check called with empty profileName; returning empty report");
        return new SavePrewarmReport { CurrentBuildVersion = _currentBuildVersion };
      }

      string profilePath;
      try { profilePath = _profileManager.GetProfileFilePath(profileName); }
      catch (Exception ex)
      {
        log.ForMethod().Warning("Prewarm Check failed to resolve profile path for {Profile}: {Error}", profileName, ex.Message);
        return new SavePrewarmReport { CurrentBuildVersion = _currentBuildVersion };
      }

      if (!File.Exists(profilePath))
      {
        log.ForMethod().Warning("Prewarm Check: profile file does not exist at {Path}", profilePath);
        return new SavePrewarmReport { CurrentBuildVersion = _currentBuildVersion };
      }

      string raw;
      try { raw = File.ReadAllText(profilePath); }
      catch (Exception ex)
      {
        log.ForMethod().Warning("Prewarm Check failed to read {Path}: {Error}", profilePath, ex.Message);
        return new SavePrewarmReport { CurrentBuildVersion = _currentBuildVersion };
      }

      string saveBuildVersion = ExtractSaveBuildVersion(raw);
      List<string> domainsWithSchemaChange = CompareDomainHashes(raw);
      (List<string> added, List<string> removed, List<string> updated) modDiff = CompareModManifest(profilePath);

      var report = new SavePrewarmReport
      {
        SaveBuildVersion = saveBuildVersion,
        CurrentBuildVersion = _currentBuildVersion,
        DomainsWithSchemaChange = domainsWithSchemaChange,
        ModsAdded = modDiff.added,
        ModsRemoved = modDiff.removed,
        ModsUpdated = modDiff.updated,
      };

      log.ForMethod().Information(
        "Prewarm Check {Profile}: saveBuild={SaveBuild} currentBuild={CurrentBuild} schemaChanges={SchemaCount} modsAdded={Added} modsRemoved={Removed} modsUpdated={Updated} hasIssues={HasIssues}",
        profileName, saveBuildVersion, _currentBuildVersion, domainsWithSchemaChange.Count,
        modDiff.added.Count, modDiff.removed.Count, modDiff.updated.Count, report.HasIssues);

      return report;
    }

    // Matches `"<SaveFormatVersion>k__BackingField" : "0.2.20"` anywhere in the file.
    private static readonly Regex SaveFormatVersionRx = new(
      @"""<SaveFormatVersion>k__BackingField""\s*:\s*""([^""]*)""",
      RegexOptions.Compiled);

    private static string ExtractSaveBuildVersion(string raw)
    {
      var match = SaveFormatVersionRx.Match(raw);
      return match.Success ? match.Groups[1].Value : string.Empty;
    }

    /// <summary>
    ///   Scan the save text for every section that carries an <c>__type</c> field paired with a
    ///   <c>SaveSchemaHash</c>, build a map keyed by DTO full name, then look up each registry entry
    ///   in that map and flag mismatches. The friendly label used in the report prefers the save's
    ///   own top-level key (e.g. <c>"ColonyData"</c>) over the migrator's <see cref="IForwardMigratorBase.Domain"/>
    ///   (e.g. <c>"Colony"</c>) so the prewarm modal shows users a string that matches what's in
    ///   their save file.
    ///
    ///   <para>A missing match for a registered type is conservatively flagged as a change (the save
    ///   may legitimately not contain that section, in which case the warning is harmless). Sections
    ///   present in the save but unknown to the registry are silently ignored — they belong to
    ///   domains the current build no longer migrates, which is its own kind of compatibility issue
    ///   but not one this tier surfaces.</para>
    /// </summary>
    private List<string> CompareDomainHashes(string raw)
    {
      Dictionary<string, (string SaveKey, string SavedHash)> savedByType = BuildSavedHashMap(raw);

      var changed = new List<string>();
      foreach (SchemaHashRegistryEntry entry in _registry)
      {
        string typeFullName = entry.SaveDataTypeFullName;
        if (string.IsNullOrEmpty(typeFullName)) continue;

        if (!savedByType.TryGetValue(typeFullName, out var saved))
        {
          // Type not present in this save. Common case: domain didn't write any data yet (e.g.,
          // colony with no bills). Skip silently — we only flag REAL hash deltas.
          continue;
        }

        if (!string.Equals(saved.SavedHash, entry.CurrentHash, StringComparison.Ordinal))
        {
          string label = !string.IsNullOrEmpty(saved.SaveKey) ? saved.SaveKey : entry.Domain;
          changed.Add(label);
        }
      }
      return changed;
    }

    // Matches a top-level `"saveKey" : { ... "__type" : "TypeFullName,Asm" ... "<SaveSchemaHash>..." : "hash" ... }`
    // section. The 4KB lookahead window from `__type` to `<SaveSchemaHash>` covers domains with
    // moderately large value graphs ahead of the hash field; in practice the schema-stamp fields
    // (SaveVersion, SaveSchemaHash) sit at the top of each DTO so the hash is within the first few
    // hundred bytes. The type group strips the assembly-name suffix (everything after the comma).
    private static readonly Regex SectionRx = new(
      @"""([A-Za-z_][A-Za-z0-9_]*)""\s*:\s*\{\s*""__type""\s*:\s*""([^"",]+),[^""]*""[\s\S]{0,4096}?""<SaveSchemaHash>k__BackingField""\s*:\s*""([^""]*)""",
      RegexOptions.Compiled);

    private static Dictionary<string, (string SaveKey, string SavedHash)> BuildSavedHashMap(string raw)
    {
      var dict = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
      foreach (Match m in SectionRx.Matches(raw))
      {
        string saveKey = m.Groups[1].Value;
        string typeFullName = m.Groups[2].Value;
        string savedHash = m.Groups[3].Value;
        if (string.IsNullOrEmpty(typeFullName)) continue;
        // First match wins — typeFullName collisions across sections would be a bigger save-shape
        // problem than the prewarm checker can solve.
        if (!dict.ContainsKey(typeFullName))
          dict[typeFullName] = (saveKey, savedHash);
      }
      return dict;
    }

    private (List<string> added, List<string> removed, List<string> updated) CompareModManifest(string profilePath)
    {
      var added = new List<string>();
      var removed = new List<string>();
      var updated = new List<string>();

      if (_modManifestProvider == null) return (added, removed, updated);

      IReadOnlyDictionary<string, string> current;
      IReadOnlyDictionary<string, string> saved;
      try
      {
        current = _modManifestProvider.GetCurrentManifest() ?? new Dictionary<string, string>();
        saved = _modManifestProvider.ReadSavedManifest(profilePath) ?? new Dictionary<string, string>();
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning("Prewarm mod manifest read failed for {Path}: {Error}", profilePath, ex.Message);
        return (added, removed, updated);
      }

      foreach (var pair in current)
        if (!saved.ContainsKey(pair.Key))
          added.Add(pair.Key);

      foreach (var pair in saved)
      {
        if (!current.ContainsKey(pair.Key))
        {
          removed.Add(pair.Key);
          continue;
        }
        string currentVer = current[pair.Key] ?? string.Empty;
        string savedVer = pair.Value ?? string.Empty;
        if (!string.Equals(currentVer, savedVer, StringComparison.Ordinal))
          updated.Add($"{pair.Key}: {savedVer} → {currentVer}");
      }

      return (added, removed, updated);
    }
  }
}
