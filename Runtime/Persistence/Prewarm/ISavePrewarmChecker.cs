namespace MToolKit.Runtime.Persistence.Prewarm
{
  /// <summary>
  ///   Inspects a save file's metadata header to surface coarse compatibility signals (build version delta,
  ///   per-domain schema-hash mismatch, mod-list diff) before the user commits to a full load. See ADR-0012.
  ///   Intended to run in the bootstrap/menu scope, before the scene swap that triggers the actual load.
  /// </summary>
  public interface ISavePrewarmChecker
  {
    /// <summary>
    ///   Reads the save file for <paramref name="profileName"/> and returns a <see cref="SavePrewarmReport"/>
    ///   summarizing compatibility against the current build. Hot-path requirement: header-only reads,
    ///   no DTO graph hydration, no Unity object instantiation. Target: sub-50ms typical.
    /// </summary>
    SavePrewarmReport Check(string profileName);
  }
}
