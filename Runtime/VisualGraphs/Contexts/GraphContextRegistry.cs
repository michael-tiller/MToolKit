using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Contexts
{
  /// <summary>
  ///   Owns runtime <see cref="IGraphContext" />s. Player and World are lazily-created singleton contexts
  ///   backed by their own state; Graph contexts are keyed by owner id (a quest/graph id).
  /// </summary>
  /// <remarks>
  ///   <para>Single class, no factory split (per the phase design constraints). Main-thread only — a plain
  ///   <see cref="Dictionary{TKey,TValue}" />, no locks.</para>
  ///   <para><b>9.0.2b contract:</b> the QuestManager refactor must register Graph contexts with
  ///   <c>ownerId = questGuid</c> exactly — the verbatim id that <see cref="ScopedKeyResolver" /> parses out
  ///   of a <c>quest:&lt;id&gt;.key</c> reference.</para>
  /// </remarks>
  public sealed class GraphContextRegistry
  {
    /// <summary>Reserved owner id for the singleton Player context.</summary>
    public const string PlayerOwnerId = "player";

    /// <summary>Reserved owner id for the singleton World context.</summary>
    public const string WorldOwnerId = "world";

    // Non-cached logger (NOT the house Lazy<ILogger>): cold warning/error paths only, and resolving
    // Log.Logger at log time is what lets tests assert warnings via a swapped sink.
    private static ILogger log => Log.Logger.ForContext<GraphContextRegistry>().ForFeature("VisualGraphs.Contexts") ?? Logger.None;

    private readonly IEventEmitter emitter;
    private readonly Dictionary<(EGraphContextScope scope, string ownerId), GraphContext> contexts = new();

    private GraphVariableSet playerDeclarations;
    private GraphVariableSet worldDeclarations;

    public GraphContextRegistry(IEventEmitter emitter)
    {
      this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary>
    ///   Get the existing context or create it. Player/World ignore <paramref name="ownerId" /> (normalized
    ///   to the reserved constant) and reject a supplied <paramref name="state" />/<paramref name="declarations" />
    ///   (use <see cref="SetScopeDeclarations" />). Graph requires a non-empty owner id and a state on first
    ///   create; a later call with a DIFFERENT non-null state throws (call <see cref="Remove" /> first).
    /// </summary>
    public IGraphContext GetOrCreate(EGraphContextScope scope, string ownerId,
      IGraphState state = null, GraphVariableSet declarations = null)
    {
      if (scope != EGraphContextScope.Graph)
        return GetOrCreateScopeSingleton(scope, ownerId, state, declarations);

      if (ownerId == null) throw new ArgumentNullException(nameof(ownerId));
      if (string.IsNullOrWhiteSpace(ownerId))
        throw new ArgumentException("Graph owner id must be non-empty.", nameof(ownerId));

      var key = (scope, ownerId);
      if (contexts.TryGetValue(key, out var existing))
      {
        if (state != null && !ReferenceEquals(state, existing.State))
          throw new InvalidOperationException(
            $"Graph context '{ownerId}' already exists with a different state. " +
            $"Call Remove('{ownerId}') before re-creating with a new state.");
        return existing; // null or same state → return existing; declarations arg ignored on this path
      }

      if (state == null)
        throw new ArgumentNullException(nameof(state), $"A state is required to create graph context '{ownerId}'.");

      var created = new GraphContext(scope, ownerId, state, emitter, declarations);
      contexts[key] = created;
      return created;
    }

    private IGraphContext GetOrCreateScopeSingleton(EGraphContextScope scope, string ownerId,
      IGraphState state, GraphVariableSet declarations)
    {
      if (state != null)
        throw new ArgumentException($"{scope} scope owns its own state; do not supply one.", nameof(state));
      if (declarations != null)
        throw new ArgumentException($"{scope} declarations are set via SetScopeDeclarations, not GetOrCreate.", nameof(declarations));

      var constant = scope == EGraphContextScope.Player ? PlayerOwnerId : WorldOwnerId;
      if (ownerId != null && !string.Equals(ownerId, constant, StringComparison.Ordinal))
        log.Warning("{Scope} scope ignores owner id '{Owner}'; normalizing to '{Constant}'", scope, ownerId, constant);

      var key = (scope, constant);
      if (contexts.TryGetValue(key, out var existing)) return existing;

      var scopeDeclarations = scope == EGraphContextScope.Player ? playerDeclarations : worldDeclarations;
      var backing = new DebuggableGraphState(new InMemoryGraphState(), constant);
      var created = new GraphContext(scope, constant, backing, emitter, scopeDeclarations);
      contexts[key] = created;
      return created;
    }

    /// <summary>
    ///   Pure lookup — never creates. Player/World normalize any owner id (null or foreign) to the reserved
    ///   constant silently (no warning — lookup stays noise-free), so after creation they resolve with a null
    ///   owner; before first creation they return false. Graph looks up the verbatim owner id.
    /// </summary>
    public bool TryGet(EGraphContextScope scope, string ownerId, out IGraphContext context)
    {
      if (contexts.TryGetValue(NormalizeKey(scope, ownerId), out var found))
      {
        context = found;
        return true;
      }

      context = null;
      return false;
    }

    /// <summary>
    ///   Remove a Graph context (quest teardown / reload — the legitimate re-create path). Passing a reserved
    ///   scope constant throws; an absent owner returns false.
    /// </summary>
    public bool Remove(string ownerId)
    {
      if (string.Equals(ownerId, PlayerOwnerId, StringComparison.Ordinal) ||
          string.Equals(ownerId, WorldOwnerId, StringComparison.Ordinal))
        throw new ArgumentException(
          $"'{ownerId}' is a reserved scope owner; Remove targets Graph contexts only.", nameof(ownerId));

      return contexts.Remove((EGraphContextScope.Graph, ownerId));
    }

    /// <summary>
    ///   Set the declared-variables block for the lazily-created Player or World context. Must be called
    ///   before that scope's first <see cref="GetOrCreate" />; afterwards it is logged and ignored (never
    ///   throws — load-order tolerant). Graph declarations arrive per-GetOrCreate, so Graph scope throws.
    /// </summary>
    public void SetScopeDeclarations(EGraphContextScope scope, GraphVariableSet declarations)
    {
      if (scope == EGraphContextScope.Graph)
        throw new ArgumentException("Graph declarations are supplied per-GetOrCreate.", nameof(scope));

      var constant = scope == EGraphContextScope.Player ? PlayerOwnerId : WorldOwnerId;
      if (contexts.ContainsKey((scope, constant)))
      {
        log.Error("{Scope} context already created; SetScopeDeclarations ignored. Call it before the first GetOrCreate.", scope);
        return;
      }

      if (scope == EGraphContextScope.Player) playerDeclarations = declarations;
      else worldDeclarations = declarations;
    }

    /// <summary>
    ///   Persistence seam (9.0.4): the backing state of the Player or World context, or null before it is
    ///   lazily created. Graph states are owned by their graphs, so Graph scope throws.
    /// </summary>
    public IGraphState GetScopeStateOrNull(EGraphContextScope scope)
    {
      if (scope == EGraphContextScope.Graph)
        throw new ArgumentException("Graph states are owned by their graphs, not the registry.", nameof(scope));

      var constant = scope == EGraphContextScope.Player ? PlayerOwnerId : WorldOwnerId;
      return contexts.TryGetValue((scope, constant), out var ctx) ? ctx.State : null;
    }

    /// <summary>
    ///   Persistence seam (9.0.4): restore saved key/values into the Player or World context's backing state,
    ///   forcing lazy creation if needed. Sanitizes against the scope's declarations FIRST (schema-change
    ///   behavior #4: a type-mismatched saved value is discarded loudly and the declared default overwrites
    ///   whatever the state holds). Declaration timing is pinned here: restore after
    ///   <see cref="SetScopeDeclarations" /> sanitizes against the schema; restore BEFORE it sanitizes against
    ///   nothing (all values load as undeclared — legal) and the late SetScopeDeclarations is then logged and
    ///   ignored as usual. Graph scope throws — graph snapshots restore through their runner's ImportState.
    /// </summary>
    public void RestoreScopeState(EGraphContextScope scope, IReadOnlyDictionary<string, object> data)
    {
      if (scope == EGraphContextScope.Graph)
        throw new ArgumentException("Graph states restore through their runner, not the registry.", nameof(scope));
      if (data == null) return;

      var context = GetOrCreateScopeSingleton(scope, null, null, null);
      var declarations = scope == EGraphContextScope.Player ? playerDeclarations : worldDeclarations;

      var sanitized = new Dictionary<string, object>(data);
      Persistence.GraphSnapshotSchemaSanitizer.SanitizeTypeMismatches(sanitized, declarations,
        scope == EGraphContextScope.Player ? PlayerOwnerId : WorldOwnerId);

      var state = ((GraphContext)context).State;
      foreach (var kv in sanitized)
        state.Set(kv.Key, kv.Value);
    }

    private static (EGraphContextScope, string) NormalizeKey(EGraphContextScope scope, string ownerId)
    {
      return scope switch
      {
        EGraphContextScope.Player => (scope, PlayerOwnerId),
        EGraphContextScope.World => (scope, WorldOwnerId),
        _ => (scope, ownerId)
      };
    }
  }
}
