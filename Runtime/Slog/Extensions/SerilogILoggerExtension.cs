using System.Runtime.CompilerServices;
using UnityEngine;

namespace Serilog
{
  public static class SerilogILoggerExtension
  {
    public static ILogger ForFeature(this ILogger log, string featureName)
    {
      return log.ForContext(new FeatureEnricher(featureName));
    }

    public static ILogger ForGameObject(this ILogger log, Object obj)
    {
      return log.ForContext(new UnityObjectEnricher(obj));
    }

    public static ILogger ForMethod(this ILogger log, [CallerMemberName] string methodName = null)
    {
      return log.ForContext(new MethodEnricher(methodName));
    }
  }
}