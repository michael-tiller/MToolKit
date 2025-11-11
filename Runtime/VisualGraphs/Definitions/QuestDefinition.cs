using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Definitions
{
    /// <summary>
    /// Quest definition asset linking a quest ID to a graph and configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quest Definition", fileName = "QuestDef", order = 300)]
    public sealed class QuestDefinition : ScriptableObject
    {
        [BoxGroup("Identity")]
        [Tooltip("Unique quest identifier")]
        public string questId = "quest_001";

        [BoxGroup("Graph")]
        [Required]
        [Tooltip("Quest graph asset to execute")]
        public QuestGraphAsset graphAsset;

        [BoxGroup("Variables")]
        [InlineEditor(InlineEditorModes.GUIOnly)]
        [Tooltip("Initial variables for this quest (applied after global, before save)")]
        public GraphVariableSet initialVariables;

        [BoxGroup("Configuration")]
        [Tooltip("Auto-start this quest when the game loads")]
        public bool autoStart = false;

        [BoxGroup("Configuration")]
        [Tooltip("Quest category for UI/filtering")]
        public string category = "Main";

        [BoxGroup("Addressable")]
        [Tooltip("Optional addressable key for dynamic loading")]
        public string addressableKey;
    }
}

