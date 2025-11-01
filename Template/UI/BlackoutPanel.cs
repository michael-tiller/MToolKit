

using System;
using DG.Tweening;
using MessagePipe;
using MToolKit.Runtime.MessageBus;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.ExamplePlayer.Events;

namespace MToolKit.Template.UI
{
  public class BlackoutPanel : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<BlackoutPanel>().ForFeature("MToolKit.Template.UI"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    [SerializeField][Required] private Image blackoutImage;

    IDisposable disposable;

    private void Start()  
    {
      SetAlpha(1); 
      disposable = GameMessageBroker.GetSubscriber<FadeBlackoutMessage>().Subscribe(OnFadeBlackout);
    }
    private void OnDestroy()
    {
      disposable?.Dispose();
      disposable = null;
    }

    private void SetAlpha(float alpha)
    {
      blackoutImage.color = new Color(0, 0, 0, alpha);
    }
    private void OnFadeBlackout(FadeBlackoutMessage message)
    {
      FadeToAlpha(message.Alpha, message.Duration);
    }

    private void FadeToAlpha(float alpha, float duration)
    {
      DOTween.To(() => blackoutImage.color.a, x => blackoutImage.color = new Color(0, 0, 0, x), alpha, duration);
    }

    [Button]
    public void Show() => SetAlpha(1);
    [Button]
    public void Hide() => SetAlpha(0);
    [Button]
    public void FadeIn() => FadeToAlpha(0, 1);
    [Button]
    public void FadeOut() => FadeToAlpha(1, 1);
    
  }
}
