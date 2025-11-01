// MessageBus/Messages/BackRequestMessage.cs
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;

namespace MToolKit.Runtime.Navigation.Events
{
  public readonly struct BackRequestMessage : INavigationMessage
  {
    public readonly ECanvasType Canvas;
    public BackRequestMessage(ECanvasType canvas)
    {
      Canvas = canvas;
    }
  }

}