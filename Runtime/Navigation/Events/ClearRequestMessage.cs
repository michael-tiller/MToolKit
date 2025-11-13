// MessageBus/Messages/ClearRequestMessage.cs

using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;

namespace MToolKit.Runtime.Navigation.Events
{
  public readonly struct ClearRequestMessage : INavigationMessage
  {
    public readonly ECanvasType Canvas;

    public ClearRequestMessage(ECanvasType canvas)
    {
      Canvas = canvas;
    }
  }
}