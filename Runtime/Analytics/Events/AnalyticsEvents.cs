using System.Collections.Generic;

namespace MToolKit.Runtime.Analytics.Events
{
  /// <summary>
  ///   Event for tracking game events.
  /// </summary>
  public readonly struct AnalyticsGameEvent
  {
    public readonly string Name;
    public readonly IReadOnlyDictionary<string, object> Params;

    public AnalyticsGameEvent(string name, IReadOnlyDictionary<string, object> @params = null)
    {
      Name = name;
      Params = @params;
    }
  }

  /// <summary>
  ///   Event for tracking revenue events.
  /// </summary>
  public readonly struct AnalyticsRevenueEvent
  {
    public readonly string Currency;
    public readonly double Amount;
    public readonly string ItemType;
    public readonly string ItemId;

    public AnalyticsRevenueEvent(string currency, double amount, string itemType = null, string itemId = null)
    {
      Currency = currency;
      Amount = amount;
      ItemType = itemType;
      ItemId = itemId;
    }
  }
}