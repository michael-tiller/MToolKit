using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Quest
{
  /// <summary>
  /// Game-agnostic quest lifecycle and state management service.
  /// Orchestrates quest activation, progress tracking, and graph coordination.
  /// </summary>
  public interface IQuestManager
  {
    // ==================== LIFECYCLE ====================

    /// <summary>
    /// Starts a quest, loads its objective graphs, and emits QuestStartedMessage.
    /// </summary>
    /// <param name="quest">The quest definition to start</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if quest was started, false if already active</returns>
    UniTask<bool> StartQuestAsync(QuestDefinition quest, CancellationToken ct = default);

    /// <summary>
    /// Completes a quest (all objectives done), unloads its objective graphs, and emits QuestCompletedMessage.
    /// Quest moves to "completed but unclaimed" state. Call ClaimQuest() to grant rewards.
    /// </summary>
    /// <param name="questGuid">GUID of the quest to complete</param>
    /// <returns>True if quest was completed, false if not active or already completed</returns>
    bool CompleteQuest(string questGuid);

    /// <summary>
    /// Claims a completed quest's rewards and emits QuestClaimedMessage.
    /// Game's reward system should subscribe to QuestClaimedMessage to grant rewards.
    /// Quest moves from "completed" to "claimed" state.
    /// </summary>
    /// <param name="questGuid">GUID of the quest to claim</param>
    /// <returns>True if quest was claimed, false if not completed</returns>
    bool ClaimQuest(string questGuid);

    /// <summary>
    /// Abandons a quest, unloads its graphs, resets progress, emits QuestAbandonedMessage.
    /// </summary>
    /// <param name="questGuid">GUID of the quest to abandon</param>
    /// <returns>True if quest was abandoned, false if not active</returns>
    bool AbandonQuest(string questGuid);

    // ==================== QUERIES ====================

    /// <summary>
    /// Gets all currently active quests (in progress, objectives not yet complete).
    /// </summary>
    IReadOnlyList<QuestRuntimeState> GetActiveQuests();

    /// <summary>
    /// Gets all completed but unclaimed quests (objectives done, rewards not claimed).
    /// UI should show these with a "claim reward" prompt.
    /// </summary>
    IReadOnlyList<QuestRuntimeState> GetCompletedUnclaimedQuests();

    /// <summary>
    /// Gets all claimed quest GUIDs (rewards already collected).
    /// </summary>
    IReadOnlyList<string> GetClaimedQuestGuids();

    /// <summary>
    /// Checks if a quest is currently active (in progress).
    /// </summary>
    bool IsQuestActive(string questGuid);

    /// <summary>
    /// Checks if a quest's objectives are complete (may or may not be claimed).
    /// </summary>
    bool IsQuestCompleted(string questGuid);

    /// <summary>
    /// Checks if a quest's rewards have been claimed.
    /// </summary>
    bool IsQuestClaimed(string questGuid);

    /// <summary>
    /// Gets the runtime state for an active quest.
    /// </summary>
    /// <returns>QuestRuntimeState or null if quest is not active</returns>
    QuestRuntimeState GetQuestState(string questGuid);

    /// <summary>
    /// Calculates quest completion percentage based on objective progress.
    /// </summary>
    /// <returns>0.0 to 1.0, or 0 if quest is not active</returns>
    float GetQuestCompletionPercentage(string questGuid);

    // ==================== PERSISTENCE SUPPORT ====================

    /// <summary>
    /// Gets serializable quest state for save systems.
    /// Game is responsible for calling this and storing the data.
    /// </summary>
    QuestManagerSaveData GetSaveData();

    /// <summary>
    /// Restores quest state from save data.
    /// Call this during game load before starting any quests.
    /// </summary>
    UniTask RestoreSaveDataAsync(QuestManagerSaveData saveData, CancellationToken ct = default);
  }
}

