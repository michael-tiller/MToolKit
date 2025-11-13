using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Definitions
{
  /// <summary>
  ///   Central registry asset containing all graph definitions and global variables.
  ///   Referenced by VisualGraphBootstrapMB to initialize the graph system.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Registry", fileName = "VisualGraphRegistry", order = 400)]
  public sealed class VisualGraphRegistry : ScriptableObject
  {
    [BoxGroup("Global Variables")]
    [InlineEditor]
    [Tooltip("Global variables applied to all graphs")]
    public GlobalGraphVariables GlobalVariables;

    [BoxGroup("Quest Graphs")]
    [ListDrawerSettings(ShowFoldout = true)]
    [Tooltip("All quest definitions")]
    public List<QuestDefinition> QuestDefinitions = new();

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