using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Messages
{
  /// <summary>
  ///   Message to progress or close the dialogue view.
  ///   Published when the player clicks "Next" or when dialogue should be closed.
  /// </summary>
  public readonly struct DialogueProgressMessage : IGameMessage
  {
    /// <summary>
    ///   If true, closes the dialogue view. If false, progresses to next line.
    /// </summary>
    public readonly bool ShouldClose;

    /// <summary>
    ///   The graph ID associated with this dialogue progress
    /// </summary>
    public readonly string GraphId;

    public DialogueProgressMessage(bool shouldClose = false, string graphId = null)
    {
      ShouldClose = shouldClose;
      GraphId = graphId;
    }
  }
}

