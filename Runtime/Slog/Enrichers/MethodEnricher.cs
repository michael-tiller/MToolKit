namespace Serilog
{
  public class MethodEnricher : ScalarValueEnricher
  {
    private const string METHOD_PROPERTY_NAME = "method";

    public MethodEnricher(string methodName) : base(METHOD_PROPERTY_NAME, methodName) { }
  }
}