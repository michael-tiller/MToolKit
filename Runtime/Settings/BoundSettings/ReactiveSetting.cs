using System;
using System.Collections.Generic;
using MToolKit.Runtime.Settings.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Settings.BoundSettings
{
  public class ReactiveSetting<T> : IReactiveSetting<T>, IDisposable
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ReactiveSetting<T>>().ForFeature("Settings.BoundSettings"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IDisposable selfSub;

    private readonly ISettingsSystem settingsController;

    /// <summary>
    ///   Stores the last applied value.
    /// </summary>
    public ReactiveProperty<T> LastProperty = new();

    private ISettingsInitializer settingsInitializer;

    /// <summary>
    ///   Initializes a new instance with the specified default value.
    /// </summary>
    public ReactiveSetting(T defaultValue, string name, ISettingsSystem settingsController = null)
    {
      Default = defaultValue;
      LastProperty.Value = defaultValue;
      Property = new ReactiveProperty<T>(defaultValue);
      Name = name;
      this.settingsController = settingsController;
      selfSub = Property.Subscribe(OnValueChanged);
    }

    public T Value
    {
      get => Property.Value;
      set => Property.Value = value;
    }

    public T LastValue => LastProperty.Value;

    public bool IsDefault => Equals(Value, Default);

    #region IDisposable Members

    public void Dispose()
    {
      selfSub?.Dispose();
      Property?.Dispose();
    }

    #endregion

    #region IReactiveSetting<T> Members

    public ReactiveProperty<T> Property { get; } = new();
    public T Default { get; }

    public string Name { get; }
    public bool IsDirty => !Equals(Value, LastValue);

    public void OnApply()
    {
      LastProperty.Value = Value;
    }

    public void OnCancel()
    {
      Value = LastValue;
    }

    public void OnRevertToDefault()
    {
      LastProperty.Value = Value = Default;
    }

    #endregion

    private void OnValueChanged(T value)
    {
      if (IsDirty)
        if (settingsController != null)
          settingsController.SetDirty(true);
    }

    public override bool Equals(object obj)
    {
      if (ReferenceEquals(this, obj))
        return true;
      if (obj is ReactiveSetting<T> other)
        return EqualityComparer<T>.Default.Equals(Value, other.Value) &&
               string.Equals(Name, other.Name, StringComparison.Ordinal);
      return false;
    }

    public override int GetHashCode()
    {
      unchecked
      {
        int hash = 17;
        hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
        hash = hash * 23 + EqualityComparer<T>.Default.GetHashCode(Value);
        return hash;
      }
    }
  }
}