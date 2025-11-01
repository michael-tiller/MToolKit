// Navigation/Views/PrivacyView.cs

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Navigation.Events;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Navigation.Views
{

  public class PrivacyView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<PrivacyView>().ForFeature("Navigation.Views"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private CancellationTokenSource cts;

    [Inject]
    public void Construct()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Constructing PrivacyView");
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
    
    public void OnAccept()
    {
      log.ForGameObject(gameObject).ForMethod().Information("User clicked OnAccept");
      GlobalAsyncMessageBroker.Publish(new BackRequestMessage(Canvas));
      PlayerPrefs.SetInt("PrivacyAccepted", 1);
    }
  
  }
}