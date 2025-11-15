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
    ///   Optional localization key for the dialogue text
    /// </summary>
    public readonly string LocalizationKey;

    /// <summary>
    ///   The graph ID that triggered this dialogue
    /// </summary>
    public readonly string GraphId;

    public DialogueShowMessage(string dialogueText, string speakerName, string localizationKey = null, string graphId = null)
    {
      DialogueText = dialogueText ?? string.Empty;
      SpeakerName = speakerName ?? string.Empty;
      LocalizationKey = localizationKey;
      GraphId = graphId;
    }
  }
}

