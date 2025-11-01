using System;
using UnityEngine;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.Core.Config;

namespace MToolKit.Runtime.Core.Singletons
{
  public class GlobalConfigLoader : Singleton<GlobalConfigLoader> 
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GlobalConfigLoader>().ForFeature("Installers"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    protected override bool dontDestroyOnLoad => true;
    [field: SerializeField] public GlobalPluginConfigAsset GlobalPluginConfig { get; private set; }
    [field: SerializeField] public PluginConfigAsset PluginConfig { get; private set; }
  }
}