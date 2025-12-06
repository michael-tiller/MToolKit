using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Unity3D;
using UnityEngine;
using UnityEngine.Analytics;
using Object = UnityEngine.Object;

// for compact json formatter

namespace MToolKit.Runtime.Slog
{
  public class SlogConfig : IDisposable
  {
    private const string CONFIG_FILE = "SlogConfig";
    private const string OVERRIDE_CONFIG_FILE = "OverrideSlogConfig";

    private static SlogConfigAsset configurationAsset;
    public static SlogConfigAsset ConfigurationAsset => configurationAsset;

    public SlogConfig()
    {
      // start serilog config
      LoggerConfiguration loggerConfig = new LoggerConfiguration().Enrich.FromGlobalLogContext();

      LoadSerilogConfiguration(CONFIG_FILE, OVERRIDE_CONFIG_FILE);
      ConfigMininumLevel(ref loggerConfig);
      ConfigLoggingToFile(ref loggerConfig);

      // Configure Unity3D sink with custom formatter
      string outputTemplate = "[{Level:u1}] [{SourceContext}.{method}]: {Message:lj}\n\r{Properties}";
      loggerConfig = loggerConfig.WriteTo.Unity3D(outputTemplate: outputTemplate);

      // Apply source context filtering if enabled
      if (configurationAsset.IsSourceContextFilteringEnabled) loggerConfig = loggerConfig.Filter.ByIncludingOnly(IsSourceContextAllowed);

      // Apply feature filtering if enabled
      if (configurationAsset.IsFeatureFilteringEnabled) loggerConfig = loggerConfig.Filter.ByIncludingOnly(IsFeatureAllowed);

      Log.Logger = loggerConfig.CreateLogger();

      // Get environment from environment variables (loaded by EnvironmentLoader)
      // EnvironmentLoader runs BeforeSceneLoad, so env vars should be available here
      string environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ??
                          Environment.GetEnvironmentVariable("MT_ENVIRONMENT") ??
                          "default";

      // Build version details - Application.version is the primary version
      // Additional build metadata can be added via custom build scripts if needed
      string buildVersion = Application.version;
#if UNITY_EDITOR
      string buildType = "Editor";
#else
      string buildType = "Release";
#endif

      Log.Logger = Log.Logger
        .ForContext("appName", Application.productName)
        .ForContext("deviceType", SystemInfo.deviceType)
        .ForContext("deviceID", SystemInfo.deviceUniqueIdentifier)
        .ForContext("appSessionID", AnalyticsSessionInfo.sessionId)
        .ForContext("appVersion", buildVersion)
        .ForContext("buildType", buildType)
        .ForContext("appPlatform", Application.platform)
        .ForContext("appLanguage", Application.systemLanguage)
        .ForContext("appSessionStart", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        .ForContext("environment", environment)
        ;
    }

    #region IDisposable Members

    public void Dispose()
    {
      Log.CloseAndFlush();
    }

    #endregion

    private static bool IsFeatureAllowed(LogEvent evt)
    {
      if (!configurationAsset.IsFeatureFilteringEnabled ||
          configurationAsset.AllowedFeatures == null ||
          configurationAsset.AllowedFeatures.Count == 0)
        return true;

      // Get the feature from the log event
      if (evt.Properties.TryGetValue("feature", out LogEventPropertyValue featureValue))
      {
        string feature = featureValue.ToString().Trim('"');
        // Check if any allowed feature is contained within the feature string
        return configurationAsset.AllowedFeatures.Any(allowedFeature =>
          feature.Contains(allowedFeature, StringComparison.OrdinalIgnoreCase));
      }

      return true;
    }

    private static bool IsSourceContextAllowed(LogEvent evt)
    {
      if (!configurationAsset.IsSourceContextFilteringEnabled ||
          configurationAsset.AllowedSourceContexts == null ||
          configurationAsset.AllowedSourceContexts.Count == 0)
        return true;

      // Get the source context from the log event
      if (evt.Properties.TryGetValue("SourceContext", out LogEventPropertyValue sourceContextValue))
      {
        string sourceContext = sourceContextValue.ToString().Trim('"');
        // Check if any allowed source context is contained within the source context string
        return configurationAsset.AllowedSourceContexts.Any(allowedContext =>
          sourceContext.Contains(allowedContext, StringComparison.OrdinalIgnoreCase));
      }

      // If no source context is found, allow the message (could be from Unity's internal logging)
      return true;
    }

    private static void ConfigLoggingToFile(ref LoggerConfiguration loggerConfig)
    {
      if (!configurationAsset.IsLoggingToFile) return;

      // Ensure logs directory exists
      string logsDir = Application.persistentDataPath + configurationAsset.LogPath;
      if (!string.IsNullOrEmpty(logsDir) && !Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

      loggerConfig = loggerConfig.WriteTo.File(
        new RenderedCompactJsonFormatter(),
        Application.persistentDataPath + configurationAsset.LogPath + configurationAsset.LogFileName,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
        rollOnFileSizeLimit: true,
        shared: false, // Don't share file handles
        flushToDiskInterval: TimeSpan.FromSeconds(1)
        );
    }

    private static void ConfigMininumLevel(ref LoggerConfiguration loggerConfig)
    {
      switch (configurationAsset.LogLevel)
      {
        case LogEventLevel.Verbose:
          loggerConfig = loggerConfig.MinimumLevel.Verbose();
          return;
        case LogEventLevel.Debug:
          loggerConfig = loggerConfig.MinimumLevel.Debug();
          return;
        case LogEventLevel.Information:
          loggerConfig = loggerConfig.MinimumLevel.Information();
          return;
        case LogEventLevel.Warning:
          loggerConfig = loggerConfig.MinimumLevel.Warning();
          return;
        case LogEventLevel.Error:
          loggerConfig = loggerConfig.MinimumLevel.Error();
          return;
        case LogEventLevel.Fatal:
          loggerConfig = loggerConfig.MinimumLevel.Fatal();
          return;
      }

      Debug.LogWarning($"SerilogConfigurationAsset.LogLevel {configurationAsset.LogLevel} was not handled.");
    }

    public static bool DoesResourceAssetExist(string filename, string ext = ".asset", string dir = "/Resources/")
    {
      Object obj = Resources.Load<Object>(filename);
      return obj != null;
    }

    public static void InitWithoutAsset(ref SlogConfigAsset config)
    {
      config = configurationAsset = ScriptableObject.CreateInstance<SlogConfigAsset>();
      configurationAsset.Setup();
    }

    public static void LoadFromAsset(ref SlogConfigAsset config, string path)
    {
#if UNITY_EDITOR
      // In Editor, try async loading first for better performance
      try
      {
        ResourceRequest request = Resources.LoadAsync<SlogConfigAsset>(path);
        if (request.isDone)
          config = (SlogConfigAsset)request.asset;
        else
          // Fallback to synchronous loading if async isn't immediately available
          config = Resources.Load<SlogConfigAsset>(path);
      }
      catch (Exception ex)
      {
        Debug.LogError($"Failed to load asset asynchronously from [{path}]: {ex.Message}");
        // Fallback to synchronous loading
        config = Resources.Load<SlogConfigAsset>(path);
      }
#else
      // In builds, simply use synchronous loading
      config = Resources.Load<SlogConfigAsset>(path);
#endif

      // Immediate validation - throw error if config is null
      if (config == null)
      {
        string errorMessage = $"Failed to load SlogConfigAsset from path [{path}]. Asset is null.";
        Debug.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
      }
    }

    public static async Task<SlogConfigAsset> LoadFromAssetAsync(string path)
    {
      ResourceRequest request = Resources.LoadAsync<SlogConfigAsset>(path);
      await request;
      SlogConfigAsset config = (SlogConfigAsset)request.asset;

      // Immediate validation - throw error if config is null
      if (config == null)
      {
        string errorMessage = $"Failed to load SlogConfigAsset asynchronously from path [{path}]. Asset is null.";
        Debug.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
      }

      return config;
    }

    public static SlogConfigAsset LoadSerilogConfiguration(string path, string overridePath = "")
    {
      if (configurationAsset == null)
      {
        //Debug.LogFormat($"starting LoadSerilogConfiguration with [{path}.asset].");

        bool overrideExists = false;
        if (!string.IsNullOrEmpty(overridePath))
          overrideExists = DoesResourceAssetExist(overridePath);
        string finalPath;
        if (overrideExists)
        {
          Debug.LogFormat($"Override path found. Attempting to use [{overridePath}.asset].");
          finalPath = overridePath;
        }
        else
        {
          //Debug.LogFormat($"No override path found. Attempting to use {path}.asset.");
          finalPath = path;
        }

        if (DoesResourceAssetExist(finalPath))
        {
          try
          {
            LoadFromAsset(ref configurationAsset, finalPath);
            // configurationAsset is now validated and not null due to LoadFromAsset's error handling
            Debug.LogFormat($"Serilog successfully loaded from [{finalPath}.asset].  Level: [{configurationAsset.LogLevel}] ToFile: [{configurationAsset.IsLoggingToFile}]");
          }
          catch (InvalidOperationException ex)
          {
            Debug.LogError($"Failed to load Serilog configuration from [{finalPath}]: {ex.Message}");
            // Create default configuration as fallback
            InitWithoutAsset(ref configurationAsset);
            Debug.LogWarning("Using default Serilog configuration due to load failure.");
          }
        }
        else
        {
          Debug.LogErrorFormat("No serilog config asset was found in resources at path [{0}].", finalPath);
          // Create default configuration as fallback
          InitWithoutAsset(ref configurationAsset);
          Debug.LogWarning("Using default Serilog configuration due to missing asset.");
        }
      }

      return configurationAsset;
    }
  }
}