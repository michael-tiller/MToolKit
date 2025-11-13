using System;
using MToolKit.Runtime.Core.Config;
using MToolKit.Runtime.Utilities;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
using Object = UnityEngine.Object;

namespace MToolKit.Runtime.Core.Singletons
{
  public class GlobalConstants : Singleton<GlobalConstants>
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GlobalConstants>().ForFeature("Core"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    protected override bool dontDestroyOnLoad => true;
    protected override bool selfCreate => true;

    [field: SerializeField]
    public GlobalConstantsConfigAsset GlobalConstantsConfig { get; private set; }


    public bool IsInitialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnRuntimeMethodLoad()
    {
      log.ForMethod().Information("Creating {0} singleton", nameof(GlobalConstants));
      // Create the singleton directly to avoid the Instance property's temporary GameObject issue
      GameObject singletonObject = new($"[Singleton] {nameof(GlobalConstants)}");
      singletonObject.AddComponent<GlobalConstants>();
    }

    protected override void Awake()
    {
      base.Awake();

      LoadGlobalConstantsConfiguration();
      IsInitialized = true; // Set to true anyway to prevent infinite waiting
    }

    /// <summary>
    ///   Loads GlobalConstantsConfig with environment-specific override support
    ///   Similar to SlogConfig's override pattern
    /// </summary>
    private void LoadGlobalConstantsConfiguration()
    {
      const string baseConfigFile = "GlobalConstantsConfig";
      const string overrideConfigFile = "OverrideGlobalConstantsConfig";

      bool overrideExists = DoesResourceAssetExist(overrideConfigFile);
      string finalPath = overrideExists ? overrideConfigFile : baseConfigFile;

      if (overrideExists)
        log.ForMethod().Information("Override config found. Using [{0}]", overrideConfigFile);

      GlobalConstantsConfig = Resources.Load<GlobalConstantsConfigAsset>(finalPath);
      if (GlobalConstantsConfig == null)
      {
        log.ForMethod().Error("Failed to load GlobalConstantsConfig from [{0}]. Creating default config.", finalPath);
        CreateDefaultConfig();
      }
      else
      {
        log.ForMethod().Information("GlobalConstantsConfig loaded from [{0}]", finalPath);
      }
    }

    /// <summary>
    ///   Creates a default configuration when no config asset is found
    /// </summary>
    private void CreateDefaultConfig()
    {
      GlobalConstantsConfig = ScriptableObject.CreateInstance<GlobalConstantsConfigAsset>();
      log.ForMethod().Warning("Using default GlobalConstantsConfig values");
    }

    /// <summary>
    ///   Checks if a resource asset exists (similar to SlogConfig pattern)
    /// </summary>
    private static bool DoesResourceAssetExist(string filename)
    {
      Object obj = Resources.Load<Object>(filename);
      return obj != null;
    }
  }
}