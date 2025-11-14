using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Serializable quest state for save/load systems.
  /// Game is responsible for persisting this data.
  /// </summary>
  [Serializable]
  public sealed class QuestManagerSaveData
  {
    /// <summary>
    /// GUIDs of all claimed quests (rewards already collected)
    /// </summary>
    public List<string> ClaimedQuestGuids = new();

    /// <summary>
    /// State for all currently active quests (in progress)
    /// </summary>
    public List<ActiveQuestSaveData> ActiveQuests = new();

    /// <summary>
    /// State for all completed but unclaimed quests (objectives done, rewards not claimed)
    /// </summary>
    public List<ActiveQuestSaveData> CompletedUnclaimedQuests = new();
  }

  /// <summary>
  /// Save data for a single active quest
  /// </summary>
  [Serializable]
  public sealed class ActiveQuestSaveData
  {
    /// <summary>
    /// GUID of the quest definition
    /// </summary>
    public string QuestGuid;

    /// <summary>
    /// When the quest was started
    /// </summary>
    public DateTime StartedAt;

    /// <summary>
    /// Serialized graph state (key-value pairs)
    /// Format: Dictionary&lt;string, object&gt; as JSON or binary
    /// </summary>
    public string SerializedGraphState;
  }
}

