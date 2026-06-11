using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Temporarily swaps <see cref="Log.Logger" /> for a collecting sink so a test can assert that a SUT
  ///   actually emitted a Serilog warning/error (which Unity's <c>LogAssert</c> cannot see). Restores the
  ///   previous logger on <see cref="Dispose" />. Only works against SUTs that resolve <c>Log.Logger</c> at
  ///   log time (the Contexts classes use a non-cached logger property for exactly this reason); cached
  ///   <c>Lazy&lt;ILogger&gt;</c> loggers capture the root before the swap and won't be observed.
  /// </summary>
  public sealed class SerilogSinkScope : IDisposable
  {
    private sealed class CollectingSink : ILogEventSink
    {
      public List<LogEvent> Events { get; } = new();
      public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private readonly ILogger previous;
    private readonly CollectingSink sink = new();

    public SerilogSinkScope()
    {
      previous = Log.Logger;
      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .WriteTo.Sink(sink)
        .CreateLogger();
    }

    public IReadOnlyList<LogEvent> Events => sink.Events;

    public IEnumerable<LogEvent> Warnings => Events.Where(e => e.Level == LogEventLevel.Warning);

    public IEnumerable<LogEvent> Errors => Events.Where(e => e.Level == LogEventLevel.Error);

    /// <summary>True if any captured Warning's message TEMPLATE contains the substring (stable across property values).</summary>
    public bool ContainsWarning(string templateSubstring) =>
      Events.Any(e => e.Level == LogEventLevel.Warning &&
                      e.MessageTemplate.Text.Contains(templateSubstring, StringComparison.Ordinal));

    /// <summary>True if any captured Error's message TEMPLATE contains the substring.</summary>
    public bool ContainsError(string templateSubstring) =>
      Events.Any(e => e.Level == LogEventLevel.Error &&
                      e.MessageTemplate.Text.Contains(templateSubstring, StringComparison.Ordinal));

    public void Dispose()
    {
      (Log.Logger as IDisposable)?.Dispose();
      Log.Logger = previous;
    }
  }
}
