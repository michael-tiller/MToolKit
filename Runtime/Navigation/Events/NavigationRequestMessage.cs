// MessageBus/Messages/NavigationRequestMessage.cs

using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.DataStructures;

namespace MToolKit.Runtime.Navigation.Events
{
  public readonly struct NavigationRequestMessage : INavigationMessage
  {
    public readonly NavigationRequestMessageBody Body;

    public NavigationRequestMessage(NavigationRequestMessageBody body)
    {
      Body = body;
    }
  }

}