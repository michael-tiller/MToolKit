using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using MToolKit.Runtime.VisualGraphs.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MToolKit.Runtime.VisualGraphs.Dialogue.Graphs
{
  /// <summary>
  ///   Dialogue graph asset for authoring dialogue logic.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Dialogue Graph", fileName = "DialogueGraph", order = 101)]
  public sealed class DialogueGraphAsset : NodeGraph
  {
    [BoxGroup("Description")]
    [TextArea(3, 10)]
    [HideLabel]
    public string Description;

    [BoxGroup("Performance")]
    [Tooltip("Max nodes executed per message (prevents infinite loops)")]
    [Range(64, 4096)]
    [InfoBox("Default: 1024. Increase for complex dialogues with many branches.")]
    public int MaxExecutionSteps = 1024;

    /// <summary>
    ///   Optional declared-variables block: validation/tooling metadata, NOT an init leg.
    ///   Init precedence stays GlobalGraphVariables → definition InitialVariables → restored save state (wins);
    ///   declared defaults reach runtime through typed-accessor fallback, not ApplyTo.
    /// </summary>
    [BoxGroup("Variables")]
    [Tooltip("Optional declared-variable set for this graph. Used by export validation, the variable picker, " +
             "and the text authoring importer. Undeclared keys remain legal at runtime.")]
    public GraphVariableSet DeclaredVariables;

    [BoxGroup("Event Subscriptions")]
    [InfoBox("Graph will ONLY execute when these MessagePipe events are received.")]
    [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
    public List<MessageSubscription> Subscriptions = new();

#if UNITY_EDITOR
    /// <summary>
    ///   Override CopyNode to automatically regenerate GUIDs when duplicating nodes.
    ///   This ensures each node has a unique GUID even after copy/paste.
    /// </summary>
    public override Node CopyNode(Node original)
    {
      var copied = base.CopyNode(original);

      // Regenerate GUID for VisualGraphNodeBase nodes to ensure uniqueness
      if (copied is VisualGraphNodeBase vgNode)
      {
        vgNode.RegenerateGuid();
      }

      return copied;
    }

    [BoxGroup("Utilities")]
    [Button("Fix Duplicate GUIDs", ButtonSizes.Medium)]
    [GUIColor(1.0f, 0.6f, 0.3f)]
    [Tooltip("Scans the graph for duplicate GUIDs and regenerates them. Use this if you see duplicate GUID errors.")]
    private void FixDuplicateGuids()
    {
      var guidCounts = new Dictionary<string, List<VisualGraphNodeBase>>();

      // Find all VisualGraphNodeBase nodes and group by GUID
      foreach (var node in nodes)
      {
        if (node is VisualGraphNodeBase vgNode && !string.IsNullOrEmpty(vgNode.Guid))
        {
          if (!guidCounts.ContainsKey(vgNode.Guid))
            guidCounts[vgNode.Guid] = new List<VisualGraphNodeBase>();
          guidCounts[vgNode.Guid].Add(vgNode);
        }
      }

      // Find duplicates and regenerate GUIDs (keep first occurrence, regenerate others)
      int fixedCount = 0;
      foreach (var kvp in guidCounts)
      {
        if (kvp.Value.Count > 1)
        {
          // Keep the first node's GUID, regenerate the rest
          for (int i = 1; i < kvp.Value.Count; i++)
          {
            kvp.Value[i].RegenerateGuid();
            fixedCount++;
          }
        }
      }

      if (fixedCount > 0)
      {
        EditorUtility.SetDirty(this);
        Debug.Log($"Fixed {fixedCount} duplicate GUID(s) in dialogue graph '{name}'");
      }
      else
      {
        Debug.Log($"No duplicate GUIDs found in dialogue graph '{name}'");
      }
    }
#endif
  }
}