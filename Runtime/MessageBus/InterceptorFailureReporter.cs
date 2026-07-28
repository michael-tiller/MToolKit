using System;
using System.Threading;
using MToolKit.Runtime.ErrorSystem.Messages;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Surfaces a throwing interceptor to the player through <c>ErrorSystem</c> rather than burying it in a
  ///   log file. <c>ErrorSystemPlugin</c> subscribes <see cref="ErrorRequestMessage" /> and drives
  ///   <c>IErrorService.ShowError</c>, which <c>ErrorPanel</c> renders.
  ///
  ///   <para>Publishing from inside a catch block is hazardous, so containment is explicit. Deliberately ONE
  ///   non-generic type rather than a field on <see cref="MessageBrokerCore{TScope}" />: per-scope statics
  ///   would put the recursion guard on the opposite side of the very game-to-global cycle it exists to
  ///   stop.</para>
  ///
  ///   <para>Recursion has two independent stops, neither relying on the publish reentrancy guard (which is
  ///   per-closed-core and therefore blind across scopes): the registry suppresses reporting when the
  ///   intercepted type IS <see cref="ErrorRequestMessage" /> (checked at the catch site, not here — a check
  ///   inside a substitutable delegate is not a check), and the depth guard below stops indirect and
  ///   cross-scope cycles.</para>
  ///
  ///   <para><b>Containment limit:</b> the depth guard is <c>[ThreadStatic]</c>, so a cycle in which a
  ///   MessagePipe subscriber marshals a publish onto another thread escapes it. Consuming projects own their
  ///   subscribers, so this is a documented limit of the contract. The type suppression is thread-independent
  ///   and still closes the direct cycle.</para>
  /// </summary>
  internal static class InterceptorFailureReporter
  {
    private static readonly Lazy<ILogger> logLazy =
      new(() => Log.Logger.ForContext(typeof(InterceptorFailureReporter)).ForFeature("MessageBus"));

    private static ILogger log => logLazy.Value ?? Logger.None;

    [ThreadStatic] private static bool reporting;

#if UNITY_INCLUDE_TESTS
    /// <summary>
    ///   Test-only capture hook. Never exists in a player build. Replaces the ErrorSystem publish, NOT the
    ///   log line, so tests still produce the same log output production does.
    /// </summary>
    internal static Action<string, Type, Exception> Override;

    /// <summary>
    ///   Installs <paramref name="handler" /> for the life of the returned token.
    ///   Acquisition and release are compare-and-swap, not check-then-set: a plain null check followed by an
    ///   assignment lets two parallel fixtures each believe they hold an exclusive scope and then clear each
    ///   other's delegate.
    /// </summary>
    internal static IDisposable OverrideScope(Action<string, Type, Exception> handler)
    {
      if (handler == null) throw new ArgumentNullException(nameof(handler));
      if (Interlocked.CompareExchange(ref Override, handler, null) != null)
        throw new InvalidOperationException("An interceptor-failure override scope is already active; scopes do not nest.");

      return new Scope(handler);
    }

    private sealed class Scope : IDisposable
    {
      private readonly Action<string, Type, Exception> owned;

      internal Scope(Action<string, Type, Exception> handler) => owned = handler;

      /// <summary>Clears only its own delegate, and only once — a stale restore would leak past the fixture.</summary>
      public void Dispose() => Interlocked.CompareExchange(ref Override, null, owned);
    }
#endif

    /// <summary>
    ///   Report a throwing interceptor. Never throws: callers invoke this from inside a catch block that is
    ///   protecting an in-flight dispatch chain.
    /// </summary>
    internal static void Report(string owner, Type messageType, Exception ex)
    {
      // Arm the guard BEFORE any logging. A custom Serilog sink can synchronously publish, which can fail
      // another interceptor and re-enter here before the guard would otherwise exist.
      if (reporting) return;
      reporting = true;

      string who = owner ?? "<unknown>";
      try
      {
        log.ForMethod().Error(ex, "[{Owner}] interceptor for {MessageType} threw", who, messageType?.Name);

#if UNITY_INCLUDE_TESTS
        // Read once — a second read can race a scope disposal to null and NRE, which the catch below would
        // swallow, silently losing both the override AND the real publish.
        Action<string, Type, Exception> handler = Volatile.Read(ref Override);
        if (handler != null)
        {
          handler(who, messageType, ex);
          return;
        }
#endif

        GlobalAsyncMessageBroker.Publish(new ErrorRequestMessage($"[{who}] interceptor error: {ex?.Message}", false));
      }
      catch
      {
        // ponytail: reporting must never break the chain it reports on.
      }
      finally
      {
        reporting = false;
      }
    }
  }
}
