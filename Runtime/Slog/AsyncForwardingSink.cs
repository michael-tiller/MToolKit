using System;
using System.Collections.Concurrent;
using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace MToolKit.Runtime.Slog
{
  /// <summary>
  /// Forwards log events to an inner sink from a dedicated background thread so the emitting
  /// thread (usually Unity's main thread) never touches the disk. The queue is bounded and
  /// DROPS on overflow rather than blocking — a full queue means the disk cannot keep up, and
  /// stalling gameplay to preserve Debug lines is the wrong trade (the drop count is reported
  /// when it happens). Replaces the synchronous file write that profiled as main-thread
  /// "WaitForSignal" stalls under log bursts (2026-08-02).
  /// </summary>
  public sealed class AsyncForwardingSink : ILogEventSink, IDisposable
  {
    private const int QueueCapacity = 8192;

    private readonly ILogEventSink _inner;
    private readonly IDisposable _innerDisposable;
    private readonly BlockingCollection<LogEvent> _queue = new(new ConcurrentQueue<LogEvent>(), QueueCapacity);
    private readonly Thread _worker;
    private long _dropped;

    /// <param name="inner">Sink that performs the real writes; a <see cref="Serilog.Core.Logger"/> works
    /// (it implements <see cref="ILogEventSink"/>) and is disposed — flushing it — when this sink is.</param>
    public AsyncForwardingSink(ILogEventSink inner)
    {
      _inner = inner ?? throw new ArgumentNullException(nameof(inner));
      _innerDisposable = inner as IDisposable;
      _worker = new Thread(Pump) { IsBackground = true, Name = "SlogAsyncFileSink" };
      _worker.Start();
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
      if (_queue.IsAddingCompleted) return;
      if (!_queue.TryAdd(logEvent)) Interlocked.Increment(ref _dropped);
    }

    private void Pump()
    {
      foreach (LogEvent evt in _queue.GetConsumingEnumerable())
      {
        try { _inner.Emit(evt); }
        catch { /* a failed disk write must never kill the pump */ }
      }
    }

    /// <inheritdoc />
    public void Dispose()
    {
      _queue.CompleteAdding();
      // Bounded join: losing tail lines on a wedged disk beats hanging application quit.
      _worker.Join(TimeSpan.FromSeconds(2));
      long dropped = Interlocked.Read(ref _dropped);
      if (dropped > 0)
        UnityEngine.Debug.LogWarning($"[Slog] AsyncForwardingSink dropped {dropped} event(s) — file sink could not keep up.");
      _innerDisposable?.Dispose();
    }
  }
}
