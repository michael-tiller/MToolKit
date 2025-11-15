using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Messages
{
  /// <summary>
  ///   Message to continue dialogue execution from stored next node IDs.
  ///   Published by DialogueService when DialogueProgressMessage is received,
  ///   to trigger the GraphRunner to continue processing the next nodes.
  /// </summary>
  public readonly struct DialogueContinueMessage : IGameMessage
  {
    /// <summary>
    ///   The graph ID to continue execution for
    /// </summary>
    public readonly string GraphId;

    public DialogueContinueMessage(string graphId)
    {
      GraphId = graphId;
    }
  }
}

