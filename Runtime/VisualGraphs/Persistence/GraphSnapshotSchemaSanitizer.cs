using System;
using System.Collections.Generic;
using ES3Internal;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Persistence
{
  /// <summary>
  ///   Schema guards for <see cref="Runtime.State.GraphStateSnapshot" /> data crossing the save/load boundary
  ///   (9.0.4). Save side: unserializable values fail loud and are skipped, never silently dropped on load.
  ///   Load side: a saved value whose type no longer matches its declaration is discarded loudly and replaced
  ///   with the declared default — replace, not remove, because import MERGES into existing state, so the
  ///   default must overwrite any value already initialized there.
  /// </summary>
  public static class GraphSnapshotSchemaSanitizer
  {
    // Non-cached logger (NOT the house Lazy<ILogger>): cold warning paths only, and resolving
    // Log.Logger at log time is what lets tests assert warnings via a swapped sink.
    private static ILogger log => Log.Logger.ForContext(typeof(GraphSnapshotSchemaSanitizer)).ForFeature("VisualGraphs.Persistence") ?? Logger.None;

    /// <summary>
    ///   Returns a copy of <paramref name="data" /> with every value ES3 cannot meaningfully serialize removed
    ///   (Warning per skip). Null values are kept (legal for a declared String). Two gates: the condition
    ///   <c>ES3Writer.Write(object, …)</c> itself fails on (type null or <c>isUnsupported</c>), plus an explicit
    ///   delegate rejection — ES3's reflection fallback "successfully" writes a delegate as an empty
    ///   <c>{"__type"}</c> envelope that cannot be reconstructed on load (write succeeds, load corrupts), which
    ///   is exactly the silent drop 9.0.4 forbids. Residual risk: a non-delegate type with zero serializable
    ///   members would slip through the same way; ES3's reflected-type classes are internal, so there is no
    ///   clean hook to detect that case — revisit if real state ever carries such a type.
    /// </summary>
    public static Dictionary<string, object> FilterUnserializable(IReadOnlyDictionary<string, object> data, string graphId)
    {
      var filtered = new Dictionary<string, object>(data?.Count ?? 0);
      if (data == null) return filtered;

      foreach (var kv in data)
      {
        if (kv.Value == null)
        {
          filtered[kv.Key] = null;
          continue;
        }

        var es3Type = kv.Value is Delegate
          ? null
          : ES3TypeMgr.GetOrCreateES3Type(kv.Value.GetType(), throwException: false);
        if (es3Type == null || es3Type.isUnsupported)
        {
          log.Warning("Skipping unserializable state value for graph '{GraphId}': key '{Key}' of type {ValueType} cannot be saved",
            graphId, kv.Key, kv.Value.GetType().FullName);
          continue;
        }

        filtered[kv.Key] = kv.Value;
      }

      return filtered;
    }

    /// <summary>
    ///   Enforces schema-change behavior #4 on a snapshot's data before it is merged into state: a declared
    ///   key whose saved value no longer matches the declared type is replaced with the declared default
    ///   (Warning). Undeclared keys load untouched (behavior #3); a null or invalid declaration set is a
    ///   no-op. Mirrors <c>VariableStorage</c>'s exact-type rule: null is legal only for a declared String,
    ///   and out-of-range declaration enum values are skipped, never thrown on.
    /// </summary>
    public static void SanitizeTypeMismatches(IDictionary<string, object> data, GraphVariableSet declarations, string targetId)
    {
      if (data == null || declarations == null) return;

      List<string> mismatchedKeys = null;
      foreach (var kv in data)
      {
        var declaration = declarations.Find(kv.Key);
        if (declaration == null || !Enum.IsDefined(typeof(EGraphVariableType), declaration.type)) continue;

        var matches = kv.Value == null
          ? declaration.type == EGraphVariableType.String
          : kv.Value.GetType() == declaration.GetValueType();
        if (matches) continue;

        (mismatchedKeys ??= new List<string>()).Add(kv.Key);
        log.Warning("Schema mismatch on load for '{TargetId}': key '{Key}' saved as {SavedType} but declared {DeclaredType}; saved value discarded, declared default applies",
          targetId, kv.Key, kv.Value?.GetType().FullName ?? "null", declaration.type);
      }

      if (mismatchedKeys == null) return;
      foreach (var key in mismatchedKeys)
        data[key] = declarations.Find(key).GetDefaultValue();
    }
  }
}
