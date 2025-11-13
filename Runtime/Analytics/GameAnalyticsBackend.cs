using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Analytics.Interfaces;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
#if GAMEANALYTICS_SDK
using GameAnalyticsSDK;

#endif

namespace MToolKit.Runtime.Analytics
{
  /// <summary>
  ///   Concrete implementation of IAnalyticsBackend for GameAnalytics.
  /// </summary>
  public sealed class GameAnalyticsBackend : IAnalyticsBackend
#if GAMEANALYTICS_SDK
                                             , IGameAnalyticsATTListener
#endif
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger?.ForContext<GameAnalyticsBackend>().ForFeature("Analytics"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private bool initialized;
    private static bool sessionStarted;

    public bool IsStarted => sessionStarted;

    public async UniTask InitializeAsync(CancellationToken ct)
    {
      if (initialized) return;

      // Skip initialization in editor to prevent issues
      if (Application.isEditor)
      {
        log.Information("GameAnalytics initialization skipped in editor");
        initialized = true;
        return;
      }

      await UniTask.Delay(1000, cancellationToken: ct);

#if GAMEANALYTICS_SDK
      try
      {
        // Set build version before initializing
        GameAnalytics.SetBuildAllPlatforms(Application.version);

        // Only initialize if not already initialized
        if (!GameAnalytics.Initialized)
          GameAnalytics.Initialize();
        // Don't call StartSession manually - let GameAnalytics handle it automatically
        // Optional: ATT flow on iOS handled via listener
#if UNITY_IOS
            GameAnalytics.RequestTrackingAuthorization(this);
#endif

        // Wait a frame to let GA bootstrap
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
        initialized = true;
        log.Information("GameAnalytics initialized successfully");
      }
      catch (Exception ex)
      {
        log.Error(ex, "Failed to initialize GameAnalytics");
        initialized = true; // Mark as initialized to prevent retry loops
      }
#else
        log.Warning("GameAnalytics SDK not available - using mock backend");
        initialized = true;
#endif
    }

    public void SetUserId(string userId)
    {
      if (!initialized || Application.isEditor) return;
#if GAMEANALYTICS_SDK
      try
      {
        GameAnalytics.SetCustomId(userId);
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to set user ID in GameAnalytics");
      }
#endif
    }

    public void SetUserProperty(string key, string value)
    {
      if (!initialized || Application.isEditor) return;
#if GAMEANALYTICS_SDK
      try
      {
        // Map to custom dimensions (limited). Prefer prefix buckets.
        // GA supports 3 dimension lists; here treat as design events fallback.
        GameAnalytics.NewDesignEvent($"user_property:{key}:{value}");
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to set user property in GameAnalytics");
      }
#endif
    }

    public void SetUserProperties(IReadOnlyDictionary<string, string> props)
    {
      if (props == null) return;
      foreach (KeyValuePair<string, string> kv in props) SetUserProperty(kv.Key, kv.Value);
    }

    public void SetConsent(bool analyticsEnabled, bool adsEnabled)
    {
      if (Application.isEditor) return; // Skip in editor
#if GAMEANALYTICS_SDK
      try
      {
        // GA respects internal privacy flags; minimal placeholder
        // Use your own gating to avoid sending until consent granted.
        GameAnalytics.SetEnabledEventSubmission(analyticsEnabled);
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to set consent in GameAnalytics");
      }
#endif
    }

    public UniTask StartSessionAsync(CancellationToken ct)
    {
      if (!initialized || sessionStarted)
      {
        log.Information("StartSessionAsync skipped - initialized: {0}, sessionStarted: {1}", initialized, sessionStarted);
        return UniTask.CompletedTask;
      }
#if GAMEANALYTICS_SDK
      // Let GameAnalytics handle session management automatically
      sessionStarted = true;
      log.Information("GameAnalytics session management enabled - instance: {0}", GetHashCode());
#endif
      return UniTask.CompletedTask;
    }

    public UniTask EndSessionAsync(CancellationToken ct)
    {
      if (!initialized || !sessionStarted) return UniTask.CompletedTask;
#if GAMEANALYTICS_SDK
      sessionStarted = false;
      log.Information("GameAnalytics session ended");
#endif
      return UniTask.CompletedTask;
    }

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
    {
      if (!initialized || Application.isEditor) return;

#if GAMEANALYTICS_SDK
      try
      {
        if (parameters is { Count: > 0 })
          foreach (KeyValuePair<string, object> kv in parameters)
          {
            float? valueAsNumber = TryAsFloat(kv.Value);
            if (valueAsNumber.HasValue)
              GameAnalytics.NewDesignEvent($"{eventName}:{kv.Key}", valueAsNumber.Value);
            else
              GameAnalytics.NewDesignEvent($"{eventName}:{kv.Key}:{kv.Value}");
          }
        else
          GameAnalytics.NewDesignEvent(eventName);
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to track event in GameAnalytics: {EventName}", eventName);
      }
#endif
    }

    public void TrackRevenue(string currency, double amount, string itemType, string itemId)
    {
      if (!initialized || Application.isEditor) return;
#if GAMEANALYTICS_SDK
      try
      {
        // amount in cents if using NewBusinessEvent? GA expects amount in cents (long) in some SDKs; Unity uses double.
        GameAnalytics.NewBusinessEvent(currency ?? "USD", (int)Math.Round(amount * 100), itemType ?? "unknown", itemId ?? "unknown", null);
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to track revenue in GameAnalytics");
      }
#endif
    }

    public void TrackProgression(string progression1, string progression2, string progression3, int? score)
    {
      if (!initialized || Application.isEditor) return;

#if GAMEANALYTICS_SDK
      try
      {
        if (score.HasValue)
          GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, progression1, progression2, progression3, score.Value);
        else
          GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, progression1, progression2, progression3);
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to track progression in GameAnalytics");
      }
#endif
    }

    public void TrackDesign(string eventName, float? value)
    {
      if (!initialized || Application.isEditor) return;
#if GAMEANALYTICS_SDK
      try
      {
        if (value.HasValue) GameAnalytics.NewDesignEvent(eventName, value.Value);
        else GameAnalytics.NewDesignEvent(eventName);
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to track design event in GameAnalytics: {EventName}", eventName);
      }
#endif
    }

    public void TrackError(string message, string severity)
    {
      if (!initialized || Application.isEditor) return;
#if GAMEANALYTICS_SDK
      try
      {
        GAErrorSeverity sev = severity?.ToLowerInvariant() switch
        {
          "debug" => GAErrorSeverity.Debug,
          "info" => GAErrorSeverity.Info,
          "warning" => GAErrorSeverity.Warning,
          "error" => GAErrorSeverity.Error,
          "critical" => GAErrorSeverity.Critical,
          _ => GAErrorSeverity.Warning
          };
        GameAnalytics.NewErrorEvent(sev, message ?? "unknown");
      }
      catch (Exception ex)
      {
        log.Warning(ex, "Failed to track error in GameAnalytics");
      }
#endif
    }

    public UniTask FlushAsync(CancellationToken ct)
    {
      // GA batches internally. No-op.
      return UniTask.CompletedTask;
    }

#if GAMEANALYTICS_SDK && UNITY_IOS
    public void GameAnalyticsATTListenerNotDetermined() => log.Information("ATT: NotDetermined");
    public void GameAnalyticsATTListenerRestricted() => log.Information("ATT: Restricted");
    public void GameAnalyticsATTListenerDenied() => log.Information("ATT: Denied");
    public void GameAnalyticsATTListenerAuthorized() => log.Information("ATT: Authorized");
#else
    // Provide empty implementations when not on iOS or SDK not available
    public void GameAnalyticsATTListenerNotDetermined() { }
    public void GameAnalyticsATTListenerRestricted() { }
    public void GameAnalyticsATTListenerDenied() { }
    public void GameAnalyticsATTListenerAuthorized() { }
#endif

    private static float? TryAsFloat(object v)
    {
      if (v == null) return null;
      try
      {
        return Convert.ToSingle(v);
      }
      catch
      {
        return null;
      }
    }
  }
}