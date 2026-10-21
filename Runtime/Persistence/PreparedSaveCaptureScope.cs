using System;
using System.Threading;

namespace MToolKit.Runtime.Persistence
{
  /// <summary>Coordinator-owned prepared capture retained until the original profile is restored.</summary>
  public sealed class PreparedSaveCaptureScope : IDisposable
  {
    private readonly object gate;
    private Action release;
    private bool disposed;
    private bool writing;
    internal SaveSystemCoordinator Owner { get; }
    internal long SessionGeneration { get; }
    internal object Operation { get; }

    internal PreparedSaveCaptureScope(SaveSystemCoordinator owner, long sessionGeneration,
      object operation, object gate, Action release)
    {
      Owner = owner;
      SessionGeneration = sessionGeneration;
      Operation = operation;
      this.gate = gate;
      this.release = release;
    }

    internal void BeginWrite()
    {
      lock (gate)
      {
        if (disposed) throw new ObjectDisposedException(nameof(PreparedSaveCaptureScope));
        if (writing) throw new InvalidOperationException("Busy: the prepared scope already has a save in progress.");
        writing = true;
      }
    }

    internal void EndWrite()
    {
      bool finish;
      lock (gate) { writing = false; finish = disposed; }
      if (finish) Release();
    }

    /// <summary>Releases pinned controller snapshots, admission leases, and persistence ownership.</summary>
    public void Dispose()
    {
      bool finish;
      lock (gate)
      {
        if (disposed) return;
        disposed = true;
        finish = !writing;
      }
      if (finish) Release();
    }

    private void Release() => Interlocked.Exchange(ref release, null)?.Invoke();
  }
}
