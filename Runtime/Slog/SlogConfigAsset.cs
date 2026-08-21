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

    [Tooltip("Minimum level written to the Unity Console sink. The file sink keeps everything at LogLevel; the console is the expensive sink (per-event format + editor console entry + dispatch chain), so gating it to Warning removes Info/Debug chatter cost without losing any data from the file log.")]
    [SerializeField]
    private LogEventLevel unityConsoleMinimumLevel = LogEventLevel.Warning;

    [FormerlySerializedAs("loggingToFile")]
    [SerializeField]
    private bool isLoggingToFile;

    [Tooltip("PLAYER builds only: strip Unity script stack traces from Log and Warning console emits. Every player console emit otherwise extracts + serializes a script stack trace (profiled 20-215 ms per hitch frame; Player.log becomes majority trace blocks). Error/Exception/Assert always keep traces, and the Serilog file sink is unaffected. Editor behavior never changes.")]
    [SerializeField]
    private bool stripPlayerLogStackTraces = true;

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
    public LogEventLevel UnityConsoleMinimumLevel => unityConsoleMinimumLevel;
    public bool StripPlayerLogStackTraces => stripPlayerLogStackTraces;
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