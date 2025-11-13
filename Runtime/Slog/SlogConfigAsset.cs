using System.Collections.Generic;
using Serilog.Events;
using UnityEngine;
using UnityEngine.Serialization;

namespace MToolKit.Runtime.Slog
{
  [CreateAssetMenu(fileName = "SlogConfig", menuName = "_MTools/Slog/Configuration Asset", order = 1)]
  public class SlogConfigAsset : ScriptableObject
  {
    [SerializeField]
    private string logPath = "/logs/";

    [SerializeField]
    private string logFileName = "slog.jsonl";

    [SerializeField]
    private LogEventLevel logLevel = LogEventLevel.Debug;

    [FormerlySerializedAs("loggingToFile")]
    [SerializeField]
    private bool isLoggingToFile;

    [Header("Source Context Filtering")]
    [FormerlySerializedAs("enableSourceContextFiltering")]
    [Tooltip("If enabled, only log messages from these source contexts will be output")]
    [SerializeField]
    private bool isSourceContextFilteringEnabled;

    [Tooltip("List of allowed source contexts. Leave empty to allow all contexts when filtering is enabled")]
    [SerializeField]
    private List<string> allowedSourceContexts;

    [Header("Feature Filtering")]
    [FormerlySerializedAs("enableFeatureFiltering")]
    [Tooltip("If enabled, only log messages from these features will be output")]
    [SerializeField]
    private bool isFeatureFilteringEnabled;

    [Tooltip("List of allowed features. Leave empty to allow all features when filtering is enabled")]
    [SerializeField]
    private List<string> allowedFeatures;


    public string LogPath => logPath;
    public string LogFileName => logFileName;
    public LogEventLevel LogLevel => logLevel;
    public bool IsLoggingToFile => isLoggingToFile;
    public bool IsSourceContextFilteringEnabled => isSourceContextFilteringEnabled;
    public List<string> AllowedSourceContexts => allowedSourceContexts;
    public bool IsFeatureFilteringEnabled => isFeatureFilteringEnabled;
    public List<string> AllowedFeatures => allowedFeatures;

    public void Setup(LogEventLevel level = LogEventLevel.Debug, bool useFile = false, bool sourceFiltering = false, List<string> sourceContexts = null,
      bool featuresFiltering = false, List<string> features = null)
    {
      logLevel = level;
      isLoggingToFile = useFile;
      isSourceContextFilteringEnabled = sourceFiltering;
      allowedSourceContexts = sourceContexts ?? new List<string>();
      isFeatureFilteringEnabled = featuresFiltering;
      allowedFeatures = features ?? new List<string>();
    }
  }
}