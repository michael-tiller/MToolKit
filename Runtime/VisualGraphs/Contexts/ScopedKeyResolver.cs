using System;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Contexts
{
  /// <summary>
  ///   A parsed scoped key: data, not behavior. <see cref="Scope" /> and <see cref="OwnerId" /> are
  ///   meaningful only when <see cref="IsLocal" /> is false.
  /// </summary>
  public readonly struct ScopedKeyRef
  {
    public EGraphContextScope Scope { get; }
    public string OwnerId { get; }
    public string Key { get; }
    public bool IsLocal { get; }

    private ScopedKeyRef(EGraphContextScope scope, string ownerId, string key, bool isLocal)
    {
      Scope = scope;
      OwnerId = ownerId;
      Key = key;
      IsLocal = isLocal;
    }

    public static ScopedKeyRef Local(string key) => new(EGraphContextScope.Graph, null, key, true);

    public static ScopedKeyRef Scoped(EGraphContextScope scope, string ownerId, string key) =>
      new(scope, ownerId, key, false);
  }

  /// <summary>
  ///   The single cross-scope variable access path. Parses a scoped key and routes the bare key to the right
  ///   context's <see cref="IVariableStorage" />. One resolver, several consumers (9.4 cross-graph queries,
  ///   9.5 interpolation/conditions reuse it).
  /// </summary>
  /// <remarks>
  ///   <para><b>Grammar (ordinal, case-sensitive, no trimming):</b> reserved prefixes are exactly
  ///   <c>player.</c>, <c>world.</c>, <c>quest:</c>. A <c>quest:</c> id runs to the FIRST <c>.</c> (ids
  ///   cannot contain dots — quest GUIDs do not); the key remainder is verbatim and may contain dots. Any
  ///   input not starting with a reserved prefix is a LOCAL key verbatim.</para>
  ///   <para><b>Consequence:</b> a local key literally named <c>player.gold</c> is unreachable through the
  ///   resolver (reserved by design); bare <c>player</c> without a dot is an ordinary local key.</para>
  ///   <para><b>Misses never throw</b> (a missing quest context → warning + caller fallback). Only MALFORMED
  ///   syntax fails loud (ArgumentException), including through <see cref="Get{T}" />/<see cref="Set{T}" />.</para>
  /// </remarks>
  public sealed class ScopedKeyResolver
  {
    private const string PlayerPrefix = "player.";
    private const string WorldPrefix = "world.";
    private const string QuestPrefix = "quest:";

    // Non-cached logger (NOT the house Lazy<ILogger>): cold warning paths only, and resolving Log.Logger at
    // log time is what lets tests assert the fallback warnings via a swapped sink.
    private static ILogger log => Log.Logger.ForContext<ScopedKeyResolver>().ForFeature("VisualGraphs.Contexts") ?? Logger.None;

    private readonly GraphContextRegistry registry;

    public ScopedKeyResolver(GraphContextRegistry registry)
    {
      this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    ///   Parse only — no routing. Malformed input throws <see cref="ArgumentException" />. Public so
    ///   authoring-time validation (9.5) can syntax-check keys without a live registry.
    /// </summary>
    public static ScopedKeyRef Parse(string scopedKey)
    {
      if (string.IsNullOrWhiteSpace(scopedKey))
        throw new ArgumentException("Scoped key must be non-empty.", nameof(scopedKey));

      if (scopedKey.StartsWith(PlayerPrefix, StringComparison.Ordinal))
        return ParseSimpleScope(EGraphContextScope.Player, PlayerPrefix, scopedKey);
      if (scopedKey.StartsWith(WorldPrefix, StringComparison.Ordinal))
        return ParseSimpleScope(EGraphContextScope.World, WorldPrefix, scopedKey);
      if (scopedKey.StartsWith(QuestPrefix, StringComparison.Ordinal))
        return ParseQuest(scopedKey);

      return ScopedKeyRef.Local(scopedKey);
    }

    private static ScopedKeyRef ParseSimpleScope(EGraphContextScope scope, string prefix, string scopedKey)
    {
      var key = scopedKey.Substring(prefix.Length);
      if (key.Length == 0)
        throw new ArgumentException($"Scoped key '{scopedKey}' has an empty key after '{prefix}'.", nameof(scopedKey));
      return ScopedKeyRef.Scoped(scope, null, key);
    }

    private static ScopedKeyRef ParseQuest(string scopedKey)
    {
      var body = scopedKey.Substring(QuestPrefix.Length);
      var dot = body.IndexOf('.');
      if (dot < 0)
        throw new ArgumentException($"Quest key '{scopedKey}' must be 'quest:<id>.<key>'.", nameof(scopedKey));

      var ownerId = body.Substring(0, dot);
      var key = body.Substring(dot + 1);
      if (ownerId.Length == 0)
        throw new ArgumentException($"Quest key '{scopedKey}' has an empty quest id.", nameof(scopedKey));
      if (key.Length == 0)
        throw new ArgumentException($"Quest key '{scopedKey}' has an empty key.", nameof(scopedKey));

      return ScopedKeyRef.Scoped(EGraphContextScope.Graph, ownerId, key);
    }

    /// <summary>
    ///   Resolve the storage a scoped key routes to. Returns false ONLY when the target context is missing
    ///   (a quest context that does not exist) — Player/World are lazily created and never miss, local
    ///   always resolves to <paramref name="local" />. Malformed keys throw; a bare key with a null
    ///   <paramref name="local" /> throws (programmer error).
    /// </summary>
    public bool TryResolveStorage(string scopedKey, IGraphContext local,
      out IVariableStorage storage, out string bareKey)
    {
      var parsed = Parse(scopedKey);
      bareKey = parsed.Key;

      if (parsed.IsLocal)
      {
        if (local == null)
          throw new ArgumentNullException(nameof(local),
            $"A local context is required to resolve the bare key '{scopedKey}'.");
        storage = local.Variables;
        return true;
      }

      if (parsed.Scope != EGraphContextScope.Graph)
      {
        storage = registry.GetOrCreate(parsed.Scope, null).Variables; // lazy singleton — never misses
        return true;
      }

      if (registry.TryGet(EGraphContextScope.Graph, parsed.OwnerId, out var context))
      {
        storage = context.Variables;
        return true;
      }

      storage = null;
      return false;
    }

    /// <summary>
    ///   Read through the scoped key. A present value or a declared default returns silently; an unset +
    ///   undeclared key on a resolved target, or a missing target context, logs a warning and returns
    ///   <paramref name="fallback" />. Never throws on a miss (malformed keys still throw).
    /// </summary>
    public T Get<T>(string scopedKey, IGraphContext local, T fallback = default)
    {
      if (!TryResolveStorage(scopedKey, local, out var storage, out var bareKey))
      {
        log.Warning("Scoped key '{Key}': no matching context; returning caller fallback", scopedKey);
        return fallback;
      }

      if (!storage.Contains(bareKey))
      {
        // target exists, but the key has neither a stored value nor a declaration (case b)
        log.Warning("Scoped key '{Key}': unset and undeclared on the target; returning caller fallback", scopedKey);
        return fallback;
      }

      // stored value, or a declared default (case a) — both legitimate, silent
      return storage.Get(bareKey, fallback);
    }

    /// <summary>
    ///   Write through the scoped key. A missing target context logs a warning and no-ops (never throws on a
    ///   miss; malformed keys still throw). Declared-type enforcement is <see cref="VariableStorage" />'s.
    /// </summary>
    public void Set<T>(string scopedKey, IGraphContext local, T value)
    {
      if (!TryResolveStorage(scopedKey, local, out var storage, out var bareKey))
      {
        log.Warning("Scoped key '{Key}': no matching context; Set ignored", scopedKey);
        return;
      }

      storage.Set(bareKey, value);
    }
  }
}
