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
