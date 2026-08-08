using System;
using System.Diagnostics;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Serilog
{
  // Fills in `method` by walking the call stack when no explicit ForMethod() was set.
  // Cheap when `method` is already present (early return). Skipped frames: Serilog
  // pipeline and MToolKit.Slog enrichers, so the first user frame wins.
  public class MethodFromStackEnricher : ILogEventEnricher
  {
    private const string METHOD_PROPERTY = "method";

    #region ILogEventEnricher Members

    public void Enrich(LogEvent evt, ILogEventPropertyFactory propertyFactory)
    {
      if (evt.Properties.ContainsKey(METHOD_PROPERTY))
        return;

      // Stack walking costs ~0.05-0.1ms + ~70B/frame walked PER EVENT — under a Debug-level log
      // burst (hundreds of events/frame) this enricher alone profiled as a frame hitch
      // (dirigible2D 2026-08-08). Below Warning, callers who want `method` set it via ForMethod();
      // high-volume Debug/Verbose chatter goes without rather than paying the walk.
      if (evt.Level < LogEventLevel.Warning)
        return;

      StackTrace trace = new(fNeedFileInfo: false);
      int frameCount = trace.FrameCount;
      for (int i = 0; i < frameCount; i++)
      {
        StackFrame frame = trace.GetFrame(i);
        MethodBase method = frame?.GetMethod();
        Type declaringType = method?.DeclaringType;
        if (declaringType == null)
          continue;

        string ns = declaringType.Namespace;
        if (ns != null && (ns.StartsWith("Serilog", StringComparison.Ordinal) ||
                           ns.StartsWith("MToolKit.Runtime.Slog", StringComparison.Ordinal)))
          continue;

        evt.AddPropertyIfAbsent(propertyFactory.CreateProperty(METHOD_PROPERTY, UnwrapCompilerName(method.Name)));
        return;
      }
    }

    #endregion

    // Lambdas / async state machines surface as <CallerName>b__0 / MoveNext.
    // Pull the original caller name out of the angle brackets when present.
    private static string UnwrapCompilerName(string name)
    {
      if (string.IsNullOrEmpty(name) || name[0] != '<')
        return name;
      int gt = name.IndexOf('>');
      return gt > 1 ? name.Substring(1, gt - 1) : name;
    }
  }
}
