using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Serilog;
using UnityEngine;
using VContainer;
using MToolKit.Runtime.Core.Interfaces;
using MessagePipe;
using Sirenix.OdinInspector;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Analytics.Events;
using MToolKit.Runtime.Analytics.Interfaces;
using MToolKit.Runtime.Analytics.Config;


namespace MToolKit.Runtime.Analytics
{

/// <summary>
/// Plugin for analytics services.
/// </summary>
public sealed class AnalyticsPlugin : AbstractGamePlugin, IDependencyDeclaration, IRuntimePlugin
{
    [SerializeField, Required] private AnalyticsConfig config;

    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AnalyticsPlugin>().ForFeature("Analytics"));
    private new static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private IObjectResolver resolver;
    private CancellationTokenSource cts;
    private bool runtimeInitialized;

    public IEnumerable<Type> RequiredServices => new[] { typeof(IAnalyticsService), typeof(ISettingsSystem) };
    public IEnumerable<Type> OptionalServices => Array.Empty<Type>();


    private IAnalyticsService analyticsService;

    [ShowInInspector, ReadOnly]
    private AnalyticsService AnalyticsService => analyticsService as AnalyticsService;

    public override void Register(IContainerBuilder builder)
    {
        log.ForMethod(nameof(Register)).Debug("AnalyticsPlugin Register called");
        
        // Register the config
        builder.RegisterInstance(config).As<AnalyticsConfig>();
        
        // Bind backend and façade here; swap backend later without touching callers.
        builder.Register<IAnalyticsBackend, GameAnalyticsBackend>(Lifetime.Singleton);
        builder.Register<IAnalyticsService, AnalyticsService>(Lifetime.Singleton);
        
        // Register the event bridge as a singleton service
        builder.Register<AnalyticsEventBridge>(Lifetime.Singleton);
        
        log.ForMethod(nameof(Register)).Information("AnalyticsPlugin Register completed - IAnalyticsService registered");
    }

    public void PerformSetup(IObjectResolver objectResolver)
    {
        resolver = objectResolver;
        cts = new CancellationTokenSource();
    }

    public void PerformRuntimeInitialization(IObjectResolver objectResolver)
    {
        log.ForMethod(nameof(PerformRuntimeInitialization)).Information("AnalyticsPlugin PerformRuntimeInitialization called, EnableOnStartup: {0}, RuntimeInitialized: {1}", config.EnableOnStartup, runtimeInitialized);
        if (!config.EnableOnStartup || runtimeInitialized) return;

        runtimeInitialized = true;

        // Ensure cts is initialized
        if (cts == null)
        {
            cts = new CancellationTokenSource();
        }

        InitializeAsync(cts.Token).Forget();
    }

    public void Initialize(IObjectResolver resolver)
    {
        log.ForMethod(nameof(Initialize)).Information("AnalyticsPlugin Initialize called");
        
        // Ensure resolver is set
        if (this.resolver == null)
        {
            this.resolver = resolver;
        }
        
        PerformRuntimeInitialization(resolver);
    }

    public bool AreDependenciesReady(IObjectResolver resolver)
    {
        return resolver.TryResolve<IAnalyticsService>(out _);
    }

    private async UniTaskVoid InitializeAsync(CancellationToken ct)
    {
        log.ForMethod(nameof(InitializeAsync)).Information("AnalyticsPlugin InitializeAsync started");
        analyticsService = resolver.Resolve<IAnalyticsService>() as AnalyticsService;
        var settingsSystem = resolver.Resolve<ISettingsSystem>();
        var gameSettings = settingsSystem.GameSettings;

        // Check both config enablement and user consent
        if (!config.EnableGameAnalytics)
        {
            log.ForMethod(nameof(InitializeAsync)).Information("Analytics disabled by configuration");
            return;
        }

        // Set initial consent based on user setting
        var userConsent = gameSettings.AnalyticsEnabled.Value;
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
        log.ForMethod(nameof(InitializeAsync)).Information("Analytics runtime initialized");
    }

    private void OnDestroy()
    {
        try 
        { 
            cts?.Cancel(); 
            
            // End the session when the plugin is destroyed
            if (runtimeInitialized && resolver != null)
            {
                GameAnalyticsSDK.GameAnalytics.EndSession();
                log.ForMethod(nameof(OnDestroy)).Information("Analytics session ended on plugin destruction");
            }
        } 
        catch (Exception ex)
        {
            log.ForMethod(nameof(OnDestroy)).Warning(ex, "Failed to end analytics session on destruction");
        }
        cts?.Dispose();
    }
}

}