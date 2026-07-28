using System;
using Serilog.Events;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Marker type selecting the game-scoped broker's static state. Never instantiated — it exists only to
  ///   close <see cref="MessageBrokerCore{TScope}" />, whose statics are distinct per closed generic type.
  /// </summary>
  internal sealed class GameScope
  {
  }

  /// <summary>Marker type selecting the global-scoped broker's static state. See <see cref="GameScope" />.</summary>
  internal sealed class GlobalScope
  {
  }

  /// <summary>
  ///   Per-scope display metadata, resolved once into a <c>static readonly</c> by the core.
  ///
  ///   <para>Deliberately an immutable lookup rather than mutable statics assigned from each façade's static
  ///   constructor: any code touching <see cref="MessageBrokerCore{TScope}" /> directly runs the core's type
  ///   initializer WITHOUT the façade's, so it would observe fallback values; and once the core's one-shot
  ///   <c>Lazy&lt;ILogger&gt;</c> evaluated, a later write could no longer change it, letting the logger's
  ///   source context and the broker name in message text diverge permanently.</para>
  /// </summary>
  internal static class BrokerScopeInfo
  {
    /// <summary>
    ///   (display name, Serilog source context, log level for a redundant Initialize). The double-init level
    ///   differs per broker and is preserved verbatim from the pre-refactor implementations — Global logged
    ///   Verbose, Game logged Warning. Unifying them would be a behaviour change smuggled into a refactor.
    /// </summary>
    internal static (string Name, Type LogContext, LogEventLevel DoubleInit) For(Type scope)
    {
      if (scope == typeof(GameScope))
        return (nameof(GameMessageBroker), typeof(GameMessageBroker), LogEventLevel.Warning);
      if (scope == typeof(GlobalScope))
        return (nameof(GlobalAsyncMessageBroker), typeof(GlobalAsyncMessageBroker), LogEventLevel.Verbose);

      throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown broker scope.");
    }
  }
}
