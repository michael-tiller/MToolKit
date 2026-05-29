using System;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Runtime.Core
{
  /// <summary>
  ///   Static façade for startup timing. Consumers (including MToolKit internal code
  ///   like PluginRegistry) call <see cref="Phase"/> without needing the resolver.
  ///   The concrete profiler is installed by the game at bootstrap via <see cref="Current"/>.
  ///   Until installed, all calls are no-ops.
  /// </summary>
  public static class StartupProfiling
  {
    private static IStartupProfiler _current = NullStartupProfiler.Instance;

    public static IStartupProfiler Current
    {
      get => _current;
      set => _current = value ?? NullStartupProfiler.Instance;
    }

    public static IDisposable Phase(string name) => _current.Phase(name);
  }

  internal sealed class NullStartupProfiler : IStartupProfiler
  {
    public static readonly NullStartupProfiler Instance = new();
    private NullStartupProfiler() { }
    public IDisposable Phase(string name) => NoopScope.Instance;
  }

  internal sealed class NoopScope : IDisposable
  {
    public static readonly NoopScope Instance = new();
    private NoopScope() { }
    public void Dispose() { }
  }
}
