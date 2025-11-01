using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using MToolKit.Runtime.Analytics.Interfaces;

namespace MToolKit.Runtime.Analytics
{
/// <summary>
/// Concrete implementation of IAnalyticsService.
/// </summary>

[Serializable]
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IAnalyticsBackend backend; // Backend implementation

    /// <summary>
    /// Constructor for AnalyticsService.
    /// </summary>
    /// <param name="backend">The backend implementation.</param>
    public AnalyticsService(IAnalyticsBackend backend)
    {
        this.backend = backend;
    }

    public UniTask InitializeAsync(CancellationToken ct) => backend.InitializeAsync(ct);
    public UniTask StartSessionAsync(CancellationToken ct) 
    {
        if (backend.Started) return UniTask.CompletedTask;
        return backend.StartSessionAsync(ct);
    }
    
    public UniTask EndSessionAsync(CancellationToken ct) 
    {
        if (!backend.Started) return UniTask.CompletedTask;
        return backend.EndSessionAsync(ct);
    }

    public void SetUserId(string userId) => backend.SetUserId(userId);
    public void SetUserProperty(string key, string value) => backend.SetUserProperty(key, value);
    public void SetUserProperties(IReadOnlyDictionary<string, string> props) => backend.SetUserProperties(props);
    public void SetConsent(bool analyticsEnabled, bool adsEnabled) => backend.SetConsent(analyticsEnabled, adsEnabled);

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null) => backend.TrackEvent(eventName, parameters);
    public void TrackRevenue(string currency, double amount, string itemType = null, string itemId = null) => backend.TrackRevenue(currency, amount, itemType, itemId);
    public void TrackProgression(string progression1, string progression2 = null, string progression3 = null, int? score = null) => backend.TrackProgression(progression1, progression2, progression3, score);
    public void TrackDesign(string eventName, float? value = null) => backend.TrackDesign(eventName, value);
    public void TrackError(string message, string severity = "warning") => backend.TrackError(message, severity);

    public UniTask FlushAsync(CancellationToken ct) => backend.FlushAsync(ct);
}
}