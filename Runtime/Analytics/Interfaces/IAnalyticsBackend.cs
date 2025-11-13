using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace MToolKit.Runtime.Analytics.Interfaces
{
  /// <summary>
  ///   Interface for analytics backend.
  /// </summary>
  public interface IAnalyticsBackend
  {
    bool IsStarted { get; }

    UniTask InitializeAsync(CancellationToken ct);
    UniTask StartSessionAsync(CancellationToken ct);
    UniTask EndSessionAsync(CancellationToken ct);

    void SetUserId(string userId);
    void SetUserProperty(string key, string value);
    void SetUserProperties(IReadOnlyDictionary<string, string> props);
    void SetConsent(bool analyticsEnabled, bool adsEnabled);

    void TrackEvent(string eventName, IReadOnlyDictionary<string, object> parameters);
    void TrackRevenue(string currency, double amount, string itemType, string itemId);
    void TrackProgression(string progression1, string progression2, string progression3, int? score);
    void TrackDesign(string eventName, float? value);
    void TrackError(string message, string severity);

    UniTask FlushAsync(CancellationToken ct);
  }
}