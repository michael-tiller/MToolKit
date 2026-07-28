namespace MToolKit.Runtime.MessageBus.Interfaces
{
  /// <summary>
  ///   Opt-in contract letting a pre-interceptor veto a message before it reaches the bus. A cancelled
  ///   message is not published; post-interceptors still run and can read <see cref="IsCancelled" />.
  ///
  ///   <para>Deliberately does NOT extend <c>IGameMessage</c>: the brokers' generics are unconstrained and
  ///   MessagePipe's <c>IPublisher&lt;T&gt;</c> imposes nothing, so requiring an unrelated marker would
  ///   exclude otherwise valid DTOs from opting into cancellation.</para>
  ///
  ///   <para><b>Implementors must be reference types.</b> Cancellation mutates the message; a struct
  ///   implementation would be boxed by the <c>is</c> check, mutated in the box, and re-boxed uncancelled on
  ///   the next read — cancellation would silently fail. This is enforced, not merely documented: registration
  ///   rejects a cancellable value type, and dispatch rejects one hidden behind <c>object</c> or an
  ///   interface.</para>
  ///
  ///   <para>Cancellation is sticky — <see cref="IsCancelled" /> lives on the message, so republishing the
  ///   same instance short-circuits. Treat cancellable messages as single-use.</para>
  /// </summary>
  public interface ICancellableMessage
  {
    /// <summary>True once any interceptor has cancelled this message.</summary>
    bool IsCancelled { get; }

    /// <summary>Why it was cancelled, if a reason was supplied.</summary>
    string CancelReason { get; }

    /// <summary>Veto the message. Stops remaining pre-interceptors and prevents publication.</summary>
    void Cancel(string reason = null);
  }
}
