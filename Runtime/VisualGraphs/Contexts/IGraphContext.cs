using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Variables;

namespace MToolKit.Runtime.VisualGraphs.Contexts
{
  /// <summary>
  ///   The three runtime variable scopes. Quest contexts are <see cref="Graph" />-scoped contexts whose
  ///   owner is a quest — there is no separate quest scope (quest convenience is extension methods).
  /// </summary>
  public enum EGraphContextScope
  {
    Graph = 0,
    Player = 1,
    World = 2
  }

  /// <summary>
  ///   One flat runtime context over an <see cref="Runtime.Interfaces.IGraphState" />: a scope, an owner
  ///   id, typed variable access, and outbound event emission. There is deliberately no capability-split
  ///   hierarchy (no IQuestContext etc.) — scopes are data on this single interface, and cross-scope access
  ///   goes through <see cref="ScopedKeyResolver" />, not through context-to-context references.
  /// </summary>
  public interface IGraphContext
  {
    /// <summary>The scope this context represents.</summary>
    EGraphContextScope Scope { get; }

    /// <summary>
    ///   Owner identity: the quest/graph id for <see cref="EGraphContextScope.Graph" /> contexts; the
    ///   reserved constant (<c>"player"</c>/<c>"world"</c>) for the singleton scopes.
    /// </summary>
    string OwnerId { get; }

    /// <summary>Typed variable accessor over this context's state (with declared-default fallback).</summary>
    IVariableStorage Variables { get; }

    /// <summary>
    ///   Emit a MessagePipe message into the game (outbound: graph logic → bus). A null message logs an
    ///   error and no-ops. This is the bus-publish path, NOT the inbound graph-routing path — emitting must
    ///   not re-enter graph delivery synchronously.
    /// </summary>
    void Emit(IGameMessage message, string domain = null);
  }
}
