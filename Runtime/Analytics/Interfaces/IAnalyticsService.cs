using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// Namespace for analytics service interfaces.
/// </summary>
namespace MToolKit.Runtime.Analytics.Interfaces
{

/// <summary>
/// Interface for analytics service.
/// </summary>
public interface IAnalyticsService
{
    UniTask InitializeAsync(CancellationToken ct);
    UniTask StartSessionAsync(CancellationToken ct);
    UniTask EndSessionAsync(CancellationToken ct);

    void SetUserId(string userId);
    void SetUserProperty(string key, string value);
    void SetUserProperties(IReadOnlyDictionary<string, string> props);
    void SetConsent(bool analyticsEnabled, bool adsEnabled);

    void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    void TrackRevenue(string currency, double amount, string itemType = null, string itemId = null);
    void TrackProgression(string progression1, string progression2 = null, string progression3 = null, int? score = null);
    void TrackDesign(string eventName, float? value = null);
    void TrackError(string message, string severity = "warning"); // debug, info, warning, error, critical

    UniTask FlushAsync(CancellationToken ct);
}
}