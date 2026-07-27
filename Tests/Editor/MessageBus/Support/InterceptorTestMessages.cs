using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Tests.Editor.MessageBus.Support
{
  /// <summary>Cancellable message. A CLASS — cancellation mutates, and a struct would be lost to boxing.</summary>
  public class CancellableCommand : ICancellableMessage
  {
    public bool IsCancelled { get; private set; }
    public string CancelReason { get; private set; }

    public void Cancel(string reason = null)
    {
      IsCancelled = true;
      CancelReason = reason;
    }
  }

  /// <summary>Second cancellable type, for cross-type and cross-scope cases.</summary>
  public class OtherCancellableCommand : ICancellableMessage
  {
    public bool IsCancelled { get; private set; }
    public string CancelReason { get; private set; }

    public void Cancel(string reason = null)
    {
      IsCancelled = true;
      CancelReason = reason;
    }
  }

  /// <summary>
  ///   Deliberately illegal: a value type implementing <see cref="ICancellableMessage" />. Exists purely to
  ///   prove both guards reject it — the registration-time typeof(T) check and the dispatch-time runtime
  ///   check that catches it hidden behind object/interface.
  /// </summary>
  public struct BadCancellableStruct : ICancellableMessage
  {
    public bool IsCancelled { get; private set; }
    public string CancelReason { get; private set; }

    public void Cancel(string reason = null)
    {
      IsCancelled = true;
      CancelReason = reason;
    }
  }

  /// <summary>Non-cancellable message, to prove interception works without the cancellation contract.</summary>
  public class PlainNotification
  {
    public int Value;
  }
}
