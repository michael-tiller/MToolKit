using System;
using MToolKit.Runtime.ErrorSystem.Interface;
using MToolKit.Runtime.ErrorSystem.Messages;
using MToolKit.Runtime.ErrorSystem.Views;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.ErrorSystem
{
  /// <summary>
  ///   Service implementation for displaying error messages to the user.
  ///   Manages the error view and canvas group to show/hide error dialogs.
  /// </summary>
  [Serializable]
  public class ErrorService : IErrorService
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ErrorService>().ForFeature("Core.ErrorSystem.Service"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly ErrorView errorView;
    private readonly CanvasGroup errorCanvasGroup;


    [ReadOnly]
    [ShowInInspector]
    public ErrorRequestMessage? LastErrorMessage { get; private set; }

    public ErrorService(ErrorView errorView, CanvasGroup errorCanvasGroup)
    {
      this.errorView = errorView;
      this.errorCanvasGroup = errorCanvasGroup;

      log.Verbose("ErrorService constructed");
    }

    public void ShowError(ErrorRequestMessage message)
    {
      // ErrorRequestMessage is a struct, so null checks are not needed
      log.Information("Showing error message: {Message}", message.Message);

      // Defensive: Check for null references before using
      if (errorView == null || errorCanvasGroup == null)
      {
        log.Error("ErrorService has null references - cannot display error. Missing view: {MissingView}, missing group: {MissingGroup}",
          errorView == null, errorCanvasGroup == null);
        return;
      }

      try
      {
        errorView.SetMessage(message.Message);
        errorCanvasGroup.alpha = 1;
        errorCanvasGroup.blocksRaycasts = true;
        errorCanvasGroup.interactable = true;
        LastErrorMessage = message;
      }
      catch (Exception ex)
      {
        log.Error(ex, "Exception while showing error message: {Message}", message.Message);
      }
    }

    public void HideError()
    {
      log.Verbose("Hiding error message");

      // Defensive: Check for null references
      if (errorView == null || errorCanvasGroup == null)
      {
        log.Warning("ErrorService has null references - cannot hide error");
        return;
      }

      try
      {
        errorView.SetMessage(string.Empty);
        errorCanvasGroup.alpha = 0;
        errorCanvasGroup.blocksRaycasts = false;
        errorCanvasGroup.interactable = false;
        LastErrorMessage = null;
      }
      catch (Exception ex)
      {
        log.Error(ex, "Exception while hiding error message");
      }
    }
  }
}