using Serilog.Core;
using Serilog.Events;

namespace Serilog
{
  public class RenamePropertyEnricher : ILogEventEnricher
  {
    private readonly string from;
    private readonly string to;

    public RenamePropertyEnricher(string from, string to)
    {
      this.from = from;
      this.to = to;
    }

    #region ILogEventEnricher Members

    public void Enrich(LogEvent evt, ILogEventPropertyFactory propertyFactory)
    {
      if (!evt.Properties.TryGetValue(from, out LogEventPropertyValue value))
        return;

      evt.AddOrUpdateProperty(new LogEventProperty(to, value));
      evt.RemovePropertyIfPresent(from);
    }

    #endregion
  }
}
