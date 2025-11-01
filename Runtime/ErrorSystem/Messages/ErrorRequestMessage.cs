// Core/MessageBus/Messages/ErrorRequestMessage.cs

using MToolKit.Runtime.ErrorSystem.Interface;

namespace MToolKit.Runtime.ErrorSystem.Messages
{
  public readonly struct ErrorRequestMessage : IErrorMessage
  {
    public readonly string Message;
    public readonly bool Fatal;

    public ErrorRequestMessage(string message, bool fatal = false)
    {
      Message = message;
      Fatal = fatal;
    }
  }
}