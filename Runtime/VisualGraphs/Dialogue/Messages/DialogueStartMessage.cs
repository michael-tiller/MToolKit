using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Messages
{
  /// <summary>
  ///   Message to start a dialogue graph execution.
  ///   Sent to trigger a dialogue graph to begin execution.
  /// </summary>
  public readonly struct DialogueStartMessage : IGameMessage
  {
    public readonly string DialogueId;

    public DialogueStartMessage(string dialogueId)
    {
      DialogueId = dialogueId;
    }
  }
}

