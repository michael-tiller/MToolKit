namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  /// Quest category for UI/filtering and assignment policy decisions.
  /// </summary>
  public enum EQuestCategory
  {
    Main = 0,
    Side = 1,
    Secret = 2,
    Repeatable = 3,
    Daily = 4
  }

  /// <summary>
  /// How a quest becomes available and starts.
  /// </summary>
  public enum EQuestActivationMode
  {
    /// <summary>
    /// Only started via explicit call from game code (e.g., GameMessageBroker.Publish(StartQuestRequestMessage))
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Auto-accept when all prerequisites are met
    /// </summary>
    AutoWhenAvailable = 1,

    /// <summary>
    /// Becomes "offerable" but needs a provider (NPC/UI) to accept
    /// </summary>
    OfferWhenAvailable = 2,

    /// <summary>
    /// Only starts from a specific trigger/event
    /// </summary>
    TriggerOnly = 3
  }

  /// <summary>
  /// Visibility rules for quests in the quest log/UI.
  /// </summary>
  public enum EQuestVisibility
  {
    /// <summary>
    /// Shows in log even if locked
    /// </summary>
    AlwaysVisible = 0,

    /// <summary>
    /// Appears once prerequisites are met / ready
    /// </summary>
    HiddenUntilAvailable = 1,

    /// <summary>
    /// "Secret" until player accepts it
    /// </summary>
    HiddenUntilAccepted = 2,

    /// <summary>
    /// Fully invisible, only used as a flag / backend
    /// </summary>
    NeverVisible = 3
  }

  /// <summary>
  /// Runtime state of a quest.
  /// </summary>
  public enum EQuestState
  {
    /// <summary>
    /// Not yet available (prerequisites not met)
    /// </summary>
    Locked = 0,

    /// <summary>
    /// Can be offered/auto-assigned
    /// </summary>
    Available = 1,

    /// <summary>
    /// In progress
    /// </summary>
    Active = 2,

    /// <summary>
    /// Completed (objectives done, may or may not be claimed)
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Failed (if quest is failable)
    /// </summary>
    Failed = 4
  }
}

