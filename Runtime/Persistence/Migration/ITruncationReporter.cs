using System;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Per-load accumulator for <see cref="TruncationEntry"/> rows emitted by concrete
  ///   migrators during best-effort newer-save loads. The save coordinator opens a load
  ///   scope, awaits parallel domain loads (each of which may report entries
  ///   concurrently), then drains the accumulated report and publishes the
  ///   <see cref="SaveTruncatedOnLoadMessage"/> outcome.
  ///   Implementations must be thread-safe — multiple controllers' migrators report
  ///   concurrently via <see cref="ES3Integration.ES3GameSaveSystem"/>'s
  ///   <c>UniTask.WhenAll</c> dispatch.
  /// </summary>
  public interface ITruncationReporter
  {
    /// <summary>
    ///   Reset the accumulator before a new load. Idempotent. Concurrency-safe (e.g. atomic
    ///   queue replacement); callers do not need their own lock.
    /// </summary>
    void BeginLoadScope();

    /// <summary>
    ///   Append an entry. Thread-safe. Throws <see cref="ArgumentNullException"/> when
    ///   <paramref name="entry"/> is null — a null entry is a programmer error and severity
    ///   classification only null-tolerates <see cref="TruncationEntry.Reason"/>, not the
    ///   entry itself.
    /// </summary>
    void Report(TruncationEntry entry);

    /// <summary>
    ///   Snapshot the accumulated entries, compute their severity, and empty the queue.
    ///   Returns <see cref="TruncationReport.Empty"/> when nothing was reported in the
    ///   current load scope.
    /// </summary>
    TruncationReport DrainReport();
  }
}
