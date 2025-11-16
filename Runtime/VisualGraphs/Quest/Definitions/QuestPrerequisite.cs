using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  /// Prerequisite condition for a quest to become available.
  /// All conditions must be met (AND logic).
  /// </summary>
  [System.Serializable]
  public sealed class QuestPrerequisite
  {
    [Tooltip("Optional: Required quest GUID. Leave empty if not needed.")]
    public string RequiredQuestGuid;

    [Tooltip("Optional: Required state of the prerequisite quest.")]
    [ShowIf("@!string.IsNullOrEmpty(RequiredQuestGuid)")]
    public EQuestState RequiredState = EQuestState.Completed;

    [Tooltip("Optional: Required world flag key. Leave empty if not needed.")]
    public string RequiredFlagKey;

    [Tooltip("Optional: Required value for the world flag.")]
    [ShowIf("@!string.IsNullOrEmpty(RequiredFlagKey)")]
    public bool RequiredFlagValue = true;

    [Tooltip("Optional: Required player level. Set to 0 or negative to ignore.")]
    public int RequiredLevel = 0;

    /// <summary>
    /// Check if this prerequisite has any conditions set.
    /// </summary>
    public bool HasConditions()
    {
      return !string.IsNullOrEmpty(RequiredQuestGuid) ||
             !string.IsNullOrEmpty(RequiredFlagKey) ||
             RequiredLevel > 0;
    }
  }
}

