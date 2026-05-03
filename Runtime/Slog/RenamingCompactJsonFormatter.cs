using System.Collections.Generic;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace Serilog
{
  public class RenamingCompactJsonFormatter : ITextFormatter
  {
    private readonly RenderedCompactJsonFormatter inner = new();
    private readonly IReadOnlyDictionary<string, string> renames;

    public RenamingCompactJsonFormatter(IReadOnlyDictionary<string, string> renames)
    {
      this.renames = renames;
    }

    #region ITextFormatter Members

    public void Format(LogEvent logEvent, TextWriter output)
    {
      LogEvent renamed = new(
        logEvent.Timestamp,
        logEvent.Level,
        logEvent.Exception,
        logEvent.MessageTemplate,
        Project(logEvent.Properties));
      inner.Format(renamed, output);
    }

    #endregion

    private IEnumerable<LogEventProperty> Project(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
      foreach (KeyValuePair<string, LogEventPropertyValue> kvp in properties)
      {
        string name = renames.TryGetValue(kvp.Key, out string mapped) ? mapped : kvp.Key;
        yield return new LogEventProperty(name, kvp.Value);
      }
    }
  }
}