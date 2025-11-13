namespace Serilog
{
  public class FeatureEnricher : ScalarValueEnricher
  {
    private const string FEATURE_PROPERTY_NAME = "feature";

    public FeatureEnricher(string featureName) : base(FEATURE_PROPERTY_NAME, featureName) { }
  }
}