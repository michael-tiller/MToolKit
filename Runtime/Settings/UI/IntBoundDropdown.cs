using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.UI.Abstract;
using R3;
using Serilog;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

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

    [SerializeField]
    private Button prevButton, nextButton;

    [SerializeField]
    private bool showNextPrevButtons;

    public void OnNext()
    {
      int count = Dropdown.options.Count;
      if (count == 0) return;
      Dropdown.value = (Dropdown.value + 1) % count;
    }

    public void OnPrev()
    {
      int count = Dropdown.options.Count;
      if (count == 0) return;
      Dropdown.value = (Dropdown.value - 1 + count) % count;
    }

    protected override void OnEnable()
    {
      base.OnEnable();

      if (nextButton == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("nextButton is not assigned");
        return;
      }
      if (prevButton == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("prevButton is not assigned");
        return;
      }
      nextButton.onClick.AddListener(OnNext);
      prevButton.onClick.AddListener(OnPrev);

      nextButton.gameObject.SetActive(showNextPrevButtons);
      prevButton.gameObject.SetActive(showNextPrevButtons);
    }

    protected override void OnDisable()
    {
      base.OnDisable();
      if (nextButton == null || prevButton == null)
        return;
      nextButton.onClick.RemoveListener(OnNext);
      prevButton.onClick.RemoveListener(OnPrev);
    }
    
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