// Navigation/Views/SettingsView.cs

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.Events;
using Serilog;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Navigation.Views
{
  public class InterstitialAlertView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<InterstitialAlertView>().ForFeature("Navigation.Views"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private CancellationTokenSource cts;

    [SerializeField]
    [Required]
    private TextMeshProUGUI messageText;

    [Inject]
    public void Construct()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Constructing InterstitialAlertView");
    }


    public void SetMessage(string message)
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Setting message: {0}", message);
      if (message.IsNullOrWhitespace())
        GlobalAsyncMessageBroker.Publish(new BackRequestMessage(Canvas));
      else
        messageText.SetText(message);
    }

    private void OnEnable()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Enabled");
      cts = new CancellationTokenSource();
      SubscribeToEvents(cts.Token).Forget();
    }

    private void OnDisable()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Disabled");
      cts?.Cancel();
      cts?.Dispose();
      cts = null;
    }

    private async UniTask SubscribeToEvents(CancellationToken token)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Subscribing to events");
      await token.WaitUntilCanceled();
    }

    // Override Hide to prevent destruction during async operations
    public override void Hide() { }
  }
}