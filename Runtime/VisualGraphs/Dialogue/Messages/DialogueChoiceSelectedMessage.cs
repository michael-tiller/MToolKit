using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Messages
{
  /// <summary>
  ///   Message indicating which choice the player selected.
  ///   Published when the player clicks a choice button (0-2 for choices 1-3).
  /// </summary>
  public readonly struct DialogueChoiceSelectedMessage : IGameMessage
  {
    /// <summary>
    ///   The index of the selected choice (0-based, 0-2 for choices 1-3)
    /// </summary>
    public readonly int ChoiceIndex;

    /// <summary>
    ///   The graph ID associated with this choice selection
    /// </summary>
    public readonly string GraphId;

    public DialogueChoiceSelectedMessage(int choiceIndex, string graphId = null)
    {
      ChoiceIndex = choiceIndex;
      GraphId = graphId;
    }
  }
}

