using MessagePipe;
using MToolKit.Runtime.Analytics.Interfaces;
using MToolKit.Runtime.ErrorSystem.Messages;
using R3;

namespace MToolKit.Runtime.Analytics.Events
{
  /// <summary>
  ///   Namespace for analytics plugin, service, and event bridge.
  /// </summary>
  internal sealed class NameSpaceDoc { }

  /// <summary>
  ///   Bridge between analytics events and analytics service.
  /// </summary>
  public sealed class AnalyticsEventBridge
  {
    private readonly IAnalyticsService analytics;
    private readonly CompositeDisposable disposables = new();
    private readonly ISubscriber<ErrorRequestMessage> errorMessages;
    private readonly ISubscriber<AnalyticsGameEvent> gameEvents;
    private readonly ISubscriber<AnalyticsRevenueEvent> revenueEvents;

    public AnalyticsEventBridge(
      ISubscriber<AnalyticsGameEvent> gameEvents,
      ISubscriber<AnalyticsRevenueEvent> revenueEvents,
      ISubscriber<ErrorRequestMessage> errorMessages,
      IAnalyticsService analytics)
    {
      this.gameEvents = gameEvents;
      this.revenueEvents = revenueEvents;
      this.errorMessages = errorMessages;
      this.analytics = analytics;

      gameEvents.Subscribe(OnGameEvent).AddTo(disposables);
      revenueEvents.Subscribe(OnRevenue).AddTo(disposables);
      errorMessages.Subscribe(OnError).AddTo(disposables);
    }

    public void Dispose()
    {
      disposables.Dispose();
    }

    private void OnGameEvent(AnalyticsGameEvent evt)
    {
      analytics.TrackEvent(evt.Name, evt.Params);
    }

    private void OnRevenue(AnalyticsRevenueEvent evt)
    {
      analytics.TrackRevenue(evt.Currency, evt.Amount, evt.ItemType, evt.ItemId);
    }

    private void OnError(ErrorRequestMessage errorMessage)
    {
      // Automatically track errors in analytics
      string severity = errorMessage.Fatal ? "fatal" : "error";
      analytics.TrackError(errorMessage.Message, severity);
    }
  }
}