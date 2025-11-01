using MessagePipe;
using R3;
using MToolKit.Runtime.ErrorSystem.Messages;
using MToolKit.Runtime.Analytics.Interfaces;

/// <summary>
/// Namespace for analytics plugin, service, and event bridge.
/// </summary>
namespace MToolKit.Runtime.Analytics.Events
{

/// <summary>
/// Bridge between analytics events and analytics service.
/// </summary>
public sealed class AnalyticsEventBridge
{
    private readonly ISubscriber<AnalyticsGameEvent> gameEvents;
    private readonly ISubscriber<AnalyticsRevenueEvent> revenueEvents;
    private readonly ISubscriber<ErrorRequestMessage> errorMessages;
    private readonly IAnalyticsService analytics;
    private readonly CompositeDisposable disposables = new();

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

    public void Dispose() => disposables.Dispose();

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
        var severity = errorMessage.Fatal ? "fatal" : "error";
        analytics.TrackError(errorMessage.Message, severity);
    }
}
}