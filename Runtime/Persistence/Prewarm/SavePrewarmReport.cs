using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.Persistence.Prewarm
{
  /// <summary>
  ///   Output of <see cref="ISavePrewarmChecker.Check"/>. Captures coarse compatibility signals readable from the
  ///   save file's metadata header without running hydrators or instantiating world state. See ADR-0012 for the
  ///   two-tier (prewarm + post-load) save-compatibility surface this feeds.
  /// </summary>
  public sealed class SavePrewarmReport
  {
    /// <summary>True if any signal — version delta, schema-hash mismatch, or mod-list diff — fires.</summary>
    public bool HasIssues =>
      !string.IsNullOrEmpty(SaveBuildVersion) && SaveBuildVersion != CurrentBuildVersion
      || DomainsWithSchemaChange.Count > 0
      || ModsAdded.Count > 0 || ModsRemoved.Count > 0 || ModsUpdated.Count > 0;

    /// <summary>SaveFormatVersion read from ProfileMetadata. Empty when missing or unreadable.</summary>
    public string SaveBuildVersion { get; set; } = string.Empty;

    /// <summary>Current build version supplied by the host at check time.</summary>
    public string CurrentBuildVersion { get; set; } = string.Empty;

    /// <summary>Domain keys (matching <see cref="Migration.ForwardMigrator{T}.DomainKey"/>) whose stored hash does not match the current build's hash.</summary>
    public IReadOnlyList<string> DomainsWithSchemaChange { get; set; } = Array.Empty<string>();

    /// <summary>Mod IDs present in the current load that were not present in the save.</summary>
    public IReadOnlyList<string> ModsAdded { get; set; } = Array.Empty<string>();

    /// <summary>Mod IDs present in the save that are not present in the current load.</summary>
    public IReadOnlyList<string> ModsRemoved { get; set; } = Array.Empty<string>();

    /// <summary>Mod IDs whose version string changed between save and current load (formatted "id: oldVer → newVer").</summary>
    public IReadOnlyList<string> ModsUpdated { get; set; } = Array.Empty<string>();
  }
}
