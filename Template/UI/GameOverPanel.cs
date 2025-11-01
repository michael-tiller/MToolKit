using System;
using Cysharp.Threading.Tasks;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using MToolKit.Runtime.MessageBus;
using ILogger = Serilog.ILogger;
using MessagePipe;
using MToolKit.Template.ExamplePlayer.Events;

namespace MToolKit.Template.UI
{
  public class GameOverPanel : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameOverPanel>().ForFeature("MToolKit.Template.UI"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    private IDisposable deathSubscription;

    [SerializeField, Required]
    private CanvasGroup gameOverCanvasGroup;
    [SerializeField, Required]
    private TextMeshProUGUI causeOfDeathLabel;

    private const string CAUSE_OF_DEATH_PREFIX = "Cause Of Death: ";


    private void Start()
    {
      // Subscribe to player death messages via the message broker
      var subscriber = GameMessageBroker.GetSubscriber<PlayerDeathMessage>();
      if (subscriber != null)
      {
        deathSubscription = subscriber.Subscribe(OnPlayerDeath);
      }
      
      gameOverCanvasGroup.alpha = 0;
      gameOverCanvasGroup.blocksRaycasts = false;
      gameOverCanvasGroup.interactable = false;
    }

    private void OnDestroy()
    {
      deathSubscription?.Dispose();
    }

    private void OnPlayerDeath(PlayerDeathMessage deathMessage)
    {
      OnPlayerDeathAsync(deathMessage).Forget();
    }

    private async UniTaskVoid OnPlayerDeathAsync(PlayerDeathMessage deathMessage)
    {
      await UniTask.Delay(1000);
      gameOverCanvasGroup.alpha = 1;
      gameOverCanvasGroup.blocksRaycasts = true;
      gameOverCanvasGroup.interactable = true;
      causeOfDeathLabel.SetText(CAUSE_OF_DEATH_PREFIX + deathMessage.CauseOfDeath);
    }

    public void OnRespawn()
    {
      gameOverCanvasGroup.alpha = 0;
      gameOverCanvasGroup.blocksRaycasts = false;
      gameOverCanvasGroup.interactable = false;
      
      // Publish respawn request via message broker
      GameMessageBroker.Publish(new PlayerRespawnRequestMessage());
    }
  }
}
