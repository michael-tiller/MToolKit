using System;
using System.Collections.Generic;
using UnityEngine;
using Serilog;
using ILogger = Serilog.ILogger;
using Sirenix.OdinInspector;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.ErrorSystem.Interface;
using VContainer;
using MToolKit.Runtime.MessageBus;
using R3;
using MessagePipe;
using MToolKit.Runtime.ErrorSystem.Messages;
using MToolKit.Runtime.ErrorSystem.Views;

namespace MToolKit.Runtime.ErrorSystem
{
  /// <summary>
  /// ErrorSystem Plugin that registers the ErrorService globally.
  /// Provides error display functionality through message bus integration.
  /// </summary>
  public sealed class ErrorSystemPlugin : AbstractGamePlugin, IDependencyDeclaration, IRuntimePlugin
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ErrorSystemPlugin>().ForFeature("Core.ErrorSystem"));
    private new static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    public IEnumerable<Type> RequiredServices => new[] { typeof(IErrorService) };

    public IEnumerable<Type> OptionalServices => Array.Empty<Type>();

    [SerializeField, Required] private ErrorView errorView;
    [SerializeField, Required] private CanvasGroup errorCanvasGroup;

    private IDisposable errorRequestMessageSubscription;
    private IErrorService errorService;

    [ReadOnly]
    [ShowInInspector]
    private ErrorService errorServiceInstance => errorService as ErrorService;

    public override void Register(IContainerBuilder builder)
    {
      log.ForMethod(nameof(Register)).Debug("ErrorSystemPlugin Register called");

      // Ensure the plugin persists across scene changes
      DontDestroyOnLoad(gameObject);

      // Register the ErrorService as a singleton
      builder.Register<IErrorService>(resolver =>
      {
        try
        {
          errorService = new ErrorService(errorView, errorCanvasGroup);
          return errorService;
        }
        catch (VContainerException ex)
        {
          log.ForMethod(nameof(Register)).Error(ex, "Failed to resolve dependencies for ErrorService");
          throw;
        }
      }, Lifetime.Singleton);

      log.ForMethod(nameof(Register)).Information("ErrorSystemPlugin Register completed - IErrorService registered");
    }

    public void PerformSetup(IObjectResolver resolver)
    {
      log.ForMethod(nameof(PerformSetup)).Debug("Performing setup for ErrorSystemPlugin");
    }

    public void PerformRuntimeInitialization(IObjectResolver resolver)
    {
      log.ForMethod(nameof(PerformRuntimeInitialization)).Debug("Performing runtime initialization for ErrorSystemPlugin");

      // Resolve the error service
      errorService = resolver.Resolve<IErrorService>();

      // Hide error on initialization
      errorService.HideError();

      // Subscribe to error request messages
      errorRequestMessageSubscription = GlobalAsyncMessageBroker.GetSubscriber<ErrorRequestMessage>().Subscribe(OnErrorRequestMessage);
    }

    private void OnErrorRequestMessage(ErrorRequestMessage message)
    {
      log.ForMethod(nameof(OnErrorRequestMessage)).Debug("OnErrorRequestMessage called with message: {0}", message.Message);
      
      // Defensive: Ensure error service is available before attempting to show error
      if (errorService == null)
      {
        log.ForMethod(nameof(OnErrorRequestMessage)).Warning("Error service not initialized yet, cannot show error. Message: {Message}", message.Message);
        return;
      }
      
      try
      {
        errorService.ShowError(message);
      }
      catch (Exception ex)
      {
        // If error showing fails, log it but don't crash the system
        log.ForMethod(nameof(OnErrorRequestMessage)).Error(ex, "Failed to show error message: {Message}", message.Message);
      }
    }

    public bool AreDependenciesReady(IObjectResolver resolver)
    {
      log.ForMethod(nameof(AreDependenciesReady)).Debug("Checking if dependencies are ready for ErrorSystemPlugin");
      return resolver.TryResolve<IErrorService>(out _);
    }

    private void OnDestroy()
    {
      log.ForMethod(nameof(OnDestroy)).Debug("OnDestroy called for ErrorSystemPlugin");
      errorRequestMessageSubscription?.Dispose();
      errorRequestMessageSubscription = null;
    }

    public void OnClickBackButton()
    {
      if (errorService == null)
      {
        log.ForMethod(nameof(OnClickBackButton)).Warning("Cannot handle back button - error service not initialized");
        return;
      }
      
      if (errorServiceInstance.LastErrorMessage.HasValue && errorServiceInstance.LastErrorMessage.Value.Fatal)
      {
        log.ForMethod(nameof(OnClickBackButton)).Error("Fatal error, exiting application");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
        return;
      }
      
      log.ForGameObject(gameObject).ForMethod().Information("OnClickBackButton called for ErrorSystemPlugin");
      errorService.HideError();
    }
  }
}