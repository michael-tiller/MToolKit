using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Dialogue.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MToolKit.Runtime.VisualGraphs.Config
{
  /// <summary>
  ///   Central registry asset containing all graph definitions and global variables.
  ///   Referenced by VisualGraphPlugin to initialize the graph system.
  ///   Uses addressable asset references for quest definitions.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Registry", fileName = "VisualGraphRegistry", order = 400)]
  [InlineEditor]
  public sealed class VisualGraphRegistry : ScriptableObject
  {
    [BoxGroup("Global Variables")]
    [InlineEditor]
    [Tooltip("Global variables applied to all graphs")]
    public GlobalGraphVariables GlobalVariables;

    [BoxGroup("Quest Graphs")]
    [ListDrawerSettings(ShowFoldout = true)]
    [Tooltip("All quest definitions as addressable asset references. Drag QuestDefinition assets here.")]
    public List<QuestAssetReference> QuestDefinitions = new();

    [BoxGroup("Dialogue Graphs")]
    [ListDrawerSettings(ShowFoldout = true)]
    [Tooltip("All dialogue definitions")]
    public List<DialogueDefinition> DialogueDefinitions = new();

    [BoxGroup("Debug")]
    [ReadOnly]
    [ShowInInspector]
    public int TotalGraphCount => QuestDefinitions.Count + DialogueDefinitions.Count;
  }
}