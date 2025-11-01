
namespace Serilog
{
  public class MethodEnricher : ScalarValueEnricher
  {
    private const string MethodPropertyName = "method";

    public MethodEnricher(string methodName) : base(MethodPropertyName, methodName)
    {
    }
  }
}