using MToolKit.Runtime.VisualGraphs.Dialogue.Graphs;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Definitions
{
  /// <summary>
  ///   Dialogue definition asset linking a dialogue ID to a graph and configuration.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Dialogue Definition", fileName = "DialogueDef", order = 301)]
  public sealed class DialogueDefinition : ScriptableObject
  {
    [BoxGroup("Identity")]
    [Tooltip("Unique dialogue identifier")]
    public string DialogueId = "dialogue_001";

    [BoxGroup("Graph")]
    [Required]
    [Tooltip("Dialogue graph asset to execute")]
    public DialogueGraphAsset GraphAsset;

    [BoxGroup("Variables")]
    [InlineEditor]
    [Tooltip("Initial variables for this dialogue (applied after global, before save)")]
    public GraphVariableSet InitialVariables;

    [BoxGroup("Configuration")]
    [Tooltip("Start this dialogue when NPC is interacted with")]
    public bool StartOnInteract = true;

    [BoxGroup("Configuration")]
    [Tooltip("NPC identifier for this dialogue")]
    public string NpcId = "npc_001";

    [BoxGroup("Addressable")]
    [Tooltip("Optional addressable key for dynamic loading")]
    public string AddressableKey;
  }
}