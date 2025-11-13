using System;
using MToolKit.Runtime.Core.Config;
using MToolKit.Runtime.Utilities;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Core.Singletons
{
  public class GlobalConfigLoader : Singleton<GlobalConfigLoader>
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GlobalConfigLoader>().ForFeature("Installers"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    protected override bool dontDestroyOnLoad => true;

    [field: SerializeField]
    public GlobalPluginConfigAsset GlobalPluginConfig { get; private set; }

    [field: SerializeField]
    public PluginConfigAsset PluginConfig { get; private set; }
  }
}