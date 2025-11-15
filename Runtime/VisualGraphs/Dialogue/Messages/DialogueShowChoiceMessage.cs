using System.Collections.Generic;
using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Messages
{
  /// <summary>
  ///   Message to show dialogue choices in the dialogue view.
  ///   Published when a DialogueChoiceNode is executed.
  /// </summary>
  public readonly struct DialogueShowChoiceMessage : IGameMessage
  {
    /// <summary>
    ///   List of choice texts (up to 3 choices)
    /// </summary>
    public readonly IReadOnlyList<ChoiceData> Choices;

    /// <summary>
    ///   The graph ID that triggered this choice
    /// </summary>
    public readonly string GraphId;

    public DialogueShowChoiceMessage(IReadOnlyList<ChoiceData> choices, string graphId = null)
    {
      Choices = choices ?? new List<ChoiceData>();
      GraphId = graphId;
    }

    /// <summary>
    ///   Data for a single choice option
    /// </summary>
    public readonly struct ChoiceData
    {
      public readonly string Text;
      public readonly string LocalizationKey;

      public ChoiceData(string text, string localizationKey = null)
      {
        Text = text ?? string.Empty;
        LocalizationKey = localizationKey;
      }
    }
  }
}

