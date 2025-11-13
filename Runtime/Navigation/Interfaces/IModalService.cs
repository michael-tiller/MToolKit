using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Navigation.Events;
using MToolKit.Runtime.Navigation.Views;
using MToolKit.Runtime.Settings.Enums;
using UnityEngine.Events;

namespace MToolKit.Runtime.Navigation.Interfaces
{
  public interface IModalService
  {
    UniTask CreateModalView<T>(
      CancellationToken token,
      string modalName,
      string title,
      string message,
      EModalButtonType type1,
      string text1,
      UnityAction action1,
      EModalButtonType type2 = EModalButtonType.None,
      string text2 = null,
      UnityAction action2 = null,
      EModalButtonType type3 = EModalButtonType.None,
      string text3 = null,
      UnityAction action3 = null)
      where T : ModalView;

    UniTask CreateTimedModalView(
      CancellationToken token,
      string modalName,
      string title,
      string message,
      EModalButtonType type1,
      string text1,
      UnityAction action1,
      float timeout,
      UnityAction timeoutCallback,
      EModalButtonType type2 = EModalButtonType.None,
      string text2 = null,
      UnityAction action2 = null,
      EModalButtonType type3 = EModalButtonType.None,
      string text3 = null,
      UnityAction action3 = null);

    public void OnInterstitialAlertRequest(InterstitialAlertRequestMessage msg, CancellationToken token);
  }
}