using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.UI.Abstract;
using R3;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.UI
{
  /// <summary>
  ///   Concrete implementation of a toggle element bound to a reactive boolean setting.
  ///   It handles binding to the reactive setting and updating it when the toggle changes.
  /// </summary>
  public class BoolBoundToggle : AbstractSettingsToggle
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<BoolBoundToggle>().ForFeature("Settings.UI"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    // Caches the last toggle state to prevent duplicate updates.
    private bool previousToggleValue;
    // Subscription to the reactive setting.
    private IDisposable reactiveSubscription;
    // Holds the reactive setting this toggle is bound to.
    public BoolBoundReactiveSetting BoolBoundReactiveSetting { get; set; } = new();

    private void OnDestroy()
    {
      reactiveSubscription?.Dispose();

      if (Toggle != null)
        Toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
    }

    /// <summary>
    ///   Binds this toggle to the provided reactive boolean setting.
    /// </summary>
    /// <param name="reactiveSetting">The reactive boolean setting to bind.</param>
    public void Bind(ReactiveSetting<bool> reactiveSetting)
    {
      // Check if the reactive setting is already disposed
      if (reactiveSetting == null || reactiveSetting.Property == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot bind to null or disposed reactive setting");
        return;
      }

      BoolBoundReactiveSetting.Bind(reactiveSetting);

      ConfigureToggle(BoolBoundReactiveSetting.Setting.Name, BoolBoundReactiveSetting.Setting.Value);
      previousToggleValue = Toggle.isOn;

      // Check if the property is disposed before subscribing
      if (!BoolBoundReactiveSetting.Setting.Property.IsDisposed)
      {
        reactiveSubscription = BoolBoundReactiveSetting.Setting.Property.Subscribe(newValue =>
        {
          if (Toggle != null && Toggle.isOn != newValue)
            Toggle.isOn = newValue;
        });
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot subscribe to disposed ReactiveProperty");
        return;
      }

      if (Toggle != null)
        Toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    /// <summary>
    ///   Called when the toggle's value changes.
    ///   Updates the reactive setting if the value has changed.
    /// </summary>
    /// <param name="newValue">The new toggle state.</param>
    private void OnToggleValueChanged(bool newValue)
    {
      // Prevent duplicate updates.
      if (previousToggleValue == newValue)
        return;

      previousToggleValue = newValue;
      BoolBoundReactiveSetting.Setting.Value = newValue;
    }
  }
}