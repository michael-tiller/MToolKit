using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameAnalyticsSDK;
using MToolKit.Runtime.Analytics.Config;
using MToolKit.Runtime.Analytics.Events;
using MToolKit.Runtime.Analytics.Interfaces;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;


namespace MToolKit.Runtime.Analytics
{
  /// <summary>
  ///   Plugin for analytics services.
  /// </summary>
  public sealed class AnalyticsPlugin : AbstractGamePlugin, IDependencyDeclaration, IRuntimePlugin
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AnalyticsPlugin>().ForFeature("Analytics"));
    private new static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private AnalyticsConfig config;


    private IAnalyticsService analyticsService;
    private CancellationTokenSource cts;

    private IObjectResolver resolver;
    private bool runtimeInitialized;

    [ShowInInspector]
    [ReadOnly]
    private AnalyticsService analyticsServiceValue => analyticsService as AnalyticsService;

    private void OnDestroy()
    {
      try
      {
        cts?.Cancel();

        // End the session when the plugin is destroyed
        if (runtimeInitialized && resolver != null)
        {
          GameAnalytics.EndSession();
          log.ForMethod().Information("Analytics session ended on plugin destruction");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Warning(ex, "Failed to end analytics session on destruction");
      }
      cts?.Dispose();
    }

    public IEnumerable<Type> RequiredServices => new[] { typeof(IAnalyticsService), typeof(ISettingsSystem) };
    public IEnumerable<Type> OptionalServices => Array.Empty<Type>();

    public override void Register(IContainerBuilder builder)
    {
      log.ForMethod().Verbose("AnalyticsPlugin Register called");

      // Register the config
      builder.RegisterInstance(config).As<AnalyticsConfig>();

      // Bind backend and façade here; swap backend later without touching callers.
      builder.Register<IAnalyticsBackend, GameAnalyticsBackend>(Lifetime.Singleton);
      builder.Register<IAnalyticsService, AnalyticsService>(Lifetime.Singleton);

      // Register the event bridge as a singleton service
      builder.Register<AnalyticsEventBridge>(Lifetime.Singleton);

      log.ForMethod().Verbose("AnalyticsPlugin Register completed - IAnalyticsService registered");
    }

    public void PerformSetup(IObjectResolver objectResolver)
    {
      resolver = objectResolver;
      cts = new CancellationTokenSource();
    }

    public void PerformRuntimeInitialization(IObjectResolver objectResolver)
    {
      log.ForMethod().Verbose("AnalyticsPlugin PerformRuntimeInitialization called, EnableOnStartup: {0}, RuntimeInitialized: {1}", config.EnableOnStartup, runtimeInitialized);
      if (!config.EnableOnStartup || runtimeInitialized) return;

      runtimeInitialized = true;

      // Ensure cts is initialized
      cts ??= new CancellationTokenSource();

      InitializeAsync(cts.Token).Forget();
    }

    public void Initialize(IObjectResolver resolver)
    {
      log.ForMethod().Verbose("AnalyticsPlugin Initialize called");

      // Ensure resolver is set
      this.resolver ??= resolver;

      PerformRuntimeInitialization(resolver);
    }

    public bool AreDependenciesReady(IObjectResolver resolver)
    {
      return this.resolver.TryResolve<IAnalyticsService>(out _);
    }

    private async UniTaskVoid InitializeAsync(CancellationToken ct)
    {
      log.ForMethod().Verbose("AnalyticsPlugin InitializeAsync started");
      analyticsService = resolver.Resolve<IAnalyticsService>() as AnalyticsService;
      ISettingsSystem settingsSystem = resolver.Resolve<ISettingsSystem>();
      GameSettingsModule gameSettings = settingsSystem.GameSettings;

      // Check both config enablement and user consent
      if (!config.EnableGameAnalytics)
      {
        log.ForMethod().Information("Analytics disabled by configuration");
        return;
      }

      if (analyticsService == null)
      {
        log.ForMethod().Warning("Analytics service was not found.");
        return;
      }

      // Set initial consent based on user setting
      bool userConsent = gameSettings.AnalyticsEnabled.Value;
      analyticsService.SetConsent(userConsent, userConsent);

      if (config.RequireConsentBeforeSending && !userConsent)
      {
        // Wait for user to grant consent via settings menu
        await gameSettings.AnalyticsEnabled.Property.Where(v => v).FirstAsync(ct);
        analyticsService.SetConsent(true, true);
      }

      await analyticsService.InitializeAsync(ct);
      await analyticsService.StartSessionAsync(ct);
      runtimeInitialized = true;
      log.ForMethod().Information("Analytics runtime initialized");
    }
  }
}