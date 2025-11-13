// Navigation/Views/SettingsView.cs

using System;
using System.Threading;
using MToolKit.Runtime.Navigation.Views;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.ErrorSystem.Views
{
  public class ErrorView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ErrorView>().ForFeature("ErrorSystem.Views"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private CancellationTokenSource cts;

    [SerializeField]
    [Required]
    private TextMeshProUGUI errorText;

    [SerializeField]
    [Required]
    private Button closeButton;

    [Inject]
    public void Construct()
    {
      log.ForGameObject(gameObject).ForMethod().Information("Constructing ErrorView");
    }

    public override void Show()
    {
      base.Show();
      EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    public void SetMessage(string message)
    {
      log.ForGameObject(gameObject).ForMethod().Information("Setting message: {0}", message);
      errorText.SetText(message);
    }

    private void OnEnable()
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Enabled");
      cts = new CancellationTokenSource();
    }

    private void OnDisable()
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Disabled");
      cts?.Cancel();
      cts?.Dispose();
      cts = null;
    }
  }
}