using UnityEngine;
using System.Collections.Generic;
using Serilog.Events;

namespace MToolKit.Runtime.Slog
{
  [CreateAssetMenu(fileName = "SlogConfig", menuName = "_MTools/Slog/Configuration Asset", order = 1)]
  public class SlogConfigAsset : ScriptableObject
  {
    public string logPath = "/logs/";
    public string logFileName = "slog.jsonl";


    public LogEventLevel logLevel = LogEventLevel.Debug;
    public bool loggingToFile;

    [Header("Source Context Filtering")]
    [Tooltip("If enabled, only log messages from these source contexts will be output")]
    public bool enableSourceContextFiltering = false;

    [Tooltip("List of allowed source contexts. Leave empty to allow all contexts when filtering is enabled")]
    public List<string> allowedSourceContexts = default;

    [Header("Feature Filtering")]
    [Tooltip("If enabled, only log messages from these features will be output")]
    public bool enableFeatureFiltering = false;

    [Tooltip("List of allowed features. Leave empty to allow all features when filtering is enabled")]
    public List<string> allowedFeatures = default;

    public void Setup(LogEventLevel level = LogEventLevel.Debug, bool useFile = false, bool sourceFiltering = false, List<string> sourceContexts = null, bool featuresFiltering = false, List<string> features = null)
    {
      logLevel = level;
      loggingToFile = useFile;
      enableSourceContextFiltering = sourceFiltering;
      allowedSourceContexts = sourceContexts ?? new List<string>();
      enableFeatureFiltering = featuresFiltering;
      allowedFeatures = features ?? new List<string>();
    }
  }
}