using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  ///   Defines a campaign (collection of related quests).
  ///   Can be linear (sequential) or branching (player choice).
  ///   Uses GUID for safe references (no string ID typos!).
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quests/Quest Campaign", fileName = "QuestCampaign", order = 290)]
  [InlineEditor]
  public sealed class QuestCampaign : GuidScriptableObject
  {

    [BoxGroup("Display")]
    [TextArea(3, 8)]
    [Tooltip("Campaign description")]
    public string Description = "";

    [BoxGroup("Display")]
    [PreviewField(80, ObjectFieldAlignment.Left)]
    [Tooltip("Campaign icon displayed in UI")]
    public Sprite Icon;

    [BoxGroup("Graph")]
    [Tooltip("Optional campaign graph for orchestration logic (quest unlocking, campaign rewards, cutscenes)")]
    public QuestGraphAsset CampaignGraph;

    [BoxGroup("Quests")]
    [InfoBox("All quests in this campaign. Order matters if Sequential is enabled.")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true, ShowFoldout = true)]
    [Required]
    public List<QuestDefinition> Quests = new();

    [BoxGroup("Progression")]
    [Tooltip("If true, quests must be completed in order. If false, player can do any quest.")]
    public bool Sequential = true;

    [BoxGroup("Progression")]
    [Tooltip("If true, all quests must be completed. If false, campaign completes after any subset.")]
    public bool AllQuestsRequired = true;

    [BoxGroup("Progression")]
    [ShowIf("AllQuestsRequired", false)]
    [MinValue(1)]
    [Tooltip("Number of quests required to complete campaign (if not all required)")]
    public int RequiredQuestCount = 1;

    [BoxGroup("Configuration")]
    [Tooltip("Auto-start first quest when campaign begins")]
    public bool AutoStartFirstQuest = true;

    [BoxGroup("Configuration")]
    [Tooltip("Campaign category for UI/filtering")]
    public string Category = "Main";

    /// <summary>
    ///   Get the next available quest in the campaign.
    ///   Returns null if all quests complete or none available.
    /// </summary>
    public QuestDefinition GetNextAvailableQuest(Dictionary<string, IGraphState> questStates)
    {
      if (Sequential)
      {
        // Sequential: return first incomplete quest
        foreach (var quest in Quests)
        {
          if (!questStates.TryGetValue(quest.Guid, out var state) || !quest.IsComplete(state))
            return quest;
        }
        return null; // All complete
      }
      else
      {
        // Non-sequential: return any incomplete quest
        return Quests.FirstOrDefault(quest =>
          !questStates.TryGetValue(quest.Guid, out var state) || !quest.IsComplete(state));
      }
    }

    /// <summary>
    ///   Calculate campaign completion percentage (0.0 to 1.0).
    /// </summary>
    public float GetCompletionPercentage(Dictionary<string, IGraphState> questStates)
    {
      if (Quests.Count == 0) return 1.0f;

      var completedCount = Quests.Count(quest =>
        questStates.TryGetValue(quest.Guid, out var state) && quest.IsComplete(state));

      return (float)completedCount / Quests.Count;
    }

    /// <summary>
    ///   Check if campaign is complete.
    /// </summary>
    public bool IsComplete(Dictionary<string, IGraphState> questStates)
    {
      if (Quests.Count == 0) return false;

      if (AllQuestsRequired)
      {
        // All quests must be complete
        return Quests.All(quest =>
          questStates.TryGetValue(quest.Guid, out var state) && quest.IsComplete(state));
      }
      else
      {
        // Only need RequiredQuestCount quests complete
        var completedCount = Quests.Count(quest =>
          questStates.TryGetValue(quest.Guid, out var state) && quest.IsComplete(state));
        return completedCount >= RequiredQuestCount;
      }
    }

    /// <summary>
    ///   Get list of all completed quests.
    /// </summary>
    public List<QuestDefinition> GetCompletedQuests(Dictionary<string, IGraphState> questStates)
    {
      return Quests.Where(quest =>
        questStates.TryGetValue(quest.Guid, out var state) && quest.IsComplete(state)).ToList();
    }

    /// <summary>
    ///   Get list of all active (started but not complete) quests.
    /// </summary>
    public List<QuestDefinition> GetActiveQuests(Dictionary<string, IGraphState> questStates)
    {
      return Quests.Where(quest =>
        questStates.TryGetValue(quest.Guid, out var state) && !quest.IsComplete(state)).ToList();
    }

    /// <summary>
    ///   Get list of all locked (not yet available) quests.
    /// </summary>
    public List<QuestDefinition> GetLockedQuests(Dictionary<string, IGraphState> questStates)
    {
      if (!Sequential) return new List<QuestDefinition>(); // No locked quests if non-sequential

      var locked = new List<QuestDefinition>();
      var foundIncomplete = false;

      foreach (var quest in Quests)
      {
        if (!questStates.TryGetValue(quest.Guid, out var state) || !quest.IsComplete(state))
        {
          if (foundIncomplete)
            locked.Add(quest); // All quests after first incomplete are locked
          foundIncomplete = true;
        }
      }

      return locked;
    }

#if UNITY_EDITOR
    [BoxGroup("Graph")]
    [Button("Create & Link Campaign Graph", ButtonSizes.Medium)]
    [GUIColor(0.3f, 0.8f, 0.3f)]
    [ShowIf("@CampaignGraph == null")]
    [Tooltip("Links to existing QuestGraphAsset if found, otherwise creates a new one in the same folder with '_Graph' suffix")]
    private void CreateAndLinkCampaignGraph()
    {
      var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
      if (string.IsNullOrEmpty(assetPath))
      {
        UnityEngine.Debug.LogError("Cannot create graph: Asset must be saved to disk first.");
        return;
      }

      var folderPath = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
      var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
      var graphName = $"{assetName}_Graph";
      var graphPath = $"{folderPath}/{graphName}.asset";

      // Check if graph already exists - if so, just link it
      var existingGraph = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestGraphAsset>(graphPath);
      if (existingGraph != null)
      {
        CampaignGraph = existingGraph;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEngine.Debug.Log($"Linked to existing campaign graph: {graphPath}");
        return;
      }

      // Create new graph asset
      var graphAsset = ScriptableObject.CreateInstance<QuestGraphAsset>();
      graphAsset.name = graphName;
      graphAsset.Description = $"Campaign graph for {DisplayName}";

      UnityEditor.AssetDatabase.CreateAsset(graphAsset, graphPath);
      UnityEditor.AssetDatabase.SaveAssets();
      UnityEditor.AssetDatabase.Refresh();

      // Link it
      CampaignGraph = graphAsset;
      UnityEditor.EditorUtility.SetDirty(this);

      UnityEngine.Debug.Log($"Created and linked campaign graph: {graphPath}");
    }
#endif
  }
}

