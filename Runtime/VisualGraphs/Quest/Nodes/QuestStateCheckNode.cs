using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Checks the state of a quest and branches execution.
  ///   Output ports: NotStarted, Active, Complete, Claimed.
  ///   Uses QuestAssetReference to reference the quest definition.
  ///   Note: "NotStarted" includes both never-started quests and abandoned quests,
  ///   as QuestManager does not distinguish between them.
  /// </summary>
  [CreateNodeMenu("Quest/Check State")]
  [NodeTint("#B8905C")]
  [NodeWidth(400)]
  public sealed class QuestStateCheckNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if quest has not been started (includes never-started and abandoned quests)")]
    public NodeConnection NotStarted;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if quest is currently active (in progress)")]
    public NodeConnection Active;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if quest is completed but rewards not yet claimed")]
    public NodeConnection Complete;

    [Output(connectionType = ConnectionType.Multiple)]
    [Tooltip("Executes if quest rewards have been claimed")]
    public NodeConnection Claimed;

    [BoxGroup("Quest")]
    [InfoBox("The quest definition to check. Can be loaded via Addressables.")]
    [Required]
    public QuestAssetReference Quest;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

