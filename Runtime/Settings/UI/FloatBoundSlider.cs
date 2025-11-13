using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.UI.Abstract;
using R3;
using Serilog;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.UI
{
  /// <summary>
  ///   Concrete implementation of GenericSliderWithLabel for controlling the master volume.
  ///   Handles binding to the reactive master volume setting and updating it when the slider changes.
  /// </summary>
  public class FloatBoundElementSlider : AbstractSettingsSlider
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<FloatBoundElementSlider>().ForFeature("Settings.UI"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    // Caches the last slider value to prevent duplicate updates.
    private float previousSliderValue;
    // Subscription to the reactive setting.
    private IDisposable reactiveSubscription;
    // Holds the reactive setting this slider is bound to.
    public FloatBoundReactiveSetting FloatBoundReactiveSetting { get; set; } = new();

    private void OnDestroy()
    {
      reactiveSubscription?.Dispose();

      if (Slider != null)
        Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }

    /// <summary>
    ///   Binds this slider to the provided reactive setting for master volume.
    /// </summary>
    /// <param name="reactiveSetting">The reactive setting to bind.</param>
    public void Bind(ReactiveSetting<float> reactiveSetting)
    {
      // Check if the reactive setting is already disposed
      if (reactiveSetting == null || reactiveSetting.Property == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot bind to null or disposed reactive setting");
        return;
      }

      FloatBoundReactiveSetting.Bind(reactiveSetting);

      ConfigureSlider(FloatBoundReactiveSetting.Setting.Name, FloatBoundReactiveSetting.Setting.Value);
      previousSliderValue = Value;

      // Check if the property is disposed before subscribing
      if (!FloatBoundReactiveSetting.Setting.Property.IsDisposed)
      {
        reactiveSubscription = FloatBoundReactiveSetting.Setting.Property.Subscribe(newValue =>
        {
          if (!Mathf.Approximately(Value, newValue))
            Value = newValue;
        });
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot subscribe to disposed ReactiveProperty");
        return;
      }

      if (Slider != null)
        Slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    /// <summary>
    ///   Called when the underlying slider's value changes.
    ///   Updates the reactive setting if the slider value has changed.
    /// </summary>
    /// <param name="newValue">The new slider value.</param>
    private void OnSliderValueChanged(float newValue)
    {
      // Prevent duplicate updates.
      if (Mathf.Approximately(previousSliderValue, newValue))
        return;

      previousSliderValue = newValue;
      FloatBoundReactiveSetting.Setting.Value = newValue;

      log.ForGameObject(gameObject).ForMethod().Information("Slider {name} value changed to {newValue}", FloatBoundReactiveSetting.Setting.Name, newValue);
    }
  }
}