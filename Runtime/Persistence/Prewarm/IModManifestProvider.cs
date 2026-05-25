using System.Collections.Generic;

namespace MToolKit.Runtime.Persistence.Prewarm
{
  /// <summary>
  ///   Optional adapter that surfaces the current mod manifest (id → version) and reads a saved manifest
  ///   from a save file. Implemented in the game layer where mod knowledge lives; MToolKit's prewarm checker
  ///   takes <c>null</c> when no mod system is wired (mod diff fields stay empty in the report).
  /// </summary>
  public interface IModManifestProvider
  {
    /// <summary>Currently-loaded mod IDs mapped to their version strings.</summary>
    IReadOnlyDictionary<string, string> GetCurrentManifest();

    /// <summary>
    ///   Mod IDs and versions recorded in <paramref name="profileFilePath"/>. Returns an empty dictionary
    ///   when the save predates mod-manifest persistence (treated as "unknown mods", which conservatively
    ///   surfaces the modal — safe default per ADR-0012 backwards-compat policy).
    /// </summary>
    IReadOnlyDictionary<string, string> ReadSavedManifest(string profileFilePath);
  }
}
