using Serilog.Core;
using Serilog.Events;

namespace Serilog
{
//https://stackoverflow.com/questions/65396561/serilog-access-original-object-in-logevent-that-was-passed-to-logging-statement
  public class ScalarValueEnricher : ILogEventEnricher
  {
    private readonly LogEventProperty prop;

    public ScalarValueEnricher(string name, object value)
    {
      prop = new LogEventProperty(name, new ScalarValue(value));
    }

    #region ILogEventEnricher Members

    public void Enrich(LogEvent evt, ILogEventPropertyFactory _)
    {
      evt.AddPropertyIfAbsent(prop);
    }

    #endregion
  }
}