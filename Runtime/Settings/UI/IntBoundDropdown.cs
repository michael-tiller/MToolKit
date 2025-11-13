using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.UI.Abstract;
using R3;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.UI
{
  public class IntBoundElementDropdown : AbstractSettingsDropdown
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<IntBoundElementDropdown>().ForFeature("Settings.UI"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    // Caches the last dropdown value to prevent duplicate updates.
    private int previousSelectedIndex;
    // Subscription to the reactive setting.
    private IDisposable reactiveSubscription;
    // Holds the reactive setting this dropdown is bound to.
    public IntBoundReactiveSetting IntBoundReactiveSetting { get; set; } = new();

    private void OnDestroy()
    {
      reactiveSubscription?.Dispose();

      if (Dropdown != null)
        Dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
    }

    /// <summary>
    ///   Binds this dropdown to the provided reactive integer setting.
    /// </summary>
    /// <param name="reactiveSetting">The reactive integer setting to bind.</param>
    public void Bind(ReactiveSetting<int> reactiveSetting)
    {
      // Check if the reactive setting is already disposed
      if (reactiveSetting == null || reactiveSetting.Property == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot bind to null or disposed reactive setting");
        return;
      }

      IntBoundReactiveSetting.Bind(reactiveSetting);

      ConfigureDropdown(IntBoundReactiveSetting.Setting.Name, IntBoundReactiveSetting.Setting.Value);
      previousSelectedIndex = Dropdown.value;

      // Check if the property is disposed before subscribing
      if (!IntBoundReactiveSetting.Setting.Property.IsDisposed)
      {
        reactiveSubscription = IntBoundReactiveSetting.Setting.Property.Subscribe(newValue =>
        {
          if (Dropdown != null && Dropdown.value != newValue)
            Dropdown.value = newValue;
        });
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot subscribe to disposed ReactiveProperty");
        return;
      }


      if (Dropdown != null)
        Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    ///   Called when the dropdown's value changes.
    ///   Updates the reactive setting if the value has changed.
    /// </summary>
    /// <param name="newValue">The new dropdown index.</param>
    private void OnDropdownValueChanged(int newValue)
    {
      // Prevent duplicate updates.
      if (previousSelectedIndex == newValue)
        return;

      previousSelectedIndex = newValue;
      IntBoundReactiveSetting.Setting.Value = newValue;
    }
  }
}