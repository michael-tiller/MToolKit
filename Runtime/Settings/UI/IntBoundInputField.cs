using System;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.UI.Abstract;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.UI
{
  /// <summary>
  ///   Concrete settings element binding a <see cref="TMP_InputField" /> to a reactive integer
  ///   setting. The setting's value drives the field text; a VALID integer typed into the field
  ///   writes back to the setting. Non-numeric input is IGNORED — the setting keeps its last value
  ///   (number-in ⇒ change, not-a-number ⇒ no change). Mirrors <see cref="BoolBoundToggle" /> /
  ///   <see cref="IntBoundElementDropdown" />.
  /// </summary>
  public class IntBoundInputField : AbstractSettingsElement
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<IntBoundInputField>().ForFeature("Settings.UI"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private TMP_InputField inputField;

    // Subscription to the reactive setting.
    private IDisposable reactiveSubscription;
    // Guards the value-changed listener while we push the setting's value into the field.
    private bool suppressWriteback;
    // Holds the reactive setting this input field is bound to.
    public IntBoundReactiveSetting IntBoundReactiveSetting { get; set; } = new();

    public TMP_InputField InputField => inputField;

    private void OnDestroy()
    {
      reactiveSubscription?.Dispose();

      if (inputField != null)
        inputField.onValueChanged.RemoveListener(OnInputValueChanged);
    }

    /// <summary>
    ///   Binds this input field to the provided reactive integer setting.
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

      if (label != null)
        Name = IntBoundReactiveSetting.Setting.Name;
      SetTextWithoutWriteback(IntBoundReactiveSetting.Setting.Value);

      // Check if the property is disposed before subscribing
      if (!IntBoundReactiveSetting.Setting.Property.IsDisposed)
      {
        reactiveSubscription = IntBoundReactiveSetting.Setting.Property.Subscribe(SetTextWithoutWriteback);
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Cannot subscribe to disposed ReactiveProperty");
        return;
      }

      if (inputField != null)
        inputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    /// <summary>
    ///   Called when the field text changes. A valid integer is written back to the setting;
    ///   non-numeric text is ignored (no change).
    /// </summary>
    /// <param name="text">The new field text.</param>
    private void OnInputValueChanged(string text)
    {
      if (suppressWriteback)
        return;

      if (int.TryParse(text, out int value))
        IntBoundReactiveSetting.Setting.Value = value;
    }

    private void SetTextWithoutWriteback(int value)
    {
      if (inputField == null)
        return;

      suppressWriteback = true;
      inputField.SetTextWithoutNotify(value.ToString());
      suppressWriteback = false;
    }
  }
}
