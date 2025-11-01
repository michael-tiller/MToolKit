
namespace Serilog
{
  public class FeatureEnricher : ScalarValueEnricher
  {
    private const string FeaturePropertyName = "feature";

    public FeatureEnricher(string featureName) : base(FeaturePropertyName, featureName)
    {
    }
  }
}