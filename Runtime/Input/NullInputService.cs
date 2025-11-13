using System;
using MToolKit.Runtime.Input.Interfaces;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Input
{
  /// <summary>
  ///   No-op implementation of IInputService that does nothing
  ///   Used as a fallback when InputActionAsset is not assigned
  /// </summary>
  public class NullInputService : IInputService
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<NullInputService>().ForFeature("Core.Services.InputService"));
    private static ILogger log => logLazy.Value ?? Logger.None;

#pragma warning disable CS0067
    public event Action OnPausePressed;
    public event Action OnAnyKeyPressed;
#pragma warning restore CS0067

    public void Initialize(object inputActionAsset)
    {
      log.ForMethod().Warning("NullInputService: Initialize called but service is not functional. Assign InputActionAsset in GlobalInstaller to enable input.");
    }

    public void Enable()
    {
      log.ForMethod().Verbose("NullInputService: Enable called (no-op)");
    }

    public void Disable()
    {
      log.ForMethod().Verbose("NullInputService: Disable called (no-op)");
    }

    public bool IsActionPressed(object action)
    {
      return false;
    }

    public bool WasActionPressedThisFrame(object action)
    {
      return false;
    }

    public bool WasActionReleasedThisFrame(object action)
    {
      return false;
    }
  }
}