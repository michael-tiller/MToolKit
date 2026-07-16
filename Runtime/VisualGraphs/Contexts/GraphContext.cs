using System;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Variables;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Contexts
{
  /// <summary>
  ///   Standard <see cref="IGraphContext" />: typed variable access via <see cref="VariableStorage" /> over
  ///   a caller-supplied state, plus outbound emission through an injected <see cref="IEventEmitter" />.
  /// </summary>
  /// <remarks>
  ///   This type NEVER wraps the supplied state in <c>DebuggableGraphState</c> — wrap policy belongs to the
  ///   state's creator (the registry wraps the Player/World states it creates; graph-scope callers pass an
  ///   already-wrapped live state). Double-wrapping would double-emit debug events.
  /// </remarks>
  public sealed class GraphContext : IGraphContext
  {
    // Non-cached logger (NOT the house Lazy<ILogger>): this type only logs on the cold Emit(null) error
    // path, and resolving Log.Logger at log time is what lets tests assert the error via a swapped sink.
    private static ILogger log => Log.Logger.ForContext<GraphContext>().ForFeature("VisualGraphs.Contexts") ?? Logger.None;

    private readonly IEventEmitter emitter;

    /// <summary>
    ///   The underlying state. Exposed for the registry (re-create detection) and the future persistence
    ///   seam — NOT on <see cref="IGraphContext" />; variable access goes through <see cref="Variables" />.
    /// </summary>
    public IGraphState State { get; }

    public EGraphContextScope Scope { get; }
    public string OwnerId { get; }
    public IVariableStorage Variables { get; }

    public GraphContext(EGraphContextScope scope, string ownerId, IGraphState state,
      IEventEmitter emitter, GraphVariableSet declarations = null)
    {
      if (ownerId == null) throw new ArgumentNullException(nameof(ownerId));
      if (string.IsNullOrWhiteSpace(ownerId))
        throw new ArgumentException("Owner id must be non-empty.", nameof(ownerId));
      State = state ?? throw new ArgumentNullException(nameof(state));
      this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));

      Scope = scope;
      OwnerId = ownerId;
      Variables = new VariableStorage(state, declarations);
    }

    public void Emit(IGameMessage message, string domain = null)
    {
      if (message == null)
      {
        log.Error("Emit called with a null message on {Scope} context '{Owner}'; ignoring", Scope, OwnerId);
        return;
      }

      emitter.Emit(message, domain);
    }
  }
}
