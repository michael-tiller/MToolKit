using System;

namespace MToolKit.Runtime.Core.Interfaces
{
  /// <summary>
  ///   Abstraction for capturing named, nestable timing scopes during game startup.
  ///   Concrete implementations live in consuming projects; MToolKit code only calls
  ///   into this via the <see cref="MToolKit.Runtime.Core.StartupProfiling"/> façade.
  /// </summary>
  public interface IStartupProfiler
  {
    /// <summary>
    ///   Open a timing scope with the given name. The returned IDisposable must be
    ///   disposed to close the scope. Safe to call when no run is active: the returned
    ///   disposable is a no-op in that case.
    /// </summary>
    IDisposable Phase(string name);
  }
}
