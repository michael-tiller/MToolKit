using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Messages
{
  /// <summary>
  ///   Message to show a dialogue line in the dialogue view.
  ///   Published when a DialogueLineNode is executed.
  /// </summary>
  public readonly struct DialogueShowMessage : IGameMessage
  {
    /// <summary>
    ///   The dialogue text to display
    /// </summary>
    public readonly string DialogueText;

    /// <summary>
    ///   The speaker name/ID to display
    /// </summary>
    public readonly string SpeakerName;

    /// <summary>
    ///   The graph ID that triggered this dialogue
    /// </summary>
    public readonly string GraphId;

    /// <summary>
    ///   The table name that contains the dialogue text and speaker name
    /// </summary>
    public readonly string Table;

    public DialogueShowMessage(string dialogueText, string speakerName, string table, string graphId = null)
    {
      DialogueText = dialogueText ?? string.Empty;
      SpeakerName = speakerName ?? string.Empty;
      Table = table ?? string.Empty;
      GraphId = graphId;
    }
  }
}

