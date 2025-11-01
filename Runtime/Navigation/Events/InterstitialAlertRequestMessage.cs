// MessageBus/Messages/InterstitialAlertRequestMessage.cs
using MToolKit.Runtime.Navigation.Interfaces;

namespace MToolKit.Runtime.Navigation.Events
{
  public readonly struct InterstitialAlertRequestMessage : INavigationMessage
  {
    public readonly string Message;
    public InterstitialAlertRequestMessage(string message)
    {
      Message = message;
    }
  }
}